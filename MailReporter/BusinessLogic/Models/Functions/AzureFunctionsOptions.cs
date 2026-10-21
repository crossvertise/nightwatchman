namespace BusinessLogic.Models.Functions
{
    using System.Collections.Generic;

    using DomainModel;

    /// <summary>The <c>AzureFunctions</c> configuration section.</summary>
    public class AzureFunctionsOptions
    {
        public const string SectionName = "AzureFunctions";

        /// <summary>ARM resource id of the Service Bus namespace the monitored functions read from.</summary>
        public string ServiceBusNamespaceResourceId { get; set; }

        /// <summary>How long nothing may be completed while messages wait before a function counts as stuck.</summary>
        public int StuckAfterMinutes { get; set; } = 30;

        /// <summary>
        /// Messages must already have been waiting this long ago, so that a function that is just
        /// starting up for a new message (a cold start takes up to a minute) does not show as stuck.
        /// </summary>
        public int GraceMinutes { get; set; } = 10;

        public List<FunctionMonitor> Monitors { get; set; } = new List<FunctionMonitor>();
    }
}
