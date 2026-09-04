using EphemeralMongo;

namespace Mvc.IntegrationTests
{
    /// <summary>
    /// Startet einmal pro Testlauf einen echten mongod-Prozess (EphemeralMongo lädt das
    /// Binary beim ersten Lauf herunter). Jede Fixture nutzt eine eigene Datenbank.
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
