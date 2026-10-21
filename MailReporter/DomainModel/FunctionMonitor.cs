namespace DomainModel
{
    /// <summary>
    /// An Azure Function triggered by Service Bus, watched through the queue or topic subscription
    /// it reads from. Configured under <c>AzureFunctions:Monitors</c>.
    /// </summary>
    /// <remarks>
    /// Unlike a <see cref="Job"/>, a function sends no notification mail, so it is watched from the
    /// outside: messages that wait while nothing is completed mean the function is not running.
    /// </remarks>
    public class FunctionMonitor
    {
        /// <summary>Tile title, e.g. "Mailer".</summary>
        public string Name { get; set; }

        /// <summary>Name of the function app, shown on the tile.</summary>
        public string FunctionApp { get; set; }

        /// <summary>The queue the function reads. Leave empty for a topic subscription.</summary>
        public string Queue { get; set; }

        public string Topic { get; set; }

        public string Subscription { get; set; }

        /// <summary>
        /// ARM resource id of the Service Bus namespace. Empty means
        /// <c>AzureFunctions:ServiceBusNamespaceResourceId</c>.
        /// </summary>
        public string NamespaceResourceId { get; set; }

        /// <summary>The queue or topic: the entity Azure Monitor reports metrics for.</summary>
        public string MetricsEntityName => string.IsNullOrWhiteSpace(Queue) ? Topic : Queue;

        /// <summary>Queue, or topic/subscription, as shown on the tile.</summary>
        public string Source => string.IsNullOrWhiteSpace(Queue) ? $"{Topic}/{Subscription}" : Queue;
    }
}
