namespace BusinessLogic.Models.LogQuery
{
    using System.Collections.Generic;

    using DomainModel;

    /// <summary>The <c>LogQueryHealth</c> configuration section.</summary>
    public class LogQueryOptions
    {
        public const string SectionName = "LogQueryHealth";

        /// <summary>Entra scope for the Log Analytics query API.</summary>
        public const string Scope = "https://api.loganalytics.io/.default";

        /// <summary>
        /// How long to wait for one query. Higher than the HTTP poller's default: a KQL query over a
        /// busy workspace is slower than an endpoint answering from memory.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        public List<LogQueryMonitor> Monitors { get; set; } = new List<LogQueryMonitor>();
    }
}
