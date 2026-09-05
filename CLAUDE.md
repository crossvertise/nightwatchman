# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

## Overview

Nightwatchman ("MailReporter") watches over nightly batch jobs (ETL runs etc.) by accepting
their notification e-mails via webhook, matching each mail to a configured `Job`, deriving the
success status from the subject line, and storing the result as a `JobExecution` in MongoDB.
A dashboard, a PRTG JSON endpoint, and an MCP server turn this into an overdue-job monitor that
can also be queried conversationally by an AI assistant.

## Build & Test

Solution: `MailReporter/MailReporter.sln`

```powershell
dotnet restore MailReporter/MailReporter.sln
dotnet build   MailReporter/MailReporter.sln
dotnet run     --project MailReporter/Mvc/Mvc.csproj

dotnet test    MailReporter/MailReporter.sln
# Single test / filter (NUnit):
dotnet test    MailReporter/BusinessLogic.Tests/BusinessLogic.Tests.csproj --filter "FullyQualifiedName~ProcessSendInBlueEvent_EmptyEvent"
```

Two test projects: `BusinessLogic.Tests` (unit tests, Moq) and `Mvc.IntegrationTests`
(spins up the whole app via `WebApplicationFactory` against a real mongod —
EphemeralMongo downloads the binary on first run, so it needs internet once;
the OIDC redirect test needs network access to login.microsoftonline.com).

Run locally: start MongoDB, e.g. via `docker run -d --name nightwatchman-mongo -p 27018:27017 mongo:8`
(port 27017 is often already taken on dev machines), then set the secrets via
`dotnet user-secrets set <Key> <Value> --project MailReporter/Mvc/Mvc.csproj`
(`MongoDbConnectionString` = `mongodb://localhost:27018`, `MongoDbDatabaseName`,
`MandrillWebhookKey`, `BasicAuthCredentials`; for the dashboard and MCP locally also
`AzureAd:*`/`McpAuth:*`, see [docs/entra-setup.md](docs/entra-setup.md)).

All projects target `net10.0` uniformly; `MailReporter/global.json` pins the SDK
(`10.0.203`, `rollForward: latestFeature`). JSON deliberately still runs through Newtonsoft.Json
(`AddNewtonsoftJson()` in `Startup`) — `JObject` is baked into the `ISendInBlueService`
signature, and the PRTG endpoint relies on Newtonsoft `JsonSerializerSettings` (camelCase,
nulls omitted).

## Architecture

Layered with a strict dependency direction `Mvc → BusinessLogic → Repos → DomainModel`.
Registration via chained DI extensions: `Startup.ConfigureServices` calls
`services.RegisterServices()` (BusinessLogic/DIExtensions.cs), which internally calls
`RegisterRepositories()` (Repos/DIExtensions.cs). Both extension classes live in the
`BusinessLogic` namespace — a new dependency is added in exactly one of these two places.

**Persistence** — `AMongoRepo<T>` is the generic base (CRUD). The collection name is
`typeof(T).Name`; the connection comes from `MongoDbConnectionString` / `MongoDbDatabaseName` in
configuration. Each repo supplies its string id selector via `IdProperty`; ids are
`[BsonId(IdGenerator = typeof(StringObjectIdGenerator))]`.

**Core logic** — `JobExecutionService`:
- `ClassifyExecution` matches a mail to a `Job`, in fixed order: sender address
  (`Job.EmailSender`) → `Job.SubjectRegex` → `Job.SubjectContains`. No match ⇒ job `"Unknown"`.
- `DetermineJobStatus` prefers the job-specific `SuccessSubjectRegex`/`ErrorSubjectRegex`
  (only when **both** are set), otherwise the global word lists `SuccessWords`/`ErrorWords` from
  configuration. If those lists are missing, the service throws.
- `_allJobs` is loaded once per request scope and cached (services are `Scoped`).
- `ReclassifyUnclassified` re-classifies already-stored, unknown executions after the fact —
  useful once a job's matching pattern has been adjusted.

**Webhook inputs** (both `[AllowAnonymous]`, in `MailReporterController`):
- `POST/GET/HEAD /MailReporter/Mandrill` — form payload, HMAC-SHA1 signature verified by
  `MandrillWebhookAttribute` against `MandrillWebhookKey`; `HEAD` serves as Mandrill's health
  check.
- `POST /MailReporter/SendInBlue` — JSON payload (`[FromBody] JObject`), processed in
  `SendInBlueService`. Unverified/unsigned.

**Authentication** — a global `AuthorizeFilter` applies (Azure AD / Entra ID), i.e. every new
MVC action is protected by default. Exceptions need explicit `[AllowAnonymous]`.
`GET /Dashboard/Prtg` is additionally guarded by `[BasicAuth]` against `BasicAuthCredentials`
(format `user:password`) and returns PRTG channels with the seconds remaining until each job's
next expected run. `/mcp` uses a separate scheme (see MCP section below) and is **not** covered
by the MVC `AuthorizeFilter`, since it's a Minimal API endpoint.

## MCP server

Nightwatchman exposes a stateless **Streamable HTTP** MCP server at `/mcp` so an AI assistant
can answer questions like "are there any problems?" without opening the dashboard. Tools are
read-only — there is no job-creation/mutation surface via MCP.

