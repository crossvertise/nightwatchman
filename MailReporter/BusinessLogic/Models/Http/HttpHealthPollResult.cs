namespace BusinessLogic.Models.Http
{
    using DomainModel;

    /// <summary>What one poll did, so the caller can log it without re-deriving anything.</summary>
    public class HttpHealthPollResult
    {
        public string MonitorName { get; set; }

        public string JobName { get; set; }

        /// <summary>False when nothing was written; <see cref="SkippedReason"/> then says why.</summary>
        public bool Recorded { get; set; }

        public JobExecutionStatus Status { get; set; }

        /// <summary>The subject the execution was recorded with.</summary>
        public string Subject { get; set; }

        /// <summary>Why no execution was written, or <c>null</c> when one was.</summary>
        public string SkippedReason { get; set; }
    }
}
