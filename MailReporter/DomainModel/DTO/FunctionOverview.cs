namespace DomainModel.DTO
{
    /// <summary>The state of one <see cref="FunctionMonitor"/> for the dashboard.</summary>
    public class FunctionOverview
    {
        public FunctionMonitor Monitor { get; set; }

        /// <summary>Messages waiting in the queue or subscription right now.</summary>
        public long? WaitingMessages { get; set; }

        public long? DeadLetteredMessages { get; set; }

        /// <summary>Messages completed on the queue or topic within <see cref="WindowMinutes"/>.</summary>
        public long? CompletedMessages { get; set; }

        public int WindowMinutes { get; set; }

        /// <summary>
        /// Messages are waiting, were already waiting before the grace period, and nothing was
        /// completed within the window: the function is not running.
        /// </summary>
        public bool IsStuck { get; set; }

        /// <summary>Why the state could not be read; <c>null</c> when it could.</summary>
        public string Error { get; set; }
    }
}
