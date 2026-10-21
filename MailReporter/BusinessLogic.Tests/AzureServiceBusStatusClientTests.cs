using Azure.Core;

using BusinessLogic.Implementations;

using DomainModel;

using Moq;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    [TestFixture]
    public class AzureServiceBusStatusClientTests
    {
        private const string Namespace = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns";
        private static readonly DateTime From = new DateTime(2026, 10, 21, 11, 30, 0, DateTimeKind.Utc);
        private static readonly DateTime EarlierUntil = new DateTime(2026, 10, 21, 11, 50, 0, DateTimeKind.Utc);
        private static readonly DateTime To = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        private const string EntityJson =
            "{ \"properties\": { \"countDetails\": { \"activeMessageCount\": 11, \"deadLetterMessageCount\": 126, \"scheduledMessageCount\": 3 } } }";

        // ActiveMessages: 5 is the peak up to EarlierUntil; 9 at 11:55 lies inside the grace period.
        private const string MetricsJson = @"{ ""value"": [
            { ""name"": { ""value"": ""ActiveMessages"" }, ""timeseries"": [ { ""data"": [
                { ""timeStamp"": ""2026-10-21T11:30:00Z"", ""maximum"": 3.0 },
                { ""timeStamp"": ""2026-10-21T11:49:00Z"", ""maximum"": 5.0 },
                { ""timeStamp"": ""2026-10-21T11:55:00Z"", ""maximum"": 9.0 } ] } ] },
            { ""name"": { ""value"": ""CompleteMessage"" }, ""timeseries"": [ { ""data"": [
                { ""timeStamp"": ""2026-10-21T11:31:00Z"", ""total"": 2.0 },
                { ""timeStamp"": ""2026-10-21T11:40:00Z"", ""total"": 3.0 },
                { ""timeStamp"": ""2026-10-21T11:41:00Z"" } ] } ] } ] }";

        private FakeHandler _handler = null!;

        [SetUp]
        public void Setup()
        {
            _handler = new FakeHandler();
        }

        [Test]
        public async Task Reads_the_counts_of_a_queue_and_its_metrics()
        {
            _handler.Respond("/queues/xv-mailer-queue?", HttpStatusCode.OK, EntityJson);
            _handler.Respond("/providers/Microsoft.Insights/metrics?", HttpStatusCode.OK, MetricsJson);

            var status = await CreateClient().GetStatus(Namespace, Queue("xv-mailer-queue"), From, EarlierUntil, To);

            Assert.That(status.ActiveMessages, Is.EqualTo(11));
            Assert.That(status.DeadLetterMessages, Is.EqualTo(126));
            Assert.That(status.CompletedMessages, Is.EqualTo(5));
            Assert.That(status.ActiveMessagesEarlier, Is.EqualTo(5), "the peak inside the grace period must not count");
        }

        [Test]
        public async Task Sends_the_token_and_asks_for_the_queue_and_its_metrics()
        {
            _handler.Respond("/queues/", HttpStatusCode.OK, EntityJson);
            _handler.Respond("/metrics?", HttpStatusCode.OK, MetricsJson);

            await CreateClient().GetStatus(Namespace, Queue("xv-mailer-queue"), From, EarlierUntil, To);

            Assert.That(_handler.Requests, Has.Count.EqualTo(2));
            Assert.That(_handler.Requests, Has.All.Matches<HttpRequestMessage>(r => r.Headers.Authorization!.ToString() == "Bearer test-token"));
            Assert.That(_handler.Requests[0].RequestUri!.ToString(), Is.EqualTo(
                "https://management.azure.com" + Namespace + "/queues/xv-mailer-queue?api-version=2021-11-01"));

            var metrics = Uri.UnescapeDataString(_handler.Requests[1].RequestUri!.ToString());
            Assert.That(metrics, Does.StartWith("https://management.azure.com" + Namespace + "/providers/Microsoft.Insights/metrics?api-version=2023-10-01"));
            Assert.That(metrics, Does.Contain("metricnames=ActiveMessages,CompleteMessage"));
            Assert.That(metrics, Does.Contain("timespan=2026-10-21T11:30:00Z/2026-10-21T12:00:00Z"));
            Assert.That(metrics, Does.Contain("$filter=EntityName eq 'xv-mailer-queue'"));
        }

        /// <summary>
        /// Counts are per subscription, but Azure Monitor only reports metrics per topic.
        /// </summary>
        [Test]
        public async Task Asks_for_the_subscription_counts_and_the_topic_metrics()
        {
            _handler.Respond("/topics/domain-events/subscriptions/domain-events-audit?", HttpStatusCode.OK, EntityJson);
            _handler.Respond("/metrics?", HttpStatusCode.OK, MetricsJson);

            var monitor = new FunctionMonitor { Name = "Audit", Topic = "domain-events", Subscription = "domain-events-audit" };
            var status = await CreateClient().GetStatus(Namespace, monitor, From, EarlierUntil, To);

            Assert.That(status.ActiveMessages, Is.EqualTo(11));
            Assert.That(Uri.UnescapeDataString(_handler.Requests[1].RequestUri!.ToString()), Does.Contain("$filter=EntityName eq 'domain-events'"));
        }

        [Test]
        public async Task A_queue_without_metrics_has_nothing_completed_and_nothing_waiting_earlier()
        {
            _handler.Respond("/queues/", HttpStatusCode.OK, EntityJson);
            _handler.Respond("/metrics?", HttpStatusCode.OK, "{ \"value\": [] }");

            var status = await CreateClient().GetStatus(Namespace, Queue("xv-adops-report-notifier"), From, EarlierUntil, To);

            Assert.That(status.CompletedMessages, Is.EqualTo(0));
            Assert.That(status.ActiveMessagesEarlier, Is.EqualTo(0));
        }

        [Test]
        public void A_refusal_from_Azure_names_the_status_and_Azures_reason()
        {
            _handler.Respond("/queues/", HttpStatusCode.Forbidden,
                "{ \"error\": { \"code\": \"AuthorizationFailed\", \"message\": \"The client does not have authorization to perform action.\" } }");

            var ex = Assert.ThrowsAsync<HttpRequestException>(() => CreateClient().GetStatus(Namespace, Queue("xv-mailer-queue"), From, EarlierUntil, To));

            Assert.That(ex!.Message, Does.Contain("403"));
            Assert.That(ex.Message, Does.Contain("does not have authorization"));
        }

        [Test]
        public void A_refusal_without_a_json_body_still_names_the_status()
        {
            _handler.Respond("/queues/", HttpStatusCode.BadGateway, "<html>Bad Gateway</html>");

            var ex = Assert.ThrowsAsync<HttpRequestException>(() => CreateClient().GetStatus(Namespace, Queue("xv-mailer-queue"), From, EarlierUntil, To));

            Assert.That(ex!.Message, Does.Contain("502"));
        }

        private static FunctionMonitor Queue(string name) => new FunctionMonitor { Name = name, Queue = name };

        private AzureServiceBusStatusClient CreateClient()
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(_handler, disposeHandler: false));

            var credential = new Mock<TokenCredential>();
            credential.Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AccessToken("test-token", DateTimeOffset.MaxValue));

            return new AzureServiceBusStatusClient(factory.Object, credential.Object);
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly List<(string UrlPart, HttpStatusCode Status, string Body)> _responses = new();

            public List<HttpRequestMessage> Requests { get; } = new();

            public void Respond(string urlPart, HttpStatusCode status, string body) => _responses.Add((urlPart, status, body));

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                var url = request.RequestUri!.ToString();
                foreach (var (urlPart, status, body) in _responses)
                {
                    if (url.Contains(urlPart, StringComparison.Ordinal))
                    {
                        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
            }
        }
    }
}
