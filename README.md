# Nightwatchman

Nightwatchman ("MailReporter") watches over nightly batch jobs (ETL runs, backups, and
similar scheduled processes) by accepting their notification e-mails via webhook, matching
each mail to a configured `Job`, deriving a success status from the subject line, and storing
the result as a `JobExecution` in MongoDB. A dashboard and a PRTG JSON endpoint turn this into
an overdue-job monitor, and an MCP endpoint lets AI assistants (Claude Code, claude.ai, Claude
Desktop, ...) query the same data conversationally — e.g. as part of a daily briefing.

- **Stack:** ASP.NET Core MVC on .NET 10, MongoDB, Newtonsoft.Json
- **License:** Apache License 2.0 — see [LICENSE.md](LICENSE.md)

## How it works

```
 batch job / ETL run
        │  sends notification e-mail on completion
        ▼
 e-mail provider (Mandrill / Brevo-SendInBlue)
        │  webhook POST
        ▼
 MailReporterController  ──▶ JobExecutionService.ClassifyExecution
        │                        (match mail → Job: sender → SubjectRegex → SubjectContains)
        │                    DetermineJobStatus
        │                        (job regexes if both set, else global Success/ErrorWords)
        ▼
 MongoDB (JobExecution, Job)
        │
        ├──▶ Dashboard (Entra ID login)         — human overview, overdue jobs
        ├──▶ GET /Dashboard/Prtg (Basic Auth)    — PRTG monitoring sensor
        └──▶ POST /mcp (OAuth via Entra ID)      — MCP tools for AI assistants
```

## Features

- Accepts job-completion e-mails via **Mandrill** (HMAC-signed webhook) and
  **Brevo/SendInBlue** (JSON webhook).
- Classifies each mail into a configured `Job` and a status (`Success` / `Error` /
  `Warning` / `Unknown`).
- Web dashboard (Azure AD / Entra ID login) showing job health and overdue jobs.
- **PRTG** HTTP Data Advanced sensor endpoint for infrastructure monitoring.
- **MCP server** (`/mcp`, Streamable HTTP) exposing read-only tools so an AI assistant can
  answer "are there any problems?" or compile a daily briefing.

## Quick start

### Prerequisites

- .NET SDK matching [`MailReporter/global.json`](MailReporter/global.json) (currently
  `10.0.203`, `rollForward: latestFeature`)
- A MongoDB instance, e.g. via Docker:
  ```powershell
  docker run -d --name nightwatchman-mongo -p 27018:27017 mongo:8
  ```
  (port 27018 is used because 27017 is often already taken on dev machines)

### Configure secrets

