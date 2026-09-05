namespace DomainModel.DTO.Report
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Configuration and recent history of a single job.
    /// </summary>
    public class JobDetail
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string EmailSender { get; set; }

        public string SubjectContains { get; set; }

        public string SubjectRegex { get; set; }

        public string SuccessSubjectRegex { get; set; }

        public string ErrorSubjectRegex { get; set; }

        public string ExpectedInterval { get; set; }

        public string LastStatus { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public DateTime? NextRunUtc { get; set; }

        public bool IsDue { get; set; }

        public List<ExecutionSummary> RecentExecutions { get; set; } = new List<ExecutionSummary>();
    }
}
