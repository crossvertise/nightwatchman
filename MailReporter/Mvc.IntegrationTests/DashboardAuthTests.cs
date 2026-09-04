using System.Net;

namespace Mvc.IntegrationTests
{
    [TestFixture]
    public class DashboardAuthTests
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

        /// <summary>Benötigt Internetzugriff (OIDC-Metadaten von login.microsoftonline.com).</summary>
        [Test]
        public async Task Get_Dashboard_WithoutLogin_RedirectsToMicrosoftLogin()
        {
            var response = await _client.GetAsync("/Dashboard");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("login.microsoftonline.com"));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("response_type=id_token"));
        }

        [Test]
        public async Task Get_JobIndex_WithoutLogin_IsNotServedAnonymously()
        {
            var response = await _client.GetAsync("/Job");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(response.Headers.Location?.ToString(), Does.Contain("login.microsoftonline.com"));
        }
    }
}