- **Packages:** `ModelContextProtocol.AspNetCore` 2.2.0 (`Mvc.csproj`); the client package
  `ModelContextProtocol` 2.2.0 is used only in `Mvc.IntegrationTests.csproj` for tests.
- **Tools** — `Mvc/Mcp/NightwatchmanTools.cs` (`[McpServerToolType]`, static methods, services
  injected as method parameters): `get_health_summary`, `list_jobs`, `get_job`,
  `get_job_executions`, `get_recent_executions`, `get_recent_failures`,
  `get_unclassified_executions`. Each has a detailed `[Description]` aimed at LLM callers
  (summary-first — e.g. "use this first to answer 'are there any problems?'"). There is also an
  MCP **prompt** `daily_briefing` in the same style, instructing the client to call
  `get_health_summary` and, on problems, `get_recent_failures`.
- **Report DTOs** — `BusinessLogic/JobStatusReportService.cs` builds lightweight DTOs from
  `DomainModel/DTO/Report/*` (`HealthSummary`, `JobSummary`, `JobDetail`, `ExecutionSummary`,
  `JobProblem`) on top of `IJobExecutionService.GetOverview()` and `IJobExecutionRepo`.
  `ExecutionSummary` deliberately excludes the raw e-mail body/HTML.
- **Auth** — OAuth 2.0/OIDC against Microsoft Entra ID, following the same "client ≠ resource"
  pattern used by other internal MCP servers (avoids `AADSTS90009` on token refresh):
  - `Mvc/Mcp/McpAuthExtensions.cs` registers a JwtBearer scheme named **`McpBearer`**
    (separate from the existing OIDC/cookie default used by the MVC dashboard) and the
    authorization policy **`Mcp`** (`RequireAuthenticatedUser()` +
    `AddAuthenticationSchemes("McpBearer")` + an assertion that the `scp` claim contains
    `access_as_user`). Fails closed: an empty `McpAuth:ClientId`/`AzureAd:TenantId` means `/mcp`
    always returns `401` — no anonymous fallback.
  - `Mvc/Mcp/McpOAuthProxyEndpoints.cs` implements anonymous OAuth proxy endpoints
    (`/.well-known/openid-configuration`, `/.well-known/oauth-authorization-server`,
    `/.well-known/oauth-protected-resource`, `/authorize`, `/token`, `/register`) so MCP clients
    can complete an OAuth flow against Entra ID without any client-side configuration beyond the
    server's URL.
  - See [docs/entra-setup.md](docs/entra-setup.md) for the two app registrations involved
    (public client vs. resource/API) and [README.md](README.md#mcp-model-context-protocol) for
    the client-facing tool table and setup instructions.
- **Config keys:** `AzureAd:*` (existing, shared with the dashboard sign-in) plus
  `McpAuth:ClientId`, `McpAuth:ApiClientId`, `McpAuth:Scope`. All ship as empty placeholders in
  `appsettings.json` — real values are supplied via User Secrets locally or App Service
  application settings in production, never committed.
- **Adding a new tool:** add a static method to `NightwatchmanTools.cs` with `[McpServerTool]`
  and a thorough `[Description]`, take any needed services as method parameters (resolved via
  DI), and keep it read-only. Cover it in `McpEndpointTests.cs` (see below).
- **Tests:**
  - `BusinessLogic.Tests/JobStatusReportServiceTests.cs` — DTO/aggregation logic (healthy /
    failed / overdue / never-ran, filters, no body in the DTO), Moq-based like
    `JobExecutionServiceTests.cs`.
  - `Mvc.IntegrationTests/McpEndpointTests.cs` — exercises `/mcp` end-to-end via the MCP client
    SDK (`ListToolsAsync`, `CallToolAsync`), plus negative cases (no token ⇒ 401 with
    `WWW-Authenticate`; wrong scope ⇒ 403; invalid signature ⇒ 401). Uses locally signed JWTs
    (via a symmetric key configured in `NightwatchmanAppFactory`) rather than real Entra
    tokens.
  - `Mvc.IntegrationTests/McpOAuthProxyTests.cs` — the anonymous discovery/`authorize`/`register`
    endpoints.

## Deployment

Push to `master` with changes under `MailReporter/**` auto-deploys via GitHub Actions
(OIDC/federated credentials, no secrets) to an Azure App Service — test suite as a gate.
All details and the one-time setup are in [DEPLOYMENT.md](DEPLOYMENT.md).

## Configuration

`Mvc/appsettings.json` intentionally ships with empty placeholders for all secrets and
environment-specific values (`MongoDbConnectionString`, `MongoDbDatabaseName`,
`MandrillWebhookKey`, `BasicAuthCredentials`, `AzureAd:*`, `McpAuth:*`) — fill them in locally
via User Secrets (`UserSecretsId` is set in `Mvc.csproj`), not in the file itself. See
[README.md](README.md#configuration) for the full key reference.

`JobService.SeedJobs()` (`POST /Job/SeedJobs`) inserts a fixed set of example jobs to get you
started; it's a plain insert with no duplicate check.

## Code style

The codebase consistently follows: `using` directives **inside** the `namespace` block, groups
sorted alphabetically and separated by a blank line, expression-bodied members for one-line
service/repo methods. Keep new files in this style.
