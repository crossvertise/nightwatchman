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
    /// Turning a Log Analytics query into a tile. This is the monitor that sees what the payment
    /// sweep cannot: a request that failed before it ever wrote a payment row.
    /// </summary>
    [TestFixture]
    public class LogQueryPollerTests
    {
        private const string JobName = "Checkout API Errors Live";
        private const string JobId = "job-9";

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
        public void Reads_the_first_row_of_the_primary_table()
        {
            var result = LogQueryPoller.ParseResponse(Table(
                @"{""name"":""errors"",""type"":""long""},{""name"":""warnings"",""type"":""long""},{""name"":""message"",""type"":""string""}",
                @"[7,0,""7 x 5xx on checkout routes: POST Payment/CreatePaymentIntent""]"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Errors, Is.EqualTo(7));
                Assert.That(result.Warnings, Is.Zero);
                Assert.That(result.Message, Does.Contain("CreatePaymentIntent"));
            });
        }

        [Test]
        public void Finds_the_columns_by_name_not_by_position()
        {
            // The query decides what it projects and in which order; binding to column 0 would make
            // an innocent edit to the KQL silently report the wrong number.
            var result = LogQueryPoller.ParseResponse(Table(
                @"{""name"":""message"",""type"":""string""},{""name"":""Errors"",""type"":""long""}",
                @"[""two failures"",2]"));

            Assert.That(result.Errors, Is.EqualTo(2));
            Assert.That(result.Message, Is.EqualTo("two failures"));
        }

        [Test]
        public void Treats_an_empty_result_as_healthy()
        {
            // A query that filters for errors returns nothing exactly when there are none. Reading
            // that as "unknown" would turn every quiet poll into a red tile.
            var result = LogQueryPoller.ParseResponse(@"{""tables"":[{""name"":""PrimaryResult"",""columns"":[{""name"":""errors""}],""rows"":[]}]}");

            Assert.That(result.Errors, Is.Zero);
        }

        [Test]
        public void Treats_a_null_count_as_zero()
        {
            // sum() over no rows is null in KQL, and null is not evidence of a problem.
            var result = LogQueryPoller.ParseResponse(Table(@"{""name"":""errors""},{""name"":""message""}", @"[null,""nothing""]"));

            Assert.That(result.Errors, Is.Zero);
            Assert.That(result.Message, Is.EqualTo("nothing"));
        }

        // ---------- url ----------

        [Test]
        public void Builds_a_query_url_that_escapes_the_KQL()
        {
            var url = LogQueryPoller.BuildUrl(new LogQueryMonitor
            {
                WorkspaceId = "ws-1",
                Timespan = "PT30M",
                Query = "AppRequests | where ResultCode == \"500\"",
            });

            Assert.Multiple(() =>
            {
                Assert.That(url, Does.StartWith("https://api.loganalytics.io/v1/workspaces/ws-1/query"));
                Assert.That(url, Does.Contain("timespan=PT30M"));
                Assert.That(url, Does.Not.Contain(" "), "an unescaped pipe or space makes the request a 400");
                Assert.That(url, Does.Not.Contain("\""));
            });
        }

        // ---------- polling ----------

        [Test]
        public async Task A_quiet_window_records_a_successful_execution()
        {
            var results = await Run(HttpStatusCode.OK, Table(@"{""name"":""errors""},{""name"":""message""}", @"[0,""No checkout 5xx in the window""]"));

            Assert.That(results[0].Recorded, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(_created!.Status, Is.EqualTo(JobExecutionStatus.Success));
                Assert.That(_created.JobId, Is.EqualTo(JobId));
                Assert.That(_created.OriginalSubject, Is.EqualTo("No checkout 5xx in the window"));
            });
        }

        [Test]
        public async Task A_window_with_server_errors_records_a_failed_execution()
        {
            var results = await Run(HttpStatusCode.OK, Table(@"{""name"":""errors""},{""name"":""message""}", @"[2,""2 x 5xx on checkout routes""]"));

            Assert.That(results[0].Status, Is.EqualTo(JobExecutionStatus.Error));
            Assert.That(_created!.OriginalSubject, Is.EqualTo("2 x 5xx on checkout routes"));
        }

        [Test]
        public async Task A_query_that_is_refused_turns_the_tile_red()
        {
            // 403 means the identity lost Monitoring Reader, so this tile stopped watching. Passing
            // that off as health would leave us believing in a monitor that is no longer running.
            var results = await Run(HttpStatusCode.Forbidden, "no access");

            Assert.That(results[0].Status, Is.EqualTo(JobExecutionStatus.Error));
            Assert.That(_created!.OriginalSubject, Does.Contain("HTTP 403"));
        }

        [Test]
        public async Task A_monitor_pointing_at_a_job_that_does_not_exist_records_nothing()
        {
            var results = await Run(HttpStatusCode.OK, Table(@"{""name"":""errors""}", "[0]"), jobName: "No Such Job");

            Assert.That(results[0].Recorded, Is.False);
            Assert.That(results[0].SkippedReason, Does.Contain("No Such Job"));
            _jobExecutionService.Verify(s => s.Create(It.IsAny<JobExecution>()), Times.Never);
        }

        // ---------- helpers ----------

        private static string Table(string columns, string row) =>
            $@"{{""tables"":[{{""name"":""PrimaryResult"",""columns"":[{columns}],""rows"":[{row}]}}]}}";

        private async Task<IReadOnlyList<Models.Http.HttpHealthPollResult>> Run(
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
                    ["LogQueryHealth:TimeoutSeconds"] = "5",
                    ["LogQueryHealth:Monitors:0:Name"] = JobName,
                    ["LogQueryHealth:Monitors:0:JobName"] = jobName,
                    ["LogQueryHealth:Monitors:0:WorkspaceId"] = "869913e8-135b-4cb7-b45f-69126c020a9f",
                    ["LogQueryHealth:Monitors:0:Query"] = "AppRequests | take 1",
                })
                .Build();

            var poller = new LogQueryPoller(
                factory.Object,
                new StubCredential(),
                configuration,
                _jobRepo.Object,
                _jobExecutionService.Object,
                TimeProvider.System);

            return await poller.RunAll(CancellationToken.None);
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
