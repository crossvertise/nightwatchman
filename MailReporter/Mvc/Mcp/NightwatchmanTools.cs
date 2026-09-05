namespace Mvc.Mcp
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;

    using DomainModel.DTO.Report;

    using ModelContextProtocol.Server;

    /// <summary>
    /// Read-only MCP tools over the Nightwatchman job monitoring data. Services are taken as
    /// method parameters and resolved from DI for each call.
    /// </summary>
    [McpServerToolType]
    public static class NightwatchmanTools
    {
        [McpServerTool(Name = "get_health_summary")]
        [Description("Overall health of all monitored batch jobs: how many are healthy, failed, overdue or never ran, "
            + "plus one entry per problem with the reason (Failed, Overdue, UnknownStatus, NeverRan), the last status, "
            + "the last run time (UTC) and the time the job was expected by. "
            + "Use this FIRST to answer questions like 'are there any problems with Nightwatchman?', "
            + "'what went wrong last night?' or 'is everything fine?'. "
            + "If IsHealthy is false, follow up with get_recent_failures or get_job for the affected jobs.")]
        public static async Task<HealthSummary> GetHealthSummary(
            IJobStatusReportService reportService,
            [Description("Size of the reporting window in hours that follow-up questions should use. Default 24 (last night).")]
            int lookbackHours = 24) =>
            await reportService.GetHealthSummary(lookbackHours);

        [McpServerTool(Name = "list_jobs")]
        [Description("Lists every configured job with its last status, last run (UTC), next expected run and whether it is "
            + "currently overdue. Use this to find out which jobs exist or to get a job id or name for the other tools.")]
        public static async Task<IList<JobSummary>> ListJobs(IJobStatusReportService reportService) =>
            await reportService.GetJobSummaries();

        [McpServerTool(Name = "get_job")]
        [Description("Full detail of a single job: its matching configuration (sender address, subject patterns), the "
            + "expected interval, the current status and its most recent executions. Use this to investigate one job that "
            + "get_health_summary reported as a problem, or to explain how a job is matched.")]
        public static async Task<JobDetail> GetJob(
            IJobStatusReportService reportService,
            [Description("Job id or job name (case-insensitive), as returned by list_jobs or get_health_summary.")]
            string jobIdOrName) =>
            await reportService.GetJobDetail(jobIdOrName);

        [McpServerTool(Name = "get_job_executions")]
        [Description("The most recent executions of one job, newest first, with status, finish time (UTC) and the subject "
            + "of the notification mail. Use this to see the history of a single job, e.g. whether a failure is new or recurring.")]
        public static async Task<IList<ExecutionSummary>> GetJobExecutions(
            IJobStatusReportService reportService,
            [Description("Job id or job name (case-insensitive).")] string jobIdOrName,
            [Description("Maximum number of executions to return. Default 10.")] int limit = 10) =>
            await reportService.GetJobExecutions(jobIdOrName, limit);

        [McpServerTool(Name = "get_recent_executions")]
        [Description("All job executions recorded in the last N hours, newest first, optionally filtered by status. "
            + "Use this for 'what ran last night?' questions or to check a specific status across all jobs.")]
        public static async Task<IList<ExecutionSummary>> GetRecentExecutions(
            IJobStatusReportService reportService,
            [Description("Size of the time window in hours, counted back from now. Default 24.")] int hours = 24,
            [Description("Maximum number of executions to return. Default 100.")] int limit = 100,
            [Description("Optional status filter: Success, Warning, Error or Unknown. Omit for all statuses.")]
            string status = null) =>
            await reportService.GetRecentExecutions(hours, limit, status);

        [McpServerTool(Name = "get_recent_failures")]
        [Description("Only the failed and warning executions of the last N hours, newest first. "
            + "Use this right after get_health_summary reported problems to say what exactly went wrong.")]
        public static async Task<IList<ExecutionSummary>> GetRecentFailures(
            IJobStatusReportService reportService,
            [Description("Size of the time window in hours, counted back from now. Default 24.")] int hours = 24,
            [Description("Maximum number of executions to return. Default 50.")] int limit = 50) =>
            await reportService.GetRecentFailures(hours, limit);

        [McpServerTool(Name = "get_unclassified_executions")]
        [Description("Notification mails that could not be assigned to any configured job (job name 'Unknown'). "
            + "Use this to find monitoring gaps: a mail arriving here means a job pattern is missing or wrong.")]
        public static async Task<IList<ExecutionSummary>> GetUnclassifiedExecutions(
            IJobStatusReportService reportService,
            [Description("Maximum number of executions to return. Default 50.")] int limit = 50) =>
            await reportService.GetUnclassifiedExecutions(limit);
    }
}
