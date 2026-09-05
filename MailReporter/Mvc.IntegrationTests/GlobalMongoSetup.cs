using EphemeralMongo;

namespace Mvc.IntegrationTests
{
    /// <summary>
    /// Starts a real mongod process once per test run (EphemeralMongo downloads the
    /// binary on first use). Each fixture uses its own database.
    /// </summary>
    [SetUpFixture]
    public class GlobalMongoSetup
    {
        private static IMongoRunner? _runner;

        public static string ConnectionString =>
            _runner?.ConnectionString ?? throw new InvalidOperationException("MongoDB runner is not started.");

        [OneTimeSetUp]
        public void StartMongo() => _runner = MongoRunner.Run();

        [OneTimeTearDown]
        public void StopMongo() => _runner?.Dispose();
    }
}
