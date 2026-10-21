namespace BusinessLogic.Models.Functions
{
    /// <summary>Raw numbers for one queue or topic subscription, as read from Azure.</summary>
    public class ServiceBusEntityStatus
    {
        /// <summary>Messages waiting in the queue or subscription now.</summary>
        public long ActiveMessages { get; set; }

        public long DeadLetterMessages { get; set; }

        /// <summary>Messages completed on the queue or topic in the whole window.</summary>
        public long CompletedMessages { get; set; }

        /// <summary>
        /// The highest number of waiting messages from the start of the window until the grace
        /// period. Measured per queue or topic: for a topic it includes every subscription.
        /// </summary>
        public long ActiveMessagesEarlier { get; set; }
    }
}
