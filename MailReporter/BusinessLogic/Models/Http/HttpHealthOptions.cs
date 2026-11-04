namespace BusinessLogic.Models.Http
{
    using System.Collections.Generic;

    using DomainModel;

    /// <summary>The <c>HttpHealth</c> configuration section.</summary>
    public class HttpHealthOptions
    {
        public const string SectionName = "HttpHealth";

        /// <summary>How long to wait for one endpoint before the poll counts as failed.</summary>
        public int TimeoutSeconds { get; set; } = 20;

        public List<HttpHealthMonitor> Monitors { get; set; } = new List<HttpHealthMonitor>();
    }
}
