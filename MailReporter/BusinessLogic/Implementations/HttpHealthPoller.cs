namespace BusinessLogic.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    using Azure.Core;

    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.Http;

    using DomainModel;

    using Microsoft.Extensions.Configuration;

    using Newtonsoft.Json.Linq;

    using Repos;

    /// <inheritdoc cref="IHttpHealthPoller"/>
    /// <remarks>
    /// Pull, not push, for the same reason the Azure Functions tiles pull: an endpoint that stops
    /// answering is itself the signal. A push-based monitor cannot tell "nothing is wrong" apart from
    /// "nothing is reporting", whereas a poll that fails is recorded as an error, and a poll that
    /// stops happening leaves the job overdue.
    /// </remarks>
    public class HttpHealthPoller : IHttpHealthPoller
    {
        /// <summary>Error bodies are kept for context but must not bloat the Mongo document.</summary>
        internal const int MaxErrorSummaryLength = 2000;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TokenCredential _credential;
        private readonly IConfiguration _configuration;
        private readonly IJobRepo _jobRepo;
        private readonly IJobExecutionService _jobExecutionService;
        private readonly TimeProvider _timeProvider;

        public HttpHealthPoller(
            IHttpClientFactory httpClientFactory,
            TokenCredential credential,
            IConfiguration configuration,
            IJobRepo jobRepo,
            IJobExecutionService jobExecutionService,
            TimeProvider timeProvider)
        {
            _httpClientFactory = httpClientFactory;
            _credential = credential;
            _configuration = configuration;
            _jobRepo = jobRepo;
            _jobExecutionService = jobExecutionService;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<HttpHealthPollResult>> PollAll(CancellationToken cancellationToken)
        {
            var options = _configuration.GetSection(HttpHealthOptions.SectionName).Get<HttpHealthOptions>() ?? new HttpHealthOptions();
            if (options.Monitors == null || options.Monitors.Count == 0)
            {
                return Array.Empty<HttpHealthPollResult>();
            }

            var jobs = (await _jobRepo.GetAll()).ToList();
            var results = new List<HttpHealthPollResult>();

            foreach (var monitor in options.Monitors)
            {
                results.Add(await Poll(monitor, jobs, options, cancellationToken));
            }

            return results;
        }

        private async Task<HttpHealthPollResult> Poll(HttpHealthMonitor monitor, IEnumerable<Job> jobs, HttpHealthOptions options, CancellationToken cancellationToken)
        {
            var result = new HttpHealthPollResult { MonitorName = monitor.Name, JobName = monitor.TargetJobName };

            var job = jobs.FirstOrDefault(j => string.Equals(j.Name, monitor.TargetJobName, StringComparison.OrdinalIgnoreCase));
            if (job == null)
            {
                // Creating the job here would hide a typo behind a brand-new tile that nobody expects,
                // so an unmatched monitor is reported instead of silently inventing one.
                result.SkippedReason = $"no job named '{monitor.TargetJobName}' exists";
                return result;
            }

            var (status, subject, errorSummary) = await Fetch(monitor, options.TimeoutSeconds, cancellationToken);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            await _jobExecutionService.Create(new JobExecution
            {
                JobId = job.Id,
                JobName = job.Name,
                Started = now,
                Finished = now,
                Status = status,
                OriginalSubject = subject,
                ErrorSummary = errorSummary ?? string.Empty,
            });

            result.Recorded = true;
            result.Status = status;
            result.Subject = subject;
            return result;
        }

        private async Task<(JobExecutionStatus Status, string Subject, string ErrorSummary)> Fetch(
            HttpHealthMonitor monitor,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Get, monitor.Url);

                if (!string.IsNullOrWhiteSpace(monitor.Scope))
                {
                    var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { monitor.Scope }), timeout.Token);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    // An unreachable or refusing endpoint is a problem in its own right: a monitor that
                    // cannot be read is not the same as a monitor that reports health.
                    return (JobExecutionStatus.Error,
                        $"{monitor.Name}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                        Truncate(body));
                }

                var health = ParseResponse(body);
                var status = health.Errors > 0 ? JobExecutionStatus.Error : JobExecutionStatus.Success;
                var subject = string.IsNullOrWhiteSpace(health.Message)
                    ? $"{monitor.Name}: {health.Errors} errors, {health.Warnings} warnings"
                    : health.Message;

                return (status, subject, status == JobExecutionStatus.Error ? Truncate(body) : string.Empty);
            }
            catch (Exception ex)
            {
                return (JobExecutionStatus.Error, $"{monitor.Name}: {ex.GetType().Name} - {ex.Message}", Truncate(ex.ToString()));
            }
        }

        /// <summary>
        /// Reads the health numbers, tolerating both a bare body and one wrapped in an envelope —
        /// Xv.Api wraps every controller response in <c>{ meta, data }</c>.
        /// </summary>
        public static HttpHealthResponse ParseResponse(string body)
        {
            var root = JObject.Parse(body);
            var payload = Child(root, "data") as JObject ?? root;

            return new HttpHealthResponse
            {
                Errors = Child(payload, "errors")?.Value<int?>() ?? 0,
                Warnings = Child(payload, "warnings")?.Value<int?>() ?? 0,
                Message = Child(payload, "message")?.Value<string>(),
            };
        }

        private static JToken Child(JObject parent, string name) =>
            parent.GetValue(name, StringComparison.OrdinalIgnoreCase);

        private static string Truncate(string value) =>
            string.IsNullOrEmpty(value) || value.Length <= MaxErrorSummaryLength
                ? value
                : value.Substring(0, MaxErrorSummaryLength);
    }
}