`Mvc/appsettings.json` intentionally ships with empty placeholders for secrets. Fill them in
locally via [User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
(the `UserSecretsId` is already set in `Mvc.csproj`):

```powershell
dotnet user-secrets set MongoDbConnectionString "mongodb://localhost:27018" --project MailReporter/Mvc/Mvc.csproj
dotnet user-secrets set MongoDbDatabaseName      "nightwatchman"            --project MailReporter/Mvc/Mvc.csproj
dotnet user-secrets set MandrillWebhookKey       "<your-mandrill-key>"      --project MailReporter/Mvc/Mvc.csproj
dotnet user-secrets set BasicAuthCredentials     "user:password"            --project MailReporter/Mvc/Mvc.csproj
```

To use the dashboard and the MCP endpoint locally you also need `AzureAd:*` and `McpAuth:*`
(see [Configuration](#configuration) and [docs/entra-setup.md](docs/entra-setup.md)).

### Build, run, test

```powershell
dotnet restore MailReporter/MailReporter.sln
dotnet build   MailReporter/MailReporter.sln
dotnet run     --project MailReporter/Mvc/Mvc.csproj

dotnet test    MailReporter/MailReporter.sln
```

Two test projects: `BusinessLogic.Tests` (unit tests, Moq) and `Mvc.IntegrationTests` (spins up
the whole app via `WebApplicationFactory` against a real mongod — EphemeralMongo downloads the
binary on first run, so it needs internet once; the OIDC redirect test needs network access to
`login.microsoftonline.com`).

## Configuration

All keys live under `Mvc/appsettings.json` (empty placeholders by default) or, in production,
as App Service application settings (`Key__SubKey` naming for nested sections).

| Key | Description |
|---|---|
| `MongoDbConnectionString` | MongoDB connection string |
| `MongoDbDatabaseName` | Database name |
| `MandrillWebhookKey` | Shared key used to verify the Mandrill webhook's HMAC-SHA1 signature |
| `BasicAuthCredentials` | `user:password` pair guarding `GET /Dashboard/Prtg` |
| `SuccessWords` | Comma-separated words indicating a successful run (fallback status detection) |
| `ErrorWords` | Comma-separated words indicating a failed run (fallback status detection) |
| `AzureAd:Instance` | Entra ID authority, e.g. `https://login.microsoftonline.com/` |
| `AzureAd:Domain` | Verified domain of your Entra tenant |
| `AzureAd:TenantId` | Tenant id (GUID or `organizations`) |
| `AzureAd:ClientId` | App registration used for the web dashboard sign-in (OIDC) |
| `AzureAd:CallbackPath` | OIDC redirect path, default `/signin-oidc` |
| `McpAuth:ClientId` | Public-client app registration used by MCP clients (see below) |
| `McpAuth:ApiClientId` | Resource/API app registration that the `/mcp` token is issued for |
| `McpAuth:Scope` | Delegated scope required on the token; defaults to `api://{ApiClientId}/access_as_user` when empty |

See [docs/entra-setup.md](docs/entra-setup.md) for how to create the Entra app registrations
these keys point to.

## Setting up jobs

`JobService.SeedJobs()` (`POST /Job/SeedJobs`) inserts a set of example jobs to get you
started; it's a plain insert with no duplicate check, meant as a starting point rather than
something to run repeatedly. Manage jobs afterwards via the `/Job` dashboard pages.

**Matching a mail to a job** (`JobExecutionService.ClassifyExecution`), in this fixed order:

1. `Job.EmailSender` — exact sender address match
2. `Job.SubjectRegex` — regex match against the subject
3. `Job.SubjectContains` — substring match against the subject

No match ⇒ the execution is filed under a synthetic `"Unknown"` job.

**Determining success/failure** (`JobExecutionService.DetermineJobStatus`):

- If a job has **both** `SuccessSubjectRegex` and `ErrorSubjectRegex` set, those are used.
- Otherwise the global `SuccessWords` / `ErrorWords` lists are checked against the subject.
- If neither the job-specific regexes nor the global word lists are configured, the service
  throws — at least one of the two must be usable.

`ReclassifyUnclassified` re-runs classification for already-stored `"Unknown"` executions —
handy after you've adjusted a job's matching pattern.

## Webhooks

### Mandrill

Configure a webhook in Mandrill pointing at:

```
POST https://<your-host>/MailReporter/Mandrill
```

Set the webhook's signing key as `MandrillWebhookKey`; requests are rejected with `401` unless
the `X-Mandrill-Signature` header matches. `GET`/`HEAD /MailReporter/Mandrill` are anonymous
and serve as Mandrill's health check.

### Brevo / SendInBlue

Configure an inbound parse webhook pointing at:

```
POST https://<your-host>/MailReporter/SendInBlue
```

This endpoint is **unsigned** — Brevo does not sign inbound webhook payloads. If you need to
restrict access, do so at the network layer (IP allowlist, a reverse proxy shared secret, etc.)
rather than relying on the endpoint itself.

## PRTG monitoring

Add an **HTTP Data Advanced** sensor pointing at:

```
GET https://<your-host>/Dashboard/Prtg
```

with Basic authentication using the `user:password` pair configured in
`BasicAuthCredentials`. The endpoint returns PRTG channels with the seconds remaining until
each job's next expected run (negative once overdue).

## MCP (Model Context Protocol)

Nightwatchman exposes a stateless **Streamable HTTP** MCP endpoint at `/mcp`, so an AI
assistant can be asked things like *"Are there any problems with Nightwatchman?"* or *"What
went wrong last night?"* without anyone opening the dashboard.

### Authentication

`/mcp` is protected by **OAuth 2.0 / OIDC against Microsoft Entra ID**. The server itself acts
as an OAuth proxy in front of Entra ID (`/.well-known/*`, `/authorize`, `/token`, `/register`),
so MCP clients only need the server's URL — they discover the OAuth endpoints automatically and
the user authenticates via a normal browser sign-in, no client-side Entra configuration
required. See [docs/entra-setup.md](docs/entra-setup.md) for the two app registrations behind
this (a public client for the MCP tool and a resource/API app for the token audience).

### Tools

| Tool | Description |
|---|---|
| `get_health_summary(lookbackHours = 24)` | Overall health: healthy/failed/overdue/unknown job counts and a list of problems. Start here for "is everything OK?" |
| `list_jobs()` | All configured jobs with their last status and next expected run |
| `get_job(jobIdOrName)` | Details for one job (matched by id, or name case-insensitively), including recent executions |
| `get_job_executions(jobIdOrName, limit = 10)` | Recent executions for one job |
| `get_recent_executions(hours = 24, limit = 100, status = null)` | Recent executions across all jobs, optionally filtered by status |
| `get_recent_failures(hours = 24, limit = 50)` | Recent `Error`/`Warning` executions across all jobs |
| `get_unclassified_executions(limit = 50)` | Executions that couldn't be matched to a job (the `"Unknown"` job) |

There is also an MCP **prompt** `daily_briefing`, which instructs the client to call
`get_health_summary` and, if problems are found, `get_recent_failures`, then summarize the
result. All tools are read-only — Nightwatchman's MCP server cannot create or modify jobs.

### Connecting a client

**Claude Code:**

```
claude mcp add --transport http nightwatchman https://<your-host>/mcp
```

then run `/mcp` inside Claude Code and follow the browser login prompt.

**claude.ai / Claude Desktop:** Settings → Connectors → Add custom connector, and enter
`https://<your-host>/mcp` as the URL. You'll be prompted to sign in via your browser the first
time you use it.

## Development & testing

- Run MongoDB locally, e.g. `docker run -d --name nightwatchman-mongo -p 27018:27017 mongo:8`.
- Configure secrets via `dotnet user-secrets` (see [Quick start](#quick-start)).
- `dotnet test MailReporter/MailReporter.sln` runs both test projects.
  - `Mvc.IntegrationTests` uses [EphemeralMongo](https://github.com/asimmon/ephemeral-mongo),
    which downloads a `mongod` binary on first use — needs internet once.
  - The OIDC redirect test in that project needs network access to
    `login.microsoftonline.com`.
- Run a single test (NUnit filter):
  ```powershell
  dotnet test MailReporter/BusinessLogic.Tests/BusinessLogic.Tests.csproj --filter "FullyQualifiedName~ProcessSendInBlueEvent_EmptyEvent"
  ```

See [CLAUDE.md](CLAUDE.md) for architecture and code-style notes aimed at contributors.

## Deployment

Deployed via GitHub Actions using OIDC/federated credentials (no stored secrets) — see
[DEPLOYMENT.md](DEPLOYMENT.md).

## License

Apache License 2.0 — see [LICENSE.md](LICENSE.md).
