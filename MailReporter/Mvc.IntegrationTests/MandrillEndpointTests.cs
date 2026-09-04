using System.Security.Cryptography;
using System.Text;

using DomainModel;

using MongoDB.Driver;

namespace Mvc.IntegrationTests
{
    [TestFixture]
    public class MandrillEndpointTests
    {
        private NightwatchmanAppFactory _factory = null!;
        private HttpClient _client = null!;

        [OneTimeSetUp]
        public void CreateFactory()
        {
            _factory = new NightwatchmanAppFactory();
            _client = _factory.CreateClient();
        }

        [OneTimeTearDown]
        public void DisposeFactory()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task Head_ReturnsOk_ForMandrillHealthCheck()
        {
            var response = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/MailReporter/Mandrill"));

            Assert.That(response.IsSuccessStatusCode, Is.True);
        }

        [Test]
        public async Task Post_WithValidSignature_PersistsJobExecution()
        {
            var mandrillEvents = """[{"msg":{"from_email":"etl@example.com","subject":"Nightly ETL erfolgreich","html":"<p>ok</p>","text":"ok"}}]""";
            var form = new Dictionary<string, string> { ["mandrill_events"] = mandrillEvents };

            var request = new HttpRequestMessage(HttpMethod.Post, "/MailReporter/Mandrill")
            {
                Content = new FormUrlEncodedContent(form),
            };
            request.Headers.Add("X-Mandrill-Signature", ComputeSignature("https://localhost/MailReporter/Mandrill", form));

            var response = await _client.SendAsync(request);

            Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());

            var executions = await _factory.GetDatabase().GetCollection<JobExecution>(nameof(JobExecution))
                .Find(e => e.OriginalSubject == "Nightly ETL erfolgreich").ToListAsync();
            Assert.That(executions, Has.Count.EqualTo(1));
            Assert.That(executions[0].JobName, Is.EqualTo("Unknown"));
            Assert.That(executions[0].Status, Is.EqualTo(JobExecutionStatus.Success));
            Assert.That(executions[0].NotificationEmail.Sender, Is.EqualTo("etl@example.com"));
        }

        [Test]
        public async Task Post_WithInvalidSignature_ReturnsUnauthorized_AndPersistsNothing()
        {
            var form = new Dictionary<string, string>
            {
                ["mandrill_events"] = """[{"msg":{"from_email":"x@example.com","subject":"Manipulated failed","html":"","text":""}}]""",
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/MailReporter/Mandrill")
            {
                Content = new FormUrlEncodedContent(form),
            };
            request.Headers.Add("X-Mandrill-Signature", "invalid-signature");

            var response = await _client.SendAsync(request);

            Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));

            var count = await _factory.GetDatabase().GetCollection<JobExecution>(nameof(JobExecution))
                .CountDocumentsAsync(e => e.OriginalSubject == "Manipulated failed");
            Assert.That(count, Is.Zero);
        }

        /// <summary>Bildet die HMAC-SHA1-Signatur exakt wie MandrillWebhookAttribute.GenerateSignature.</summary>
        private static string ComputeSignature(string url, IReadOnlyDictionary<string, string> form)
        {
            var sourceString = url + string.Concat(form.Select(item => item.Key + item.Value));
            using var hmac = new HMACSHA1(Encoding.ASCII.GetBytes(NightwatchmanAppFactory.MandrillWebhookKey));
            return Convert.ToBase64String(hmac.ComputeHash(Encoding.ASCII.GetBytes(sourceString)));
        }
    }
}
