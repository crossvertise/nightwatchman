# Entra ID setup

Nightwatchman uses Microsoft Entra ID (Azure AD) for two independent purposes:

1. **Dashboard sign-in** (`AzureAd:*`) — an ordinary web app OIDC login for `/Dashboard` and
   friends.
2. **MCP authentication** (`McpAuth:*`) — OAuth 2.0 for the `/mcp` endpoint, using the
   "client ≠ resource" pattern described below.

None of the identifiers below are secrets by themselves — app/client ids and tenant ids are
not confidential. Treat only the App Service's `AzureAd__*`/`McpAuth__*` app settings as
configuration, not secrets to be pasted into the repo or into GitHub.

## Why client ≠ resource for MCP

A single app registration acting as both the OAuth client *and* the token audience (resource)
runs into `AADSTS90009` when the client tries to refresh its token — Entra ID does not allow a
public client to also be its own resource for a refresh grant. Nightwatchman's MCP auth
therefore uses **two** app registrations:

- a **public client** app that MCP clients (Claude Code, claude.ai, ...) authenticate as, and
- a separate **resource/API** app that the access token's audience (`aud` claim) actually is.

## App registrations

### 1. Web login app (dashboard)

Used for `AzureAd:ClientId`. A standard confidential web app registration for the MVC
dashboard's OIDC sign-in.

- Sign-in audience: single tenant (or as required by your organization)
- Redirect URI: `https://<your-host>/signin-oidc` (web platform)
- ID tokens enabled for the platform

```bash
az ad app create \
  --display-name "<app>-web" \
  --sign-in-audience AzureADMyOrg \
  --web-redirect-uris "https://<your-host>/signin-oidc" \
  --enable-id-token-issuance true
```

### 2. `<app>-mcp-server` — public client

Used for `McpAuth:ClientId`. This is the app MCP clients present themselves as when they go
through the OAuth proxy exposed by Nightwatchman itself (`/authorize`, `/token`, `/register`).

- Public client / fallback public client enabled (no client secret, since MCP clients like
  Claude Code, claude.ai, and Claude Desktop cannot hold one)
- Redirect URIs (public client / mobile & desktop platform):
  - `https://claude.ai/api/mcp/auth_callback`
  - `http://localhost`
  - `http://localhost/callback`

```bash
az ad app create \
  --display-name "<app>-mcp-server" \
  --sign-in-audience AzureADMyOrg \
  --is-fallback-public-client true \
  --public-client-redirect-uris \
      "https://claude.ai/api/mcp/auth_callback" \
      "http://localhost" \
      "http://localhost/callback"

az ad sp create --id <mcp-server-app-id>
```

### 3. `<app>-mcp-api` — resource / API

Used for `McpAuth:ApiClientId`. This app represents the protected resource — the audience of
the access token that `/mcp` validates.

- Application ID URI: `api://<mcp-api-app-id>`
- Exposes one delegated scope: `access_as_user`
- Pre-authorizes the `<app>-mcp-server` app for that scope, so users aren't shown a separate
  admin/user consent prompt for a second app during login

```bash
az ad app create \
  --display-name "<app>-mcp-api" \
  --sign-in-audience AzureADMyOrg

api_app_id=<resulting-appId>

az ad app update --id "$api_app_id" \
  --identifier-uris "api://$api_app_id"

# Add the access_as_user delegated scope and pre-authorize the server app.
# api.oauth2PermissionScopes and api.preAuthorizedApplications are set via
# Microsoft Graph, e.g. through `az rest` PATCHing the application object:
scope_id=$(uuidgen)
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/<api-app-object-id>" \
  --body '{
    "api": {
      "oauth2PermissionScopes": [{
        "id": "'"$scope_id"'",
        "adminConsentDisplayName": "Access as user",
        "adminConsentDescription": "Allows the app to access the API as the signed-in user.",
        "userConsentDisplayName": "Access as you",
        "userConsentDescription": "Allows the app to access the API on your behalf.",
        "value": "access_as_user",
        "type": "User",
        "isEnabled": true
      }],
      "preAuthorizedApplications": [{
        "appId": "<mcp-server-app-id>",
        "delegatedPermissionIds": ["'"$scope_id"'"]
      }]
    }
  }'

az ad sp create --id "$api_app_id"
```

If your account lacks permission to run the Graph PATCH above (`Insufficient privileges`), have
a tenant administrator run it, or configure the scope and pre-authorization manually in the
Entra portal (App registrations → `<app>-mcp-api` → Expose an API).

## Where each id/value goes

| Config key | Value |
|---|---|
| `AzureAd:Instance` | `https://login.microsoftonline.com/` |
| `AzureAd:TenantId` | Your tenant id (GUID), or `organizations`/`common` for multi-tenant |
| `AzureAd:ClientId` | App id of the web login app |
| `AzureAd:Domain` | Verified domain of your tenant |
| `AzureAd:CallbackPath` | `/signin-oidc` |
| `McpAuth:ClientId` | App id of `<app>-mcp-server` |
| `McpAuth:ApiClientId` | App id of `<app>-mcp-api` |
| `McpAuth:Scope` | Leave empty to default to `api://<ApiClientId>/access_as_user` |

Locally, set these via `dotnet user-secrets` (see [README.md](../README.md#configure-secrets));
in Azure, as App Service application settings with double-underscore section separators
(`AzureAd__TenantId`, `McpAuth__ClientId`, ...) — see [DEPLOYMENT.md](../DEPLOYMENT.md).

If `McpAuth:ClientId` or `AzureAd:TenantId` is left empty, `/mcp` fails closed — every request
is rejected with `401`, rather than falling back to an unauthenticated mode.
