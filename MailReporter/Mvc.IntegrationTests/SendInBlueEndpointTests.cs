using System.Text;

using DomainModel;

using MongoDB.Driver;

namespace Mvc.IntegrationTests
{
    [TestFixture]
    public class SendInBlueEndpointTests
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
        public async Task Post_RealWebhookPayload_PersistsJobExecution()
        {
            var payload = await File.ReadAllTextAsync("./TestData/SendInBlue.Event.json");

            var response = await _client.PostAsync("/MailReporter/SendInBlue",
                new StringContent(payload, Encoding.UTF8, "application/json"));

            Assert.That(response.IsSuccessStatusCode, Is.True, await response.Content.ReadAsStringAsync());

            var executions = await _factory.GetDatabase().GetCollection<JobExecution>(nameof(JobExecution))
                .Find(_ => true).ToListAsync();
            Assert.That(executions, Has.Count.EqualTo(1));
            Assert.That(executions[0].JobName, Is.EqualTo("Unknown"));
            Assert.That(executions[0].OriginalSubject, Does.StartWith("Druckfreigabeanforderung"));
        }

        [Test]
        public async Task Post_EmptyEvent_PersistsNothing()
        {
            var response = await _client.PostAsync("/MailReporter/SendInBlue",
                new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.That(response.IsSuccessStatusCode, Is.True);

            var count = await _factory.GetDatabase().GetCollection<JobExecution>(nameof(JobExecution))
                .CountDocumentsAsync(e => e.OriginalSubject == null);
            Assert.That(count, Is.Zero);
        }
    }
}
