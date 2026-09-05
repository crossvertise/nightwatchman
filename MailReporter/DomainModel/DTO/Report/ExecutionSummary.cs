namespace DomainModel.DTO.Report
{
    using System;

    /// <summary>
    /// A single job execution, reduced to the fields that are useful for reporting.
    /// Deliberately contains no mail body / HTML.
    /// </summary>
    public class ExecutionSummary
    {
        public string Id { get; set; }

        public string JobId { get; set; }

        public string JobName { get; set; }

        public string Status { get; set; }

        public DateTime? StartedUtc { get; set; }

        public DateTime FinishedUtc { get; set; }

        public string Subject { get; set; }

        public string Sender { get; set; }

        public string ErrorSummary { get; set; }
    }
}
