using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using MongoDB.Driver;

namespace Mvc.IntegrationTests
{
    /// <summary>
    /// Fährt die komplette Web-App gegen den EphemeralMongo hoch.
    /// Jede Factory-Instanz bekommt eine eigene Datenbank, damit Fixtures isoliert bleiben.
    /// </summary>
    public class NightwatchmanAppFactory : WebApplicationFactory<Startup>
    {
        public const string MandrillWebhookKey = "integration-test-mandrill-key";
        public const string BasicAuthUser = "prtg";
        public const string BasicAuthPassword = "secret";

        public string DatabaseName { get; } = "nightwatchman-test-" + Guid.NewGuid().ToString("N");

        public NightwatchmanAppFactory()
        {
            ClientOptions.BaseAddress = new Uri("https://localhost");
            ClientOptions.AllowAutoRedirect = false;
        }

        public IMongoDatabase GetDatabase() =>
            new MongoClient(GlobalMongoSetup.ConnectionString).GetDatabase(DatabaseName);

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
                });
            });
        }
    }
}
