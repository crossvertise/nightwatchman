namespace Mvc.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// OAuth 2.0 proxy endpoints — they forward the real flow to Entra ID. MCP clients expect
    /// the MCP server itself to advertise OIDC metadata and to host <c>/authorize</c> and
    /// <c>/token</c>. We rewrite the scope (clients ask for the bare client id, Entra wants the
    /// named delegated scope), always inject <c>offline_access</c> so a refresh token is issued,
    /// and accept dynamic client registration by echoing our own client id.
    /// All endpoints are anonymous on purpose; they carry no Nightwatchman data.
    /// </summary>
    public static class McpOAuthProxyEndpoints
    {
        public static IEndpointRouteBuilder MapMcpOAuthProxy(this IEndpointRouteBuilder endpoints, IConfiguration configuration)
        {
            var options = McpAuthOptions.Read(configuration);
            var tenantId = options.TenantId;
            var clientId = options.ClientId;
            var entraScope = options.Scope;

            var defaultScopeRegex = new Regex($@"(?:api://)?{Regex.Escape(clientId)}/\.default", RegexOptions.IgnoreCase);

            string NormaliseScope(string scope)
            {
                scope = string.IsNullOrEmpty(clientId) ? scope : defaultScopeRegex.Replace(scope, entraScope);
                if (!scope.Contains(entraScope, StringComparison.OrdinalIgnoreCase))
                {
                    scope = $"{scope} {entraScope}".Trim();
                }

                if (!scope.Contains("offline_access", StringComparison.OrdinalIgnoreCase))
                {
                    scope = $"{scope} offline_access".Trim();
                }

                return scope;
            }

            object BuildOidcMetadata(HttpContext ctx)
            {
                var origin = Origin(ctx);
                return new
                {
                    issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0",
                    authorization_endpoint = $"{origin}/authorize",
                    token_endpoint = $"{origin}/token",
                    registration_endpoint = $"{origin}/register",
                    jwks_uri = $"https://login.microsoftonline.com/{tenantId}/discovery/v2.0/keys",
                    response_types_supported = new[] { "code" },
                    grant_types_supported = new[] { "authorization_code", "refresh_token" },
                    code_challenge_methods_supported = new[] { "S256", "plain" },
                    scopes_supported = new[] { "openid", "profile", "offline_access", entraScope },
                    token_endpoint_auth_methods_supported = new[] { "none", "client_secret_post" },
                    response_modes_supported = new[] { "query", "fragment", "form_post" },
                };
            }

            endpoints.MapGet("/.well-known/openid-configuration", (HttpContext ctx) => Results.Json(BuildOidcMetadata(ctx)));
            endpoints.MapGet("/.well-known/oauth-authorization-server", (HttpContext ctx) => Results.Json(BuildOidcMetadata(ctx)));

            endpoints.MapGet("/.well-known/oauth-protected-resource", (HttpContext ctx) =>
            {
                var origin = Origin(ctx);
                return Results.Json(new
                {
                    resource = $"{origin}/mcp",
                    authorization_servers = new[] { origin },
                    scopes_supported = new[] { entraScope },
                    bearer_methods_supported = new[] { "header" },
                });
            });

            endpoints.MapGet("/authorize", (HttpContext ctx) =>
            {
                var qs = new List<string>();
                var sawScope = false;

                foreach (var kv in ctx.Request.Query)
                {
                    // The resource parameter is not understood by Entra; prompt/claims are set by us below.
                    if (string.Equals(kv.Key, "resource", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(kv.Key, "prompt", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(kv.Key, "claims", StringComparison.OrdinalIgnoreCase)) continue;

                    if (string.Equals(kv.Key, "scope", StringComparison.OrdinalIgnoreCase))
                    {
                        sawScope = true;
                        qs.Add($"scope={Uri.EscapeDataString(NormaliseScope(kv.Value.ToString()))}");
                        continue;
                    }

                    qs.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value.ToString())}");
                }

                if (!sawScope)
                {
                    qs.Add($"scope={Uri.EscapeDataString($"openid profile offline_access {entraScope}")}");
                }

                // Request Conditional Access auth-context "c1": Entra reuses the SSO session and
                // only steps up MFA when stale, instead of minting a stale-MFA code that fails
                // token redemption (AADSTS50078).
                qs.Add($"claims={Uri.EscapeDataString("{\"access_token\":{\"acrs\":{\"essential\":true,\"value\":\"c1\"}}}")}");

                return Results.Redirect($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize?{string.Join('&', qs)}");
            });

            endpoints.MapPost("/token", async (HttpContext ctx, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger(typeof(McpOAuthProxyEndpoints));
                var form = await ctx.Request.ReadFormAsync();
                var dict = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
                if (dict.TryGetValue("scope", out var scope))
                {
                    dict["scope"] = NormaliseScope(scope);
                }

                dict.Remove("resource");

                using var client = httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token")
                {
                    Content = new FormUrlEncodedContent(dict),
                };

                using var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                dict.TryGetValue("grant_type", out var grant);
                var grantType = string.IsNullOrEmpty(grant) ? "(none)" : grant;

                // Log the grant type and status only — never tokens or form values.
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("Token exchange: grant={GrantType} status={Status}", grantType, (int)response.StatusCode);
                }
                else
                {
                    logger.LogWarning("Token exchange FAILED: grant={GrantType} status={Status}", grantType, (int)response.StatusCode);
                }

                return Results.Content(body, "application/json", Encoding.UTF8, (int)response.StatusCode);
            });

            endpoints.MapPost("/register", async (HttpRequest request) =>
            {
                string[] redirectUris = null;
                try
                {
                    using var document = await JsonDocument.ParseAsync(request.Body);
                    if (document.RootElement.TryGetProperty("redirect_uris", out var uris))
                    {
                        redirectUris = uris.EnumerateArray().Select(u => u.GetString()).Where(u => u != null).ToArray();
                    }
                }
                catch (JsonException)
                {
                    // Malformed or empty body — dynamic client registration is a no-op anyway.
                }

                return Results.Json(new
                {
                    client_id = clientId,
                    client_id_issued_at = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    client_secret_expires_at = 0,
                    redirect_uris = redirectUris ?? Array.Empty<string>(),
                    token_endpoint_auth_method = "none",
                    grant_types = new[] { "authorization_code", "refresh_token" },
                    response_types = new[] { "code" },
                    scope = entraScope,
                    application_type = "native",
                });
            });

            return endpoints;
        }

        private static string Origin(HttpContext ctx) => $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    }
}
