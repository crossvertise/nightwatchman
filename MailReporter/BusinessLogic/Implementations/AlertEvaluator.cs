namespace BusinessLogic.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.Alerting;
    using BusinessLogic.Models.Http;

    using DomainModel.Alerting;

    using Microsoft.Extensions.Configuration;

    using Repos;

    /// <inheritdoc cref="IAlertEvaluator"/>
    /// <remarks>
    /// Edge-triggered: a problem is announced once, stays quiet while it persists, and is announced
    /// again if it returns after recovering. The memory for that lives in <see cref="AlertState"/>,
    /// one row per broken tile, deleted on recovery.
    /// <para>
    /// Both tile kinds are covered — mail/poll-driven jobs through <see cref="IJobStatusReportService"/>
    /// and Service Bus functions through <see cref="IFunctionHealthService"/>. The latter are invisible
    /// to the MCP tools, so this is the only place they are watched for anyone.
    /// </para>
    /// </remarks>
    public class AlertEvaluator : IAlertEvaluator
    {
        public const string JobKeyPrefix = "job:";
        public const string FunctionKeyPrefix = "function:";
        public const string StuckReason = "Stuck";

        private readonly IJobStatusReportService _reportService;
        private readonly IFunctionHealthService _functionHealthService;
        private readonly IAlertStateRepo _alertStateRepo;
        private readonly IAlertNotifier _notifier;
        private readonly IConfiguration _configuration;
        private readonly TimeProvider _timeProvider;

        public AlertEvaluator(
            IJobStatusReportService reportService,
            IFunctionHealthService functionHealthService,
            IAlertStateRepo alertStateRepo,
            IAlertNotifier notifier,
            IConfiguration configuration,
            TimeProvider timeProvider)
        {
            _reportService = reportService;
            _functionHealthService = functionHealthService;
            _alertStateRepo = alertStateRepo;
            _notifier = notifier;
            _configuration = configuration;
            _timeProvider = timeProvider;
        }

        public async Task<AlertRunSummary> Run(CancellationToken cancellationToken)
        {
            var options = _configuration.GetSection(AlertingOptions.SectionName).Get<AlertingOptions>() ?? new AlertingOptions();
            var summary = new AlertRunSummary();

            if (!options.Enabled)
            {
                return summary;
            }

            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var problems = await CollectProblems(options);
            var stored = (await _alertStateRepo.GetAll()).ToList();

            summary.Problems = problems.Count;

            await AnnounceNewProblems(problems, stored, options, nowUtc, summary, cancellationToken);
            await AnnounceRecoveries(problems, stored, nowUtc, summary, cancellationToken);

            return summary;
        }

        /// <summary>Every broken tile, both kinds, in one shape.</summary>
        internal async Task<List<MonitorProblem>> CollectProblems(AlertingOptions options)
        {
            var immediate = new HashSet<string>(options.ImmediateJobs ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var problems = new List<MonitorProblem>();

            var httpHealth = _configuration.GetSection(HttpHealthOptions.SectionName).Get<HttpHealthOptions>() ?? new HttpHealthOptions();

            var health = await _reportService.GetHealthSummary();
            foreach (var problem in health.Problems ?? new List<DomainModel.DTO.Report.JobProblem>())
            {
                problems.Add(new MonitorProblem
                {
                    Key = JobKeyPrefix + problem.JobName,
                    DisplayName = problem.JobName,
                    Reason = problem.Reason,
                    Detail = problem.LastSubject,
                    Issues = problem.Issues ?? new List<DomainModel.DTO.Report.HealthIssue>(),
                    Host = HealthIssueReader.HostFor(httpHealth, problem.JobName),
                    Immediate = immediate.Contains(problem.JobName),
                });
            }

            var functions = await _functionHealthService.GetOverview();
            foreach (var function in functions.Where(f => f.IsStuck))
            {
                // Only IsStuck, not Error: a transient failure to read Service Bus says nothing about
                // the function, and alerting on it would cry wolf every time ARM hiccups.
                problems.Add(new MonitorProblem
                {
                    Key = FunctionKeyPrefix + function.Monitor.Name,
                    DisplayName = function.Monitor.Name,
                    Reason = StuckReason,
                    Detail = $"{function.WaitingMessages} waiting, {function.CompletedMessages} completed in {function.WindowMinutes} min ({function.Monitor.Source})",
                    Immediate = true,
                });
            }

            return problems;
        }

        private async Task AnnounceNewProblems(
            List<MonitorProblem> problems,
            List<AlertState> stored,
            AlertingOptions options,
            DateTime nowUtc,
            AlertRunSummary summary,
            CancellationToken cancellationToken)
        {
            foreach (var problem in problems)
            {
                var state = stored.FirstOrDefault(s => string.Equals(s.Key, problem.Key, StringComparison.Ordinal));

                if (state == null)
                {
                    state = new AlertState
                    {
                        Key = problem.Key,
                        DisplayName = problem.DisplayName,
                        Reason = problem.Reason,
                        Detail = problem.Detail,
                        FirstSeenUtc = nowUtc,
                    };

                    await _alertStateRepo.Create(state);
                }
                else if (!string.Equals(state.Reason, problem.Reason, StringComparison.Ordinal))
                {
                    // Keep the latest reason, but do not re-announce: a job sliding from Failed to
                    // Overdue is the same incident, and saying so twice adds noise, not information.
                    state.Reason = problem.Reason;
                    state.Detail = problem.Detail;
                    await _alertStateRepo.Update(state);
                }

                if (state.AlertedAtUtc != null)
                {
                    continue;
                }

                if (!MayAnnounce(problem, options, nowUtc))
                {
                    summary.Deferred++;
                    continue;
                }

                if (await _notifier.Send(FormatProblem(problem, options), cancellationToken))
                {
                    state.AlertedAtUtc = nowUtc;
                    await _alertStateRepo.Update(state);
                    summary.Alerted++;
                }
                else
                {
                    summary.FailedToSend++;
                }
            }
        }

        private async Task AnnounceRecoveries(
            List<MonitorProblem> problems,
            List<AlertState> stored,
            DateTime nowUtc,
            AlertRunSummary summary,
            CancellationToken cancellationToken)
        {
            var current = new HashSet<string>(problems.Select(p => p.Key), StringComparer.Ordinal);

            foreach (var state in stored.Where(s => !current.Contains(s.Key)))
            {
                if (state.AlertedAtUtc == null)
                {
                    // Never announced — typically a batch job that broke and recovered overnight.
                    // Nobody saw it, so there is nothing to take back.
                    await _alertStateRepo.Delete(state.Id);
                    continue;
                }

                if (await _notifier.Send(FormatRecovery(state, nowUtc), cancellationToken))
                {
                    await _alertStateRepo.Delete(state.Id);
                    summary.Recovered++;
                }
                else
                {
                    // Keep the row so the next tick retries; dropping it would both lose the
                    // "all clear" and re-announce the problem if it came back.
                    summary.FailedToSend++;
                }
            }
        }

        /// <summary>
        /// Payments and queues are announced at any hour. Batch jobs wait for working hours, so an ETL
        /// that dies at 02:00 is reported at the start of the next working day rather than at night.
        /// </summary>
        internal bool MayAnnounce(MonitorProblem problem, AlertingOptions options, DateTime nowUtc)
        {
            if (problem.Immediate)
            {
                return true;
            }

            var localHour = ToLocal(nowUtc, options.TimeZone).Hour;
            return localHour >= options.BatchReleaseHour && localHour < options.BatchQuietHour;
        }

        private static DateTime ToLocal(DateTime utc, string timeZoneId)
        {
            try
            {
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
            }
            catch (Exception)
            {
                // An unknown zone id must not stop alerting; UTC is close enough to decide an hour.
                return utc;
            }
        }

        internal static string FormatProblem(MonitorProblem problem, AlertingOptions options)
        {
            var text = $"🔴 **{problem.DisplayName}** — {problem.Reason}";

            if (!string.IsNullOrWhiteSpace(problem.Detail))
            {
                text += $"{Environment.NewLine}{problem.Detail}";
            }

            var issues = problem.Issues ?? new List<DomainModel.DTO.Report.HealthIssue>();
            foreach (var issue in issues.Take(HealthIssueReader.MaxLines))
            {
                text += $"{Environment.NewLine}• {HealthIssueReader.Describe(issue, problem.Host, markdown: true)}";
            }

            if (issues.Count > HealthIssueReader.MaxLines)
            {
                text += $"{Environment.NewLine}… and {issues.Count - HealthIssueReader.MaxLines} more";
            }

            return Append(text, options);
        }

        internal static string FormatRecovery(AlertState state, DateTime nowUtc)
        {
            var broken = nowUtc - state.FirstSeenUtc;
            var duration = broken < TimeSpan.FromMinutes(1)
                ? "less than a minute"
                : $"{(int)broken.TotalHours} h {broken.Minutes} min";

            return string.Format(
                CultureInfo.InvariantCulture,
                "✅ **{0}** is green again (was {1} for {2})",
                state.DisplayName,
                state.Reason,
                duration);
        }

        private static string Append(string text, AlertingOptions options) =>
            string.IsNullOrWhiteSpace(options.DashboardUrl)
                ? text
                : $"{text}{Environment.NewLine}{options.DashboardUrl}";
    }
}
