namespace DomainModel.DTO.Report
{
    using System;

    /// <summary>
    /// One finding a health endpoint reported, e.g. one payment the payment-health sweep flagged.
    /// Read back from the response body stored on the failed execution.
    /// </summary>
    public class HealthIssue
    {
        /// <summary>Stable reason code from the endpoint, e.g. <c>TechnicalFailure</c>.</summary>
        public string Reason { get; set; }

        public int? PaymentId { get; set; }

        public int? OrderId { get; set; }

        public string Provider { get; set; }

        public DateTime? SinceUtc { get; set; }
    }
}
