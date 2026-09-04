# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Überblick

Nightwatchman ("MailReporter") überwacht nächtliche Batch-Jobs (ETL-Läufe etc.), indem es deren
Benachrichtigungs-E-Mails per Webhook entgegennimmt, sie einem konfigurierten `Job` zuordnet, den
Erfolgsstatus aus dem Betreff ableitet und das Ergebnis als `JobExecution` in MongoDB ablegt.
Ein Dashboard sowie ein PRTG-JSON-Endpoint machen daraus eine Überfälligkeits-Überwachung.

## Build & Test

Solution: `MailReporter/MailReporter.sln`

```powershell
dotnet restore MailReporter/MailReporter.sln
dotnet build   MailReporter/MailReporter.sln
dotnet run     --project MailReporter/Mvc/Mvc.csproj

dotnet test    MailReporter/MailReporter.sln
# Einzelner Test / Filter (NUnit):
dotnet test    MailReporter/BusinessLogic.Tests/BusinessLogic.Tests.csproj --filter "FullyQualifiedName~ProcessSendInBlueEvent_EmptyEvent"
```

Zwei Testprojekte: `BusinessLogic.Tests` (Unit-Tests, Moq) und `Mvc.IntegrationTests`
(fährt die komplette App per `WebApplicationFactory` gegen einen echten mongod hoch —
EphemeralMongo lädt das Binary beim ersten Lauf herunter, braucht also einmalig Internet;
der OIDC-Redirect-Test braucht Netz zu login.microsoftonline.com).

Lokal ausführen: MongoDB z. B. via `docker run -d --name nightwatchman-mongo -p 27018:27017 mongo:8`
(Port 27017 ist auf Dev-Maschinen oft belegt), dann die vier Secrets per
`dotnet user-secrets set <Key> <Wert> --project MailReporter/Mvc/Mvc.csproj` setzen
(`MongoDbConnectionString` = `mongodb://localhost:27018`, `MongoDbDatabaseName`,
`MandrillWebhookKey`, `BasicAuthCredentials`).

Alle Projekte targeten einheitlich `net10.0`; `MailReporter/global.json` pinnt das SDK
(`10.0.203`, `rollForward: latestFeature`). JSON läuft bewusst weiterhin über Newtonsoft.Json
(`AddNewtonsoftJson()` in `Startup`) — `JObject` steckt in der `ISendInBlueService`-Signatur und
der PRTG-Endpoint hängt an Newtonsoft-`JsonSerializerSettings` (camelCase, Nulls weggelassen).

## Architektur

Schichtung mit strikter Abhängigkeitsrichtung `Mvc → BusinessLogic → Repos → DomainModel`.
Registrierung über verkettete DI-Extensions: `Startup.ConfigureServices` ruft
`services.RegisterServices()` (BusinessLogic/DIExtensions.cs), das intern `RegisterRepositories()`
(Repos/DIExtensions.cs) aufruft. Beide Extension-Klassen liegen im Namespace `BusinessLogic` —
eine neue Abhängigkeit wird an genau einer dieser beiden Stellen ergänzt.

**Persistenz** — `AMongoRepo<T>` ist die generische Basis (CRUD). Der Collection-Name ist
`typeof(T).Name`, die Verbindung kommt aus `MongoDbConnectionString` / `MongoDbDatabaseName` in der
Konfiguration. Jedes Repo gibt via `IdProperty` seinen String-Id-Selector an; die Ids sind
`[BsonId(IdGenerator = typeof(StringObjectIdGenerator))]`.

**Kernlogik** — `JobExecutionService`:
- `ClassifyExecution` ordnet eine Mail einem `Job` zu, in fester Reihenfolge: Absenderadresse
  (`Job.EmailSender`) → `Job.SubjectRegex` → `Job.SubjectContains`. Kein Treffer ⇒ Job `"Unknown"`.
- `DetermineJobStatus` nutzt bevorzugt die job-spezifischen `SuccessSubjectRegex`/`ErrorSubjectRegex`
  (nur wenn **beide** gesetzt sind), sonst die globalen Wortlisten `SuccessWords`/`ErrorWords` aus
  der Konfiguration. Fehlen diese Listen, wirft der Service.
- `_allJobs` wird pro Request-Scope einmal geladen und gecached (Services sind `Scoped`).
- `ReclassifyUnclassified` klassifiziert bereits gespeicherte, unbekannte Executions nachträglich
  neu — nützlich, nachdem ein Job-Matching-Pattern angepasst wurde.

**Webhook-Eingänge** (beide `[AllowAnonymous]`, in `MailReporterController`):
- `POST/GET/HEAD /MailReporter/Mandrill` — Formular-Payload, HMAC-SHA1-signaturgeprüft durch
  `MandrillWebhookAttribute` gegen `MandrillWebhookKey`; `HEAD` dient dem Mandrill-Health-Check.
- `POST /MailReporter/SendInBlue` — JSON-Payload (`[FromBody] JObject`), verarbeitet in
  `SendInBlueService`. Ungeprüft/unsigniert.

**Authentifizierung** — global gilt ein `AuthorizeFilter` (Azure AD, Tenant crossvertise.com), d.h.
jede neue Action ist standardmäßig geschützt. Ausnahmen brauchen explizit `[AllowAnonymous]`.
`GET /Dashboard/Prtg` ist zusätzlich per `[BasicAuth]` gegen `BasicAuthCredentials`
(Format `user:password`) abgesichert und liefert PRTG-Kanäle mit den Sekunden bis zur nächsten
erwarteten Ausführung.

## Konfiguration

`Mvc/appsettings.json` enthält bewusst leere Platzhalter für Secrets
(`MongoDbConnectionString`, `MongoDbDatabaseName`, `MandrillWebhookKey`, `BasicAuthCredentials`) —
lokal über User Secrets (`UserSecretsId` ist im Mvc.csproj gesetzt) befüllen, nicht in der Datei.

`JobService.SeedJobs()` (POST `/Job/SeedJobs`) legt einen fest verdrahteten Satz crossvertise-Jobs
an; es ist ein reines Insert ohne Duplikatsprüfung.

## Codestil

Der Bestand folgt durchgängig: `using`-Direktiven **innerhalb** des `namespace`-Blocks, Gruppen
alphabetisch mit Leerzeile getrennt, Expression-bodied Members für einzeilige Service-/Repo-Methoden.
Neue Dateien in diesem Stil halten.
