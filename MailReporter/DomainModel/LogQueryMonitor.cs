namespace DomainModel
{
    /// <summary>
    /// A saved Log Analytics query whose result becomes a tile. Configured under
    /// <c>LogQueryHealth:Monitors</c>.
    /// </summary>
    /// <remarks>
    /// This exists because the payment sweep watches <em>payment rows</em>, and a request that dies
    /// with a 500 never produces one. On 12.11. two checkout calls failed with a PayPal 422; the
    /// payment stayed Draft, which the sweep deliberately ignores because every abandoned cart is
    /// Draft, so nothing was reported. An error is a property of the request, not of the payment.
    /// <para>
    /// Querying the telemetry we already write catches any 5xx on any checkout route — including
    /// routes that do not exist yet — without instrumenting each code path or adding a write to the
    /// checkout hot path. Like <see cref="HttpHealthMonitor"/>, each run is recorded as a
    /// <see cref="JobExecution"/>, so the result is an ordinary tile on the dashboard, in the PRTG
    /// feed and over MCP.
    /// </para>
    /// </remarks>
    public class LogQueryMonitor
    {
        /// <summary>Label used in logs and alerts, e.g. "Checkout API Errors Live".</summary>
        public string Name { get; set; }

        /// <summary>
        /// Name of the <see cref="Job"/> the result is recorded against. The job must already exist;
        /// its <c>ExpectedInterval</c> is what turns a stalled poller into an overdue tile. Empty
        /// falls back to <see cref="Name"/>.
        /// </summary>
        public string JobName { get; set; }

        /// <summary>Log Analytics workspace id (a GUID) the query runs against.</summary>
        public string WorkspaceId { get; set; }

        /// <summary>
        /// The KQL. It must project <c>errors</c>, and may project <c>warnings</c> and
        /// <c>message</c>; the first row is read and anything above zero errors turns the tile red.
        /// Let the query decide what counts — that keeps the rule in configuration rather than code.
        /// </summary>
        public string Query { get; set; }

        /// <summary>
        /// ISO-8601 window the query runs over. Keep it comfortably longer than the poll interval:
        /// a window shorter than the gap between polls can let an error fall between two runs
        /// unseen, and the overlap is also what keeps a tile red long enough to be confirmed.
        /// </summary>
        public string Timespan { get; set; } = "PT30M";

        /// <summary>The job this monitor writes to.</summary>
        public string TargetJobName => string.IsNullOrWhiteSpace(JobName) ? Name : JobName;
    }
}
