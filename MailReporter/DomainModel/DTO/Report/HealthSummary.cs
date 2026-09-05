namespace DomainModel.DTO.Report
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Overall health of all monitored jobs — the summary-first answer to
    /// "are there any problems with Nightwatchman?".
    /// </summary>
    public class HealthSummary
    {
        public DateTime GeneratedAtUtc { get; set; }

        public int LookbackHours { get; set; }

        public int TotalJobs { get; set; }

        public int HealthyCount { get; set; }

        public int FailedCount { get; set; }

        public int OverdueCount { get; set; }

        public int UnknownCount { get; set; }

        public int NeverRanCount { get; set; }

        public bool IsHealthy { get; set; }

        public List<JobProblem> Problems { get; set; } = new List<JobProblem>();
    }
}
