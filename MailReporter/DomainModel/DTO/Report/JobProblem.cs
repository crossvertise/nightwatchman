namespace DomainModel.DTO.Report
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One problem detected for a job: failed, overdue, unknown status or never ran.
    /// </summary>
    public class JobProblem
    {
        public string JobId { get; set; }

        public string JobName { get; set; }

        /// <summary>One of <c>Failed</c>, <c>Overdue</c>, <c>UnknownStatus</c>, <c>NeverRan</c>.</summary>
        public string Reason { get; set; }

        public string LastStatus { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public DateTime? ExpectedByUtc { get; set; }

        public string LastSubject { get; set; }

        /// <summary>
        /// Findings of a polled health endpoint (e.g. the flagged payments), read from the last failed
        /// execution. Empty for mail-driven jobs.
        /// </summary>
        public List<HealthIssue> Issues { get; set; } = new List<HealthIssue>();
    }
}
