# Deployment

Nightwatchman is deployed via GitHub Actions
([`.github/workflows/deploy.yml`](.github/workflows/deploy.yml)) to an Azure App Service
(Windows).

## Flow

- **Trigger:** push to `master` with changes under `MailReporter/**` (or to the workflow
  itself), plus manual runs via `workflow_dispatch`.
- **Build job:** restore, build, and the full test suite (unit + integration, see
  [CLAUDE.md](CLAUDE.md)) as a gate. Then `dotnet publish` **self-contained for `win-x86`** —
  the target App Service plan is a Windows 32-bit worker, and publishing self-contained makes
  the deployment independent of whatever .NET runtime happens to be installed on the App
  Service (the portal's "Stack settings" become irrelevant).
- **Deploy job:** runs in the GitHub environment `production`, deploys via
  `azure/webapps-deploy@v3`, then checks the anonymous health check
  `HEAD /MailReporter/Mandrill` as a smoke test.

## Authentication: OIDC / Federated Identity Credentials

The workflow authenticates against Azure **without secrets**, via a federated credential on an
existing App Registration (a "GitHub Actions service principal") that is bound to the GitHub
environment `production`.

Required **repository variables** (Settings → Secrets and variables → Actions → Variables — not
secrets):

| Variable | Description |
|---|---|
| `AZURE_CLIENT_ID` | App registration (client id) used for OIDC login |
| `AZURE_TENANT_ID` | Azure AD tenant id |
| `AZURE_SUBSCRIPTION_ID` | Subscription containing the target App Service |
| `AZURE_WEBAPP_NAME` | Name of the target App Service |
| `AZURE_WEBAPP_URL` | Public URL of the App Service, used for the environment link and the smoke test |

## App settings on the App Service

The application's configuration is supplied as **App Service application settings** (not
checked into the repo, not GitHub secrets). Nested configuration sections use the `Key__SubKey`
naming convention:

| Setting | Purpose |
|---|---|
| `MongoDbConnectionString` | MongoDB connection string |
| `MongoDbDatabaseName` | Database name |
| `MandrillWebhookKey` | HMAC signing key for the Mandrill webhook |
| `BasicAuthCredentials` | `user:password` for the PRTG endpoint |
| `SuccessWords` | Fallback success-classification word list |
| `ErrorWords` | Fallback error-classification word list |
| `AzureAd__TenantId` | Entra tenant id, for the dashboard sign-in |
| `AzureAd__ClientId` | App registration for the dashboard sign-in (OIDC) |
| `AzureAd__Domain` | Verified domain of the Entra tenant |
| `McpAuth__ClientId` | Public-client app registration for MCP clients |
| `McpAuth__ApiClientId` | Resource/API app registration for the `/mcp` token audience |

See [docs/entra-setup.md](docs/entra-setup.md) for how to create the underlying Entra app
registrations, and [README.md](README.md#configuration) for the full configuration reference.

## One-time setup (reference)

Adjust the placeholders (`<...>`) to your environment.

```bash
# 1. Federated credential on the GitHub Actions service principal
az ad app federated-credential create \
  --id <github-actions-sp-app-id> \
  --parameters '{
    "name": "<descriptive-name>",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<org>/<repo>:environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# 2. Deploy permissions, scoped to the resource group containing the App Service
az role assignment create \
  --assignee <github-actions-sp-object-id> \
  --role "Website Contributor" \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>

# 3. GitHub environment and repository variables
gh api -X PUT repos/<org>/<repo>/environments/production
gh variable set AZURE_CLIENT_ID       -R <org>/<repo> -b "<client-id>"
gh variable set AZURE_TENANT_ID       -R <org>/<repo> -b "<tenant-id>"
gh variable set AZURE_SUBSCRIPTION_ID -R <org>/<repo> -b "<subscription-id>"
gh variable set AZURE_WEBAPP_NAME     -R <org>/<repo> -b "<app-service-name>"
gh variable set AZURE_WEBAPP_URL      -R <org>/<repo> -b "https://<app-service-name>.azurewebsites.net"
```

For an additional environment (e.g. staging): create a new federated credential with subject
`repo:<org>/<repo>:environment:<name>`, a role assignment scoped to the corresponding resource
group, a matching GitHub environment, and extend the workflow to target it.

## Troubleshooting

- **`AADSTS70021` / login fails:** the federated credential's subject must exactly match
  `repo:<org>/<repo>:environment:production`, and the deploy job must actually run in the
  GitHub environment `production`.
- **`403` on deploy:** check the service principal's role assignment on the target resource
  group (`az role assignment list --assignee <object-id> --subscription <subscription-id>`).
- **HTTP 500.31 after deploy:** shouldn't occur with a self-contained publish; if it does,
  verify the deployed package is actually the `win-x86` build (the App Service plan is a
  32-bit worker).
- **Smoke test fails / times out:** confirm `AZURE_WEBAPP_URL` points at the right host and
  that `MailReporter/Mandrill` is reachable anonymously (no auth in front of it at the
  infrastructure level).
