using System.Net;
using System.Net.Http.Headers;
using System.Text;

using DomainModel;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using MongoDB.Driver;

using Newtonsoft.Json.Linq;

namespace Mvc.IntegrationTests
{
    [TestFixture]
    public class McpEndpointTests
    {
        private NightwatchmanAppFactory _factory = null!;

        [OneTimeSetUp]
        public async Task CreateFactoryAndSeedData()
        {
            _factory = new NightwatchmanAppFactory();

            var database = _factory.GetDatabase();
            await database.GetCollection<Job>(nameof(Job)).InsertOneAsync(new Job
            {
                Name = "EtlJob",
                SubjectContains = "ETL",
                ExpectedInterval = TimeSpan.FromHours(24),
            });

            var job = await database.GetCollection<Job>(nameof(Job)).Find(_ => true).FirstAsync();
            await database.GetCollection<JobExecution>(nameof(JobExecution)).InsertManyAsync(new[]
            {
                new JobExecution
                {
                    JobId = job.Id,
                    JobName = job.Name,
                    OriginalSubject = "ETL failed",
                    OriginalBody = "<p>stack trace</p>",
                    Finished = DateTime.UtcNow.AddHours(-2),
                    Status = JobExecutionStatus.Error,
                },
                new JobExecution
                {
                    JobName = "Unknown",
                    OriginalSubject = "Something nobody configured",
                    Finished = DateTime.UtcNow.AddHours(-3),
                    Status = JobExecutionStatus.Unknown,
                },
            });
        }

        [OneTimeTearDown]
        public void DisposeFactory() => _factory.Dispose();

        private async Task<McpClient> CreateMcpClient(string? token = null)
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer " + (token ?? NightwatchmanAppFactory.CreateMcpToken()),
            };

            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri("https://localhost/mcp"),
                    AdditionalHeaders = headers,
                },
                _factory.CreateClient());

            return await McpClient.CreateAsync(transport);
        }

        private async Task<HttpResponseMessage> PostInitialize(string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(
                    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}",
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            if (token != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var client = _factory.CreateClient();
            return await client.SendAsync(request);
        }

        [Test]
        public async Task ListTools_ExposesTheNightwatchmanTools()
        {
            await using var client = await CreateMcpClient();

            var tools = await client.ListToolsAsync();

            Assert.That(tools.Select(t => t.Name), Is.SupersetOf(new[]
            {
                "get_health_summary",
                "list_jobs",
                "get_job",
                "get_job_executions",
                "get_recent_executions",
                "get_recent_failures",
                "get_unclassified_executions",
            }));

            // Descriptions drive tool selection by the model, so they must not be empty.
            Assert.That(tools.All(t => !string.IsNullOrWhiteSpace(t.Description)), Is.True);
        }

        [Test]
        public async Task ListPrompts_ExposesTheDailyBriefingPrompt()
        {
            await using var client = await CreateMcpClient();

            var prompts = await client.ListPromptsAsync();

            Assert.That(prompts.Select(p => p.Name), Does.Contain("daily_briefing"));
        }

        [Test]
        public async Task CallTool_GetHealthSummary_ReportsTheFailedJob()
        {
            await using var client = await CreateMcpClient();

            var result = await client.CallToolAsync("get_health_summary");
            var json = JObject.Parse(TextOf(result));

            Assert.That(result.IsError, Is.Not.True);
            Assert.That(json["isHealthy"]?.Value<bool>(), Is.False);
            Assert.That(json["totalJobs"]?.Value<int>(), Is.EqualTo(1));
            Assert.That(json["failedCount"]?.Value<int>(), Is.EqualTo(1));

            var problem = json["problems"]?.First;
            Assert.That(problem?["jobName"]?.Value<string>(), Is.EqualTo("EtlJob"));
            Assert.That(problem?["reason"]?.Value<string>(), Is.EqualTo("Failed"));
            // Enums must serialise as readable names, not as numbers.
            Assert.That(problem?["lastStatus"]?.Value<string>(), Is.EqualTo("Error"));
        }

        [Test]
        public async Task CallTool_GetRecentFailures_ReturnsTheFailureWithoutTheMailBody()
        {
            await using var client = await CreateMcpClient();

            var result = await client.CallToolAsync("get_recent_failures");
            var text = TextOf(result);

            Assert.That(text, Does.Contain("ETL failed"));
            Assert.That(text, Does.Not.Contain("stack trace"));
        }

        [Test]
        public async Task CallTool_GetJob_ReturnsTheJobConfiguration()
        {
            await using var client = await CreateMcpClient();

            var result = await client.CallToolAsync(
                "get_job",
                new Dictionary<string, object?> { ["jobIdOrName"] = "etljob" });
            var json = JObject.Parse(TextOf(result));

            Assert.That(json["name"]?.Value<string>(), Is.EqualTo("EtlJob"));
            Assert.That(json["subjectContains"]?.Value<string>(), Is.EqualTo("ETL"));
        }

        [Test]
        public async Task CallTool_GetUnclassifiedExecutions_ReturnsTheUnmatchedMail()
        {
            await using var client = await CreateMcpClient();

            var result = await client.CallToolAsync("get_unclassified_executions");

            Assert.That(TextOf(result), Does.Contain("Something nobody configured"));
        }

        [Test]
        public async Task Post_WithoutToken_ReturnsUnauthorizedWithResourceMetadata()
        {
            var response = await PostInitialize(token: null);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            var challenge = response.Headers.WwwAuthenticate.Single();
            Assert.That(challenge.Scheme, Is.EqualTo("Bearer"));
            Assert.That(challenge.Parameter, Does.Contain("realm=\"nightwatchman-mcp\""));
            Assert.That(challenge.Parameter, Does.Contain("/.well-known/oauth-protected-resource"));
        }

        [Test]
        public async Task Post_WithTokenWithoutRequiredScope_ReturnsForbidden()
        {
            var response = await PostInitialize(NightwatchmanAppFactory.CreateMcpToken(scope: "User.Read"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        [Test]
        public async Task Post_WithInvalidSignature_ReturnsUnauthorized()
        {
            var response = await PostInitialize(NightwatchmanAppFactory.CreateMcpToken(validSignature: false));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Post_WhenMcpAuthIsNotConfigured_FailsClosedWithUnauthorized()
        {
            using var factory = new UnconfiguredMcpAppFactory();
            using var client = factory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(
                    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}",
                    Encoding.UTF8,
                    "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", NightwatchmanAppFactory.CreateMcpToken());

            var response = await client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        /// <summary>App with empty MCP credentials — /mcp must reject everything.</summary>
        private class UnconfiguredMcpAppFactory : NightwatchmanAppFactory
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpAuth:ClientId"] = string.Empty,
                        ["McpAuth:ApiClientId"] = string.Empty,
                    }));
            }
        }

        private static string TextOf(CallToolResult result) =>
            string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
    }
}
