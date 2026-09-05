namespace DomainModel.DTO.Report
{
    using System;

    /// <summary>
    /// Short status line for one configured job.
    /// </summary>
    public class JobSummary
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string LastStatus { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public DateTime? NextRunUtc { get; set; }

        public bool IsDue { get; set; }

        public string ExpectedInterval { get; set; }
    }
}
