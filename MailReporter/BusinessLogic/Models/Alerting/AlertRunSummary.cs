namespace BusinessLogic.Models.Alerting
{
    /// <summary>What one evaluation tick did, for the worker to log.</summary>
    public class AlertRunSummary
    {
        /// <summary>Tiles currently broken.</summary>
        public int Problems { get; set; }

        /// <summary>Problems announced on this tick.</summary>
        public int Alerted { get; set; }

        /// <summary>Tiles that went back to green on this tick.</summary>
        public int Recovered { get; set; }

        /// <summary>Problems held back because it is outside the hours for batch alerts.</summary>
        public int Deferred { get; set; }

        /// <summary>
        /// Problems seen but not yet old enough to announce. A tile that recovers while in this state
        /// is never posted at all, which is the point.
        /// </summary>
        public int AwaitingConfirmation { get; set; }

        /// <summary>Alerts whose delivery failed; they stay unannounced and are retried next tick.</summary>
        public int FailedToSend { get; set; }
    }
}
