using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

using MongoDB.Driver;

using Mvc.Mcp;

namespace Mvc.IntegrationTests
{
    /// <summary>
    /// Boots the complete web app against the EphemeralMongo instance.
    /// Every factory instance gets its own database so that fixtures stay isolated.
    /// </summary>
    public class NightwatchmanAppFactory : WebApplicationFactory<Startup>
    {
        public const string MandrillWebhookKey = "integration-test-mandrill-key";
        public const string BasicAuthUser = "prtg";
        public const string BasicAuthPassword = "secret";

        // "organizations" keeps the OpenID Connect redirect test working against the real
        // Microsoft metadata endpoint; the other ids are dummies.
        public const string TenantId = "organizations";
        public const string WebClientId = "11111111-1111-1111-1111-111111111111";
        public const string McpClientId = "22222222-2222-2222-2222-222222222222";
        public const string McpApiClientId = "33333333-3333-3333-3333-333333333333";

        /// <summary>Issuer the test tokens are minted with; replaces Entra ID in tests.</summary>
        public const string TestIssuer = "https://test-issuer";

        private const string SigningSecret = "nightwatchman-integration-test-signing-key-0123456789";
        private const string WrongSigningSecret = "nightwatchman-integration-test-WRONG-key-0123456789";

        private static readonly SymmetricSecurityKey SigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningSecret));

        private static readonly SymmetricSecurityKey WrongSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(WrongSigningSecret));

        public string DatabaseName { get; } = "nightwatchman-test-" + Guid.NewGuid().ToString("N");

        public NightwatchmanAppFactory()
        {
            ClientOptions.BaseAddress = new Uri("https://localhost");
            ClientOptions.AllowAutoRedirect = false;
        }

        public IMongoDatabase GetDatabase() =>
            new MongoClient(GlobalMongoSetup.ConnectionString).GetDatabase(DatabaseName);

        /// <summary>Creates a bearer token that the "McpBearer" scheme accepts in tests.</summary>
        public static string CreateMcpToken(string scope = "access_as_user", bool validSignature = true)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = TestIssuer,
                Audience = McpApiClientId,
                Expires = DateTime.UtcNow.AddMinutes(10),
                Claims = new Dictionary<string, object>
                {
                    [ClaimTypes.NameIdentifier] = "integration-test-user",
                    ["scp"] = scope,
                },
                SigningCredentials = new SigningCredentials(
                    validSignature ? SigningKey : WrongSigningKey,
                    SecurityAlgorithms.HmacSha256),
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MongoDbConnectionString"] = GlobalMongoSetup.ConnectionString,
                    ["MongoDbDatabaseName"] = DatabaseName,
                    ["MandrillWebhookKey"] = MandrillWebhookKey,
                    ["BasicAuthCredentials"] = BasicAuthUser + ":" + BasicAuthPassword,
                    ["SuccessWords"] = "success, succeeded, completed, erfolgreich",
                    ["ErrorWords"] = "failed, error, fehlgeschlagen, fehler",
                    ["AzureAd:TenantId"] = TenantId,
                    ["AzureAd:ClientId"] = WebClientId,
                    ["AzureAd:Domain"] = "example.com",
                    ["McpAuth:ClientId"] = McpClientId,
                    ["McpAuth:ApiClientId"] = McpApiClientId,
                });
            });

            builder.ConfigureTestServices(services =>
            {
                // Validate MCP tokens against a local symmetric key instead of Entra ID,
                // so the tests need no network and can mint their own tokens.
                services.Configure<JwtBearerOptions>(McpAuthExtensions.SchemeName, options =>
                {
                    options.Authority = null;
                    options.MetadataAddress = null!;
                    options.RequireHttpsMetadata = false;
                    options.Configuration = new OpenIdConnectConfiguration();
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = TestIssuer,
                        ValidateAudience = true,
                        ValidAudiences = new[] { McpApiClientId, $"api://{McpApiClientId}" },
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = SigningKey,
                        ValidateLifetime = true,
                    };
                });
            });
        }
    }
}
