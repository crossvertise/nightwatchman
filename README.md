# Nightwatchman

Überwacht nächtliche Batch-Jobs (ETL-Läufe etc.), indem es deren Benachrichtigungs-E-Mails per
Webhook (Mandrill, SendInBlue/Brevo) entgegennimmt, sie einem konfigurierten Job zuordnet, den
Erfolgsstatus aus dem Betreff ableitet und das Ergebnis in MongoDB ablegt. Ein Dashboard
(Azure-AD-Login) und ein PRTG-JSON-Endpoint machen daraus eine Überfälligkeits-Überwachung.

- **Stack:** ASP.NET Core MVC auf .NET 10, MongoDB, Newtonsoft.Json
- **Build & Test:** `dotnet test MailReporter/MailReporter.sln` — Details und lokales Setup
  (Docker-Mongo, User Secrets) in [CLAUDE.md](CLAUDE.md)
- **Deployment:** automatisch per GitHub Actions auf `xv-nightwatchman-live` bei Push auf
  `master` — Details in [DEPLOYMENT.md](DEPLOYMENT.md)
- **Live:** https://xv-nightwatchman-live.azurewebsites.net
