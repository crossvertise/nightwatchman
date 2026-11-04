using Azure.Core;

using BusinessLogic.Implementations;
using BusinessLogic.Interfaces;

using DomainModel;

using Microsoft.Extensions.Configuration;

using Moq;

using NUnit.Framework;

using Repos;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    /// <summary>
    /// Polling an endpoint that reports its own health, and turning that into a job execution.
    /// </summary>
    [TestFixture]
    public class HttpHealthPollerTests
    {
        private const string JobName = "Payment Health Live";
        private const string JobId = "job-1";

        private Mock<IJobRepo> _jobRepo = null!;
        private Mock<IJobExecutionService> _jobExecutionService = null!;
        private JobExecution? _created;

        [SetUp]
        public void Setup()
        {
            _created = null;

            _jobRepo = new Mock<IJobRepo>();
            _jobRepo.Setup(r => r.GetAll()).ReturnsAsync(new List<Job> { new Job { Id = JobId, Name = JobName } });

            _jobExecutionService = new Mock<IJobExecutionService>();
            _jobExecutionService.Setup(s => s.Create(It.IsAny<JobExecution>()))
                .Callback<JobExecution>(e => _created = e)
                .Returns(Task.CompletedTask);
        }

        // ---------- parsing ----------

        [Test]
        public void Reads_an_enveloped_response()
        {
            // Xv.Api wraps every controller response in { meta, data }; reading only the root would
            // report zero errors from a body that is reporting several.
            var result = HttpHealthPoller.ParseResponse(@"{""meta"":{""whitelabel"":""Xv""},""data"":{""errors"":3,""warnings"":1,""message"":""3 x stuck""}}");

            Assert.Multiple(() =>
            {
                Assert.That(result.Errors, Is.EqualTo(3));
                Assert.That(result.Warnings, Is.EqualTo(1));
                Assert.That(result.Message, Is.EqualTo("3 x stuck"));
            });
        }

        [Test]
        public void Reads_a_bare_response()
        {
            var result = HttpHealthPoller.ParseResponse(@"{""errors"":0,""warnings"":0,""message"":""all good""}");

            Assert.That(result.Errors, Is.Zero);
            Assert.That(result.Message, Is.EqualTo("all good"));
        }

        [Test]
        public void Is_not_fooled_by_casing()
        {
            var result = HttpHealthPoller.ParseResponse(@"{""Data"":{""Errors"":2,""Message"":""two""}}");

            Assert.That(result.Errors, Is.EqualTo(2));
            Assert.That(result.Message, Is.EqualTo("two"));
        }

        [Test]
        public void Treats_missing_numbers_as_healthy_rather_than_throwing()
        {
            // A body without the fields is not evidence of a problem, and inventing errors here would
            // turn the tile red for no reason.
            var result = HttpHealthPoller.ParseResponse(@"{""data"":{}}");

            Assert.That(result.Errors, Is.Zero);
            Assert.That(result.Message, Is.Null);
        }

        // ---------- polling ----------

        [Test]
        public async Task A_healthy_endpoint_records_a_successful_execution()
        {
            var results = await Poll(HttpStatusCode.OK, @"{""data"":{""errors"":0,""message"":""Payment health OK""}}");

            Assert.That(results[0].Recorded, Is.True);
            Assert.That(_created, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(_created!.Status, Is.EqualTo(JobExecutionStatus.Success));
                Assert.That(_created.JobId, Is.EqualTo(JobId));
                Assert.That(_created.JobName, Is.EqualTo(JobName));
                Assert.That(_created.OriginalSubject, Is.EqualTo("Payment health OK"));
            });
        }

        [Test]
        public async Task An_endpoint_reporting_errors_records_a_failed_execution()
        {
            var results = await Poll(HttpStatusCode.OK, @"{""data"":{""errors"":2,""message"":""2 x authorized but order still Draft""}}");

            Assert.That(results[0].Status, Is.EqualTo(JobExecutionStatus.Error));
            Assert.That(_created!.OriginalSubject, Is.EqualTo("2 x authorized but order still Draft"));
        }

        [Test]
        public async Task An_endpoint_that_refuses_is_itself_a_problem()
        {
            // A monitor that cannot be read is not the same as a monitor reporting health, so a 500 or
            // a 401 has to turn the tile red rather than pass unnoticed.
            var results = await Poll(HttpStatusCode.InternalServerError, "boom");

            Assert.That(results[0].Status, Is.EqualTo(JobExecutionStatus.Error));
            Assert.That(_created!.OriginalSubject, Does.Contain("HTTP 500"));
        }

        [Test]
        public async Task A_monitor_pointing_at_a_job_that_does_not_exist_records_nothing()
        {
            // Creating the job here would hide a typo behind a tile nobody expects.
            var results = await Poll(HttpStatusCode.OK, @"{""errors"":0}", jobName: "No Such Job");

            Assert.That(results[0].Recorded, Is.False);
            Assert.That(results[0].SkippedReason, Does.Contain("No Such Job"));
            _jobExecutionService.Verify(s => s.Create(It.IsAny<JobExecution>()), Times.Never);
        }

        // ---------- helpers ----------

        private async Task<IReadOnlyList<Models.Http.HttpHealthPollResult>> Poll(
            HttpStatusCode status,
            string body,
            string jobName = JobName)
        {
            var handler = new StubHandler(status, body);
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HttpHealth:TimeoutSeconds"] = "5",
                    ["HttpHealth:Monitors:0:Name"] = "Payment Health Live",
                    ["HttpHealth:Monitors:0:JobName"] = jobName,
                    ["HttpHealth:Monitors:0:Url"] = "https://example.invalid/v1/internal/monitoring/payment-health",
                    ["HttpHealth:Monitors:0:Scope"] = "api://example/.default",
                })
                .Build();

            var poller = new HttpHealthPoller(
                factory.Object,
                new StubCredential(),
                configuration,
                _jobRepo.Object,
                _jobExecutionService.Object,
                TimeProvider.System);

            return await poller.PollAll(CancellationToken.None);
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;

            public StubHandler(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }

        private sealed class StubCredential : TokenCredential
        {
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
                new AccessToken("stub-token", DateTimeOffset.UtcNow.AddHours(1));

            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
                new ValueTask<AccessToken>(GetToken(requestContext, cancellationToken));
        }
    }
}
