namespace BusinessLogic.Models.Alerting
{
    using System.Collections.Generic;

    using DomainModel.DTO.Report;

    /// <summary>
    /// One broken tile, normalised so job tiles and Service Bus function tiles can be alerted on by
    /// the same code.
    /// </summary>
    public class MonitorProblem
    {
        /// <summary>Stable key, e.g. <c>job:Payment Health Live</c> or <c>function:Mailer</c>.</summary>
        public string Key { get; set; }

        public string DisplayName { get; set; }

        /// <summary>Failed, Overdue, UnknownStatus, NeverRan, or Stuck for a function.</summary>
        public string Reason { get; set; }

        /// <summary>Last subject line or equivalent context.</summary>
        public string Detail { get; set; }

        /// <summary>Findings of a polled health endpoint, one alert line each.</summary>
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();

        /// <summary>Polled host (e.g. <c>https://staging.crossvertise.com</c>) for order links; null otherwise.</summary>
        public string Host { get; set; }

        /// <summary>
        /// True for payments and queues, which are announced around the clock. False for nightly
        /// batch jobs, whose problems wait for working hours.
        /// </summary>
        public bool Immediate { get; set; }
    }
}
