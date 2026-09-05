namespace Mvc.Mcp
{
    using System;

    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Entra ID settings for the MCP endpoint: the tenant comes from <c>AzureAd:TenantId</c>,
    /// everything else from the <c>McpAuth</c> section.
    /// </summary>
    public class McpAuthOptions
    {
        public string TenantId { get; private set; } = string.Empty;

        /// <summary>App registration the MCP client logs in with (public client).</summary>
        public string ClientId { get; private set; } = string.Empty;

        /// <summary>App registration that exposes the API scope; falls back to <see cref="ClientId"/>.</summary>
        public string ApiClientId { get; private set; } = string.Empty;

        /// <summary>Full delegated scope, e.g. <c>api://{ApiClientId}/access_as_user</c>.</summary>
        public string Scope { get; private set; } = string.Empty;

        /// <summary>Bare scope name as it appears in the <c>scp</c> claim, e.g. <c>access_as_user</c>.</summary>
        public string RequiredScopeName => Scope.Substring(Scope.LastIndexOf('/') + 1);

        /// <summary>False when tenant or client id are missing — the endpoint then rejects everything.</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);

        public static McpAuthOptions Read(IConfiguration configuration)
        {
            var mcpAuth = configuration.GetSection("McpAuth");
            var clientId = mcpAuth["ClientId"] ?? string.Empty;
            var apiClientId = string.IsNullOrWhiteSpace(mcpAuth["ApiClientId"]) ? clientId : mcpAuth["ApiClientId"];
            var scope = string.IsNullOrWhiteSpace(mcpAuth["Scope"]) ? $"api://{apiClientId}/access_as_user" : mcpAuth["Scope"];

            return new McpAuthOptions
            {
                TenantId = configuration.GetSection("AzureAd")["TenantId"] ?? string.Empty,
                ClientId = clientId,
                ApiClientId = apiClientId,
                Scope = scope,
            };
        }
    }
}
