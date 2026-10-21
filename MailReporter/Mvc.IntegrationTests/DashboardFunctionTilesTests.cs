using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;

using BusinessLogic.Interfaces;

using DomainModel;
using DomainModel.DTO;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mvc.IntegrationTests
{
    /// <summary>
    /// Renders the dashboard as a signed-in user (a test scheme replaces the Entra sign-in) and checks
    /// the Azure Functions tiles.
    /// </summary>
    [TestFixture]
    public class DashboardFunctionTilesTests
    {
        private const string TestScheme = "Test";

        private NightwatchmanAppFactory _factory = null!;

        [OneTimeSetUp]
        public void CreateFactory()
        {
            _factory = new NightwatchmanAppFactory();
        }

        [OneTimeTearDown]
        public void DisposeFactory()
        {
            _factory.Dispose();
        }

        [Test]
        public async Task Dashboard_shows_a_tile_per_function_with_its_state()
        {
            var functions = new[]
            {
                new FunctionOverview
                {
                    Monitor = new FunctionMonitor { Name = "Mailer", FunctionApp = "xv-functions-mailer-live-v2", Queue = "xv-mailer-queue" },
                    WaitingMessages = 11, CompletedMessages = 0, DeadLetteredMessages = 126, WindowMinutes = 30, IsStuck = true,
                },
                new FunctionOverview
                {
                    Monitor = new FunctionMonitor { Name = "CRM integration", Topic = "xv-crm-integration-topic", Subscription = "all-messages" },
                    WaitingMessages = 0, CompletedMessages = 62, DeadLetteredMessages = 3025, WindowMinutes = 30,
                },
                new FunctionOverview
                {
                    Monitor = new FunctionMonitor { Name = "Optimizer", Queue = "xv-optimize-queue-live" },
                    WindowMinutes = 30, Error = "Azure answered 403 Forbidden",
                },
            };

            var html = await GetDashboard(services => services.AddScoped<IFunctionHealthService>(_ => new FixedFunctionHealthService(functions)));

            Assert.That(html, Does.Contain("Azure Functions"));
            Assert.That(Tile(html, "Mailer"), Does.Contain("panel-danger").And.Contain("Stuck").And.Contain("Waiting: 11").And.Contain("Dead letters: 126"));
            Assert.That(Tile(html, "CRM integration"), Does.Contain("panel-success").And.Contain("OK").And.Contain("xv-crm-integration-topic/all-messages"));
            Assert.That(Tile(html, "Optimizer"), Does.Contain("panel-default").And.Contain("Unknown").And.Contain("Azure answered 403 Forbidden"));
        }

        /// <summary>The default configuration has no monitors: no section, and Azure is never asked.</summary>
        [Test]
        public async Task Dashboard_without_configured_functions_has_no_functions_section()
        {
            var html = await GetDashboard(_ => { });

            Assert.That(html, Does.Contain("Job Overwiew"));
            Assert.That(html, Does.Not.Contain("Azure Functions"));
        }

        private async Task<string> GetDashboard(Action<IServiceCollection> configureServices)
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestScheme;
                    options.DefaultAuthenticateScheme = TestScheme;
                    options.DefaultChallengeScheme = TestScheme;
                }).AddScheme<AuthenticationSchemeOptions, SignedInUserHandler>(TestScheme, _ => { });
                configureServices(services);
            }));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/Dashboard");

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>The markup of one tile: from its heading to the next tile.</summary>
        private static string Tile(string html, string name)
        {
            var heading = html.IndexOf(">" + name + "<", StringComparison.Ordinal);
            Assert.That(heading, Is.GreaterThan(0), $"no tile for {name}");

            var start = html.LastIndexOf("<div class=\"panel ", heading, StringComparison.Ordinal);
            var next = html.IndexOf("<div class=\"panel ", heading, StringComparison.Ordinal);
            return html.Substring(start, (next < 0 ? html.Length : next) - start);
        }

        private sealed class FixedFunctionHealthService : IFunctionHealthService
        {
            private readonly IReadOnlyList<FunctionOverview> _functions;

            public FixedFunctionHealthService(IReadOnlyList<FunctionOverview> functions)
            {
                _functions = functions;
            }

            public Task<IReadOnlyList<FunctionOverview>> GetOverview() => Task.FromResult(_functions);
        }

        private sealed class SignedInUserHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public SignedInUserHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
                : base(options, logger, encoder)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "integration-test-user") }, TestScheme);
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
            }
        }
    }
}
