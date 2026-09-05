namespace DomainModel.DTO.Report
{
    using System;

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
    }
}
