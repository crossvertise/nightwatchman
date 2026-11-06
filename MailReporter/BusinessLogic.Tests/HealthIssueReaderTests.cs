using BusinessLogic.Implementations;
using BusinessLogic.Models.Http;

using DomainModel;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;

namespace BusinessLogic.Tests
{
    /// <summary>
    /// Reading the payment-health findings back out of the response body stored on a failed run.
    /// </summary>
    [TestFixture]
    public class HealthIssueReaderTests
    {
        // What the poller stored for the staging test payment on 06.11.2026 (Xv.Api envelope).
        private const string StagingBody =
            "{\"data\":{\"errors\":1,\"warnings\":0,\"message\":\"1 x technical authorization failure\"," +
            "\"details\":[{\"reason\":\"TechnicalFailure\",\"paymentId\":8629,\"orderId\":60172,\"provider\":\"CreditCard\",\"sinceUtc\":\"2026-11-06T18:52:21.197\"}]}," +
            "\"meta\":{\"timeStamp\":\"2026-11-06T18:58:58.1161708Z\",\"culture\":\"de-de\",\"applicationContext\":\"Xv\"," +
            "\"user\":{\"eMail\":\"\",\"timeZoneId\":\"W. Europe Standard Time\",\"roles\":[]}}}";

        [Test]
        public void Reads_every_field_of_a_finding_from_the_envelope()
        {
            var issue = HealthIssueReader.Parse(StagingBody).Single();

            Assert.That(issue.Reason, Is.EqualTo("TechnicalFailure"));
            Assert.That(issue.PaymentId, Is.EqualTo(8629));
            Assert.That(issue.OrderId, Is.EqualTo(60172));
            Assert.That(issue.Provider, Is.EqualTo("CreditCard"));
            Assert.That(issue.SinceUtc, Is.EqualTo(new DateTime(2026, 11, 6, 18, 52, 21, 197, DateTimeKind.Utc)));
            Assert.That(issue.SinceUtc!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void A_body_cut_off_by_the_poller_still_yields_the_complete_findings()
        {
            // The poller keeps at most 2000 characters, so a long list ends mid-object.
            var body = "{\"data\":{\"errors\":3,\"details\":[" +
                "{\"reason\":\"StuckFunds\",\"paymentId\":1,\"orderId\":11,\"provider\":\"PayPal\"}," +
                "{\"reason\":\"CaptureFailure\",\"paymentId\":2,\"orderId\":12,\"provider\":\"CreditCard\"}," +
                "{\"reason\":\"TechnicalFailure\",\"paymentId\":3,\"ord";

            var issues = HealthIssueReader.Parse(body);

            Assert.That(issues.Select(i => i.PaymentId), Is.EqualTo(new int?[] { 1, 2 }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("<html>502 Bad Gateway</html>")]
        [TestCase("System.Net.Http.HttpRequestException: No such host is known.")]
        public void A_body_without_findings_yields_none(string? body)
        {
            Assert.That(HealthIssueReader.Parse(body!), Is.Empty);
        }

        [Test]
        public void Objects_without_a_reason_are_not_findings()
        {
            Assert.That(HealthIssueReader.Parse("{\"user\":{\"eMail\":\"\",\"roles\":[]}}"), Is.Empty);
        }

        [Test]
        public void The_host_comes_from_the_monitor_that_writes_to_the_job()
        {
            var options = new HttpHealthOptions
            {
                Monitors = new List<HttpHealthMonitor>
                {
                    new HttpHealthMonitor { Name = "Payment Health Staging", Url = "https://staging.crossvertise.com/api-fd/v1/internal/monitoring/payment-health" },
                },
            };

            Assert.That(HealthIssueReader.HostFor(options, "payment health staging"), Is.EqualTo("https://staging.crossvertise.com"));
            Assert.That(HealthIssueReader.HostFor(options, "OOH ETL Live"), Is.Null);
        }

        [Test]
        public void Without_a_host_the_order_number_is_plain_text()
        {
            var issue = HealthIssueReader.Parse(StagingBody).Single();

            Assert.That(
                HealthIssueReader.Describe(issue, host: null, markdown: true),
                Is.EqualTo("technical failure · CreditCard · payment 8629 · order 60172 · since 06.11. 18:52 UTC"));
        }
    }
}
