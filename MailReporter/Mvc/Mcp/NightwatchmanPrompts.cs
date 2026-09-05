namespace Mvc.Mcp
{
    using System.ComponentModel;

    using ModelContextProtocol.Server;

    /// <summary>
    /// MCP prompts that turn the Nightwatchman tools into ready-made questions.
    /// </summary>
    [McpServerPromptType]
    public static class NightwatchmanPrompts
    {
        [McpServerPrompt(Name = "daily_briefing")]
        [Description("Ask Nightwatchman for a short morning briefing about last night's batch jobs.")]
        public static string DailyBriefing(
            [Description("Size of the reporting window in hours. Default 24.")] int lookbackHours = 24) =>
            $"Give me a short Nightwatchman briefing for the last {lookbackHours} hours.\n\n"
            + $"1. Call get_health_summary with lookbackHours={lookbackHours}.\n"
            + "2. If it reports no problems, answer in one sentence that all monitored jobs are healthy, "
            + "naming how many jobs were checked.\n"
            + "3. If there are problems, call get_recent_failures for the same window and, where useful, "
            + "get_job for an affected job, then list each problem as one bullet: job name, what is wrong "
            + "(failed / overdue / unknown status / never ran), when it last ran and the subject of the last "
            + "notification mail.\n"
            + "4. Keep it brief and factual; no speculation about causes that the data does not show.";
    }
}
