using System.Net;
using System.Net.Http.Headers;
using System.Text;

using DomainModel;

using MongoDB.Driver;

using Newtonsoft.Json.Linq;

namespace Mvc.IntegrationTests
{
    [TestFixture]
    public class PrtgEndpointTests
    {
        private NightwatchmanAppFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public async Task CreateFactoryAndSeedData()
        {
            _factory = new NightwatchmanAppFactory();
            _client = _factory.CreateClient();

            var database = _factory.GetDatabase();
            await database.GetCollection<Job>(nameof(Job)).InsertOneAsync(new Job
            {
                Name = "EtlJob",
                SubjectContains = "ETL",
                ExpectedInterval = TimeSpan.FromHours(24),
            });

            var job = await database.GetCollection<Job>(nameof(Job)).Find(_ => true).FirstAsync();
            await database.GetCollection<JobExecution>(nameof(JobExecution)).InsertOneAsync(new JobExecution
            {
                JobId = job.Id,
                JobName = job.Name,
                OriginalSubject = "ETL erfolgreich",
                Finished = DateTime.UtcNow.AddHours(-2),
                Status = JobExecutionStatus.Success,
            });
        }

        [OneTimeTearDown]
        public void DisposeFactory()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task Get_WithoutCredentials_ReturnsUnauthorized()
        {
            var response = await _client.GetAsync("/Dashboard/Prtg");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Get_WithWrongCredentials_ReturnsUnauthorized()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/Prtg");
            request.Headers.Authorization = BasicAuth("prtg", "wrong-password");

            var response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Get_WithValidCredentials_ReturnsPrtgJsonContract()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/Dashboard/Prtg");
            request.Headers.Authorization = BasicAuth(NightwatchmanAppFactory.BasicAuthUser, NightwatchmanAppFactory.BasicAuthPassword);

            var response = await _client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

            // The PRTG sensor depends on the exact contract: camelCase keys, null values omitted.
            var json = JObject.Parse(body);
            var channel = (JObject?)json["prtg"]?["result"]?.First;
            Assert.That(channel, Is.Not.Null, body);
            Assert.That(channel!["channel"]?.Value<string>(), Is.EqualTo("EtlJob"));
            Assert.That(channel["unit"]?.Value<string>(), Is.EqualTo("TimeSeconds"));
            Assert.That(channel["warning"]?.Value<int>(), Is.EqualTo(0));
            Assert.That(channel["value"]?.Value<int>(), Is.GreaterThan(0).And.LessThanOrEqualTo(22 * 3600));
            Assert.That(channel["limitMinWarning"]?.Value<int>(), Is.EqualTo(0));
            Assert.That(channel["limitMinError"]?.Value<int>(), Is.EqualTo((int)(-TimeSpan.FromHours(24).TotalSeconds * 0.1)));
            Assert.That(channel["limitMode"]?.Value<int>(), Is.EqualTo(1));

            // NullValueHandling.Ignore: limits that are not set must not appear.
            Assert.That(channel.ContainsKey("limitMaxError"), Is.False);
            Assert.That(channel.ContainsKey("limitMaxWarning"), Is.False);
        }

        private static AuthenticationHeaderValue BasicAuth(string user, string password) =>
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));
    }
}
