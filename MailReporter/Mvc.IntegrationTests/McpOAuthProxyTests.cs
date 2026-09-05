using System.Net;

using Newtonsoft.Json.Linq;

namespace Mvc.IntegrationTests
{
    [TestFixture]
    public class McpOAuthProxyTests
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
        public async Task Get_ProtectedResourceMetadata_IsAnonymousAndPointsAtTheMcpEndpoint()
        {
            var response = await _client.GetAsync("/.well-known/oauth-protected-resource");
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(json["resource"]?.Value<string>(), Is.EqualTo("https://localhost/mcp"));
            Assert.That(json["authorization_servers"]?.First?.Value<string>(), Is.EqualTo("https://localhost"));
            Assert.That(
                json["scopes_supported"]?.First?.Value<string>(),
                Is.EqualTo($"api://{NightwatchmanAppFactory.McpApiClientId}/access_as_user"));
            Assert.That(json["bearer_methods_supported"]?.First?.Value<string>(), Is.EqualTo("header"));
        }

        [TestCase("/.well-known/openid-configuration")]
        [TestCase("/.well-known/oauth-authorization-server")]
        public async Task Get_AuthorizationServerMetadata_AdvertisesTheProxyEndpoints(string path)
        {
            var response = await _client.GetAsync(path);
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(json["authorization_endpoint"]?.Value<string>(), Is.EqualTo("https://localhost/authorize"));
            Assert.That(json["token_endpoint"]?.Value<string>(), Is.EqualTo("https://localhost/token"));
            Assert.That(json["registration_endpoint"]?.Value<string>(), Is.EqualTo("https://localhost/register"));
            Assert.That(
                json["issuer"]?.Value<string>(),
                Is.EqualTo($"https://login.microsoftonline.com/{NightwatchmanAppFactory.TenantId}/v2.0"));
            Assert.That(json["code_challenge_methods_supported"]?.Values<string>(), Does.Contain("S256"));
        }

        [Test]
        public async Task Get_Metadata_HonoursXForwardedProto()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/oauth-protected-resource");
            request.Headers.Add("X-Forwarded-Proto", "https");

            var response = await _client.SendAsync(request);
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(json["resource"]?.Value<string>(), Does.StartWith("https://"));
        }

        [Test]
        public async Task Post_Register_EchoesTheConfiguredClientId()
        {
            var response = await _client.PostAsync(
                "/register",
                new StringContent("{\"redirect_uris\":[\"https://claude.ai/api/mcp/auth_callback\"]}",
                    System.Text.Encoding.UTF8,
                    "application/json"));
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(json["client_id"]?.Value<string>(), Is.EqualTo(NightwatchmanAppFactory.McpClientId));
            Assert.That(
                json["redirect_uris"]?.First?.Value<string>(),
                Is.EqualTo("https://claude.ai/api/mcp/auth_callback"));
            Assert.That(json["token_endpoint_auth_method"]?.Value<string>(), Is.EqualTo("none"));
        }

        [Test]
        public async Task Get_Authorize_RedirectsToEntraWithNormalisedScope()
        {
            var response = await _client.GetAsync(
                "/authorize?client_id=" + NightwatchmanAppFactory.McpClientId +
                "&response_type=code&redirect_uri=https%3A%2F%2Fclaude.ai%2Fapi%2Fmcp%2Fauth_callback" +
                "&scope=" + Uri.EscapeDataString($"api://{NightwatchmanAppFactory.McpClientId}/.default") +
                "&resource=https%3A%2F%2Flocalhost%2Fmcp&prompt=consent");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));

            var location = response.Headers.Location!.ToString();
            var query = Uri.UnescapeDataString(location);

            Assert.That(location, Does.StartWith(
                $"https://login.microsoftonline.com/{NightwatchmanAppFactory.TenantId}/oauth2/v2.0/authorize?"));
            Assert.That(query, Does.Contain($"api://{NightwatchmanAppFactory.McpApiClientId}/access_as_user"));
            Assert.That(query, Does.Contain("offline_access"));
            Assert.That(query, Does.Not.Contain(".default"));
            Assert.That(query, Does.Not.Contain("resource="));
            Assert.That(query, Does.Not.Contain("prompt="));
            Assert.That(query, Does.Contain("acrs"));
        }
    }
}
