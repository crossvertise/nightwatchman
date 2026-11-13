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
    using BusinessLogic.Models.LogQuery;

    using DomainModel;

    using Microsoft.Extensions.Configuration;

    using Newtonsoft.Json.Linq;

    using Repos;

    /// <inheritdoc cref="ILogQueryPoller"/>
    /// <remarks>
    /// The same shape as <see cref="HttpHealthPoller"/> — acquire a token, read a number, write a job
    /// execution — against a different source. The payment sweep can only see problems that reached a
    /// payment row; this sees the ones that never got that far, by reading the request telemetry the
    /// API already writes.
    /// <para>
    /// <b>Sampling caveat.</b> Live telemetry is adaptively sampled (roughly 57 % of requests retained
    /// on a normal day), so a query that counts rows undercounts. Sum <c>ItemCount</c> in the KQL
    /// instead — that is the sampler's own estimate of how many requests each retained row stands
    /// for. Even so, a lone error can be dropped entirely, which is why this is a safety net over the
    /// durable checks rather than a replacement for them.
    /// </para>
    /// </remarks>
    public class LogQueryPoller : ILogQueryPoller
    {
        /// <summary>Error bodies are kept for context but must not bloat the Mongo document.</summary>
        internal const int MaxErrorSummaryLength = 2000;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TokenCredential _credential;
        private readonly IConfiguration _configuration;
        private readonly IJobRepo _jobRepo;
        private readonly IJobExecutionService _jobExecutionService;
        private readonly TimeProvider _timeProvider;

        public LogQueryPoller(
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

        public async Task<IReadOnlyList<HttpHealthPollResult>> RunAll(CancellationToken cancellationToken)
        {
            var options = _configuration.GetSection(LogQueryOptions.SectionName).Get<LogQueryOptions>() ?? new LogQueryOptions();
            if (options.Monitors == null || options.Monitors.Count == 0)
            {
                return Array.Empty<HttpHealthPollResult>();
            }

            var jobs = (await _jobRepo.GetAll()).ToList();
            var results = new List<HttpHealthPollResult>();

            foreach (var monitor in options.Monitors)
            {
                results.Add(await Run(monitor, jobs, options, cancellationToken));
            }

            return results;
        }

        /// <summary>Builds the Log Analytics query URL for one monitor.</summary>
        public static string BuildUrl(LogQueryMonitor monitor) =>
            $"https://api.loganalytics.io/v1/workspaces/{Uri.EscapeDataString(monitor.WorkspaceId ?? string.Empty)}/query" +
            $"?timespan={Uri.EscapeDataString(string.IsNullOrWhiteSpace(monitor.Timespan) ? "PT30M" : monitor.Timespan)}" +
            $"&query={Uri.EscapeDataString(monitor.Query ?? string.Empty)}";

        /// <summary>
        /// Reads the first row of the primary table into the same shape a self-reporting endpoint
        /// returns, so both kinds of monitor are recorded identically.
        /// </summary>
        /// <remarks>
        /// No rows means nothing matched, which is health, not absence of information — a query that
        /// filters for errors returns nothing precisely when there are none. Treating that as unknown
        /// would make a healthy system look broken on every quiet poll.
        /// </remarks>
        public static HttpHealthResponse ParseResponse(string body)
        {
            var table = (JObject.Parse(body)["tables"] as JArray)?.FirstOrDefault() as JObject;
            var columns = (table?["columns"] as JArray)?.Select(c => c["name"]?.Value<string>()).ToList();
            var row = (table?["rows"] as JArray)?.FirstOrDefault() as JArray;

            if (columns == null || row == null)
            {
                return new HttpHealthResponse();
            }

            return new HttpHealthResponse
            {
                Errors = Cell(columns, row, "errors")?.Value<int?>() ?? 0,
                Warnings = Cell(columns, row, "warnings")?.Value<int?>() ?? 0,
                Message = Cell(columns, row, "message")?.Value<string>(),
            };
        }

        private static JToken Cell(List<string> columns, JArray row, string name)
        {
            var index = columns.FindIndex(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index >= row.Count || row[index].Type == JTokenType.Null)
            {
                return null;
            }

            return row[index];
        }

        private async Task<HttpHealthPollResult> Run(LogQueryMonitor monitor, IEnumerable<Job> jobs, LogQueryOptions options, CancellationToken cancellationToken)
        {
            var result = new HttpHealthPollResult { MonitorName = monitor.Name, JobName = monitor.TargetJobName };

            var job = jobs.FirstOrDefault(j => string.Equals(j.Name, monitor.TargetJobName, StringComparison.OrdinalIgnoreCase));
            if (job == null)
            {
                // Creating the job here would hide a typo behind a brand-new tile that nobody expects.
                result.SkippedReason = $"no job named '{monitor.TargetJobName}' exists";
                return result;
            }

            var (status, subject, errorSummary) = await Query(monitor, options.TimeoutSeconds, cancellationToken);
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

        private async Task<(JobExecutionStatus Status, string Subject, string ErrorSummary)> Query(
            LogQueryMonitor monitor,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Get, BuildUrl(monitor));

                var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { LogQueryOptions.Scope }), timeout.Token);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, timeout.Token);
                var body = await response.Content.ReadAsStringAsync(timeout.Token);

                if (!response.IsSuccessStatusCode)
                {
                    // A query that cannot run is a problem in its own right: a 403 means the identity
                    // lost its Monitoring Reader role and this tile has silently stopped watching.
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

        private static string Truncate(string value) =>
            string.IsNullOrEmpty(value) || value.Length <= MaxErrorSummaryLength
                ? value
                : value.Substring(0, MaxErrorSummaryLength);
    }
}
