# Deployment

Nightwatchman wird per GitHub Actions ([`.github/workflows/deploy.yml`](.github/workflows/deploy.yml))
auf den Azure App Service **`xv-nightwatchman-live`** deployt.

## Ablauf

- **Trigger:** Push auf `master` mit Änderungen unter `MailReporter/**` (oder am Workflow selbst),
  außerdem manuell per `workflow_dispatch`.
- **Build-Job:** Restore, Build und die komplette Test-Suite (Unit + Integration, siehe CLAUDE.md)
  als Gate. Danach `dotnet publish` **self-contained für `win-x86`** — der App Service ist Windows
  mit 32-Bit-Worker, und self-contained macht das Deployment unabhängig von der dort installierten
  .NET-Runtime (das Portal-Stack-Setting ist deshalb irrelevant).
- **Deploy-Job:** läuft im GitHub-Environment `production`, deployt per `azure/webapps-deploy@v3`
  und prüft anschließend den anonymen Health-Check `HEAD /MailReporter/Mandrill` (Smoke-Test).

## Azure-Ressourcen

| Was | Wert |
|---|---|
| App Service | `xv-nightwatchman-live` (Windows, 32-Bit-Worker) |
| Resource Group | `nightwatchman-live` |
| Subscription | „Crossvertise Playground Dev/Test" — `b6ba45e2-3816-44b0-ba54-78f080dc57d2` |
| URL | https://xv-nightwatchman-live.azurewebsites.net |

Die App-Secrets (`MongoDbConnectionString`, `MongoDbDatabaseName`, `MandrillWebhookKey`,
`BasicAuthCredentials`, `SuccessWords`/`ErrorWords`) liegen als **App Settings direkt am App
Service** — nicht in GitHub.

## Authentifizierung: OIDC / Federated Identity Credentials

Der Workflow authentifiziert sich **ohne Secrets** über den org-weiten
**„Github Actions Service Principal"** (gleiches Muster wie ServiceBusAuditor, powerbi-mcp, crm-mcp):

| Was | Wert |
|---|---|
| App Registration (Client-Id) | `c1c157eb-d4c5-4288-b9b1-5615b9a4c832` |
| Service Principal (Object-Id) | `2f7e0cec-9d48-4379-8f7a-f5ca8e51c4d1` |
| Tenant | `70470a10-c0d1-4c3b-a249-d43d39407fd9` |
| Federated Credential | `Github-crossvertise-nightwatchman-production-FederatedCredential`, Subject `repo:crossvertise/nightwatchman:environment:production` |
| RBAC | **Website Contributor**, nur auf die RG `nightwatchman-live` (Least Privilege) |

Im Repo sind dazu nur drei **Actions-Variablen** (keine Secrets) konfiguriert:
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

## Einmaliges Setup (bereits erledigt, zur Reproduktion)

```bash
# 1. Federated Credential am zentralen SP
az ad app federated-credential create \
  --id c1c157eb-d4c5-4288-b9b1-5615b9a4c832 \
  --parameters '{
    "name": "Github-crossvertise-nightwatchman-production-FederatedCredential",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:crossvertise/nightwatchman:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# 2. Deploy-Rechte nur auf die Resource Group
az role assignment create \
  --assignee 2f7e0cec-9d48-4379-8f7a-f5ca8e51c4d1 \
  --role "Website Contributor" \
  --scope /subscriptions/b6ba45e2-3816-44b0-ba54-78f080dc57d2/resourceGroups/nightwatchman-live

# 3. GitHub-Environment und Variablen
gh api -X PUT repos/crossvertise/nightwatchman/environments/production
gh variable set AZURE_CLIENT_ID       -R crossvertise/nightwatchman -b "c1c157eb-d4c5-4288-b9b1-5615b9a4c832"
gh variable set AZURE_TENANT_ID       -R crossvertise/nightwatchman -b "70470a10-c0d1-4c3b-a249-d43d39407fd9"
gh variable set AZURE_SUBSCRIPTION_ID -R crossvertise/nightwatchman -b "b6ba45e2-3816-44b0-ba54-78f080dc57d2"
```

Für ein weiteres Environment (z. B. Staging): neues FIC mit Subject
`repo:crossvertise/nightwatchman:environment:<name>`, Role Assignment auf die neue RG,
GitHub-Environment anlegen und den Workflow um das Environment erweitern.

## Troubleshooting

- **`AADSTS70021` / Login schlägt fehl:** FIC-Subject muss exakt
  `repo:crossvertise/nightwatchman:environment:production` sein und der Deploy-Job muss im
  GitHub-Environment `production` laufen.
- **`403` beim Deploy:** Role Assignment des SP auf der RG prüfen (`az role assignment list
  --assignee 2f7e0cec-9d48-4379-8f7a-f5ca8e51c4d1 --subscription b6ba45e2-…`).
- **HTTP 500.31 nach Deploy:** dürfte mit self-contained publish nicht auftreten; falls doch,
  prüfen ob wirklich das win-x86-Paket deployt wurde (32-Bit-Worker!).
