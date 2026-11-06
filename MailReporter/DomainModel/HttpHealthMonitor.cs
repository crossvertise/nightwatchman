namespace DomainModel
{
    /// <summary>
    /// An HTTP endpoint that reports its own health, polled on a timer. Configured under
    /// <c>HttpHealth:Monitors</c>.
    /// </summary>
    /// <remarks>
    /// The third way a tile gets its state, next to notification mails (<see cref="Job"/>) and the
    /// Service Bus reads behind <see cref="FunctionMonitor"/>. Unlike a function tile, each poll is
    /// written as a <see cref="JobExecution"/> against <see cref="JobName"/>, so the result shows up
    /// on the dashboard, in the PRTG feed and over MCP exactly like a mail-driven job — including
    /// "overdue" when the polling itself stops.
    /// </remarks>
    public class HttpHealthMonitor
    {
        /// <summary>Label used in logs and alerts, e.g. "Payment Health Live".</summary>
        public string Name { get; set; }

        /// <summary>
        /// Name of the <see cref="Job"/> the result is recorded against. The job must already exist;
        /// its <c>ExpectedInterval</c> is what turns a stalled poller into an overdue tile. Empty
        /// falls back to <see cref="Name"/>.
        /// </summary>
        public string JobName { get; set; }

        /// <summary>Absolute URL to GET.</summary>
        public string Url { get; set; }

        /// <summary>
        /// Entra scope to acquire a token for, e.g. <c>api://&lt;app-id&gt;/.default</c>. Empty polls
        /// anonymously, which is only appropriate for an endpoint that is public anyway.
        /// </summary>
        public string Scope { get; set; }

        /// <summary>The job this monitor writes to.</summary>
        public string TargetJobName => string.IsNullOrWhiteSpace(JobName) ? Name : JobName;
    }
}
