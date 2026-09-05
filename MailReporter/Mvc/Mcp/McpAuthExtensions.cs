namespace Mvc.Mcp
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.IdentityModel.Tokens;

    /// <summary>
    /// Bearer authentication for the MCP endpoint. Runs as its own scheme next to the
    /// OpenID Connect / cookie default that the MVC UI uses.
    /// </summary>
    public static class McpAuthExtensions
    {
        /// <summary>Name of the JWT bearer scheme protecting <c>/mcp</c>.</summary>
        public const string SchemeName = "McpBearer";

        /// <summary>Name of the authorization policy protecting <c>/mcp</c>.</summary>
        public const string PolicyName = "Mcp";

        /// <summary>Realm advertised in the <c>WWW-Authenticate</c> challenge.</summary>
        public const string Realm = "nightwatchman-mcp";

        private const string ScopeClaim = "scp";
        private const string ScopeClaimUri = "http://schemas.microsoft.com/identity/claims/scope";

        public static AuthenticationBuilder AddMcpAuthentication(this AuthenticationBuilder builder, IConfiguration configuration)
        {
            var options = McpAuthOptions.Read(configuration);

            return builder.AddJwtBearer(SchemeName, o =>
            {
                // Authority is only set when a tenant is configured — otherwise the handler
                // would try to download OIDC metadata from an invalid URL at first use.
                o.Authority = options.IsConfigured ? $"https://login.microsoftonline.com/{options.TenantId}/v2.0" : null;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudiences = new[]
                    {
                        options.ClientId,
                        $"api://{options.ClientId}",
                        options.ApiClientId,
                        $"api://{options.ApiClientId}",
                    },
                    ValidIssuers = options.IsConfigured
                        ? new[]
                        {
                            $"https://login.microsoftonline.com/{options.TenantId}/v2.0",
                            $"https://sts.windows.net/{options.TenantId}/",
                        }
                        : null,
                };

                o.Events = new JwtBearerEvents
                {
                    // Fail closed: without a configured tenant / client id no token can ever
                    // be trusted, so reject every request instead of falling back to anonymous.
                    OnMessageReceived = ctx =>
                    {
                        if (!options.IsConfigured)
                        {
                            ctx.Fail("MCP authentication is not configured (AzureAd:TenantId / McpAuth:ClientId are empty).");
                        }

                        return Task.CompletedTask;
                    },

                    // MCP spec: the 401 must carry a WWW-Authenticate header pointing the client
                    // at the protected-resource metadata so it can discover the authorization
                    // server. Without this, Claude's OAuth bootstrap fails.
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        var origin = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                        ctx.Response.StatusCode = 401;
                        ctx.Response.Headers.Append(
                            "WWW-Authenticate",
                            $"Bearer realm=\"{Realm}\", resource_metadata=\"{origin}/.well-known/oauth-protected-resource\"");
                        return Task.CompletedTask;
                    },
                };
            });
        }

        public static IServiceCollection AddMcpAuthorization(this IServiceCollection services, IConfiguration configuration)
        {
            var requiredScope = McpAuthOptions.Read(configuration).RequiredScopeName;

            return services.AddAuthorization(o => o.AddPolicy(PolicyName, p => p
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(SchemeName)
                .RequireAssertion(ctx => ctx.User.Claims
                    .Where(c => c.Type == ScopeClaim || c.Type == ScopeClaimUri)
                    .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Any(s => string.Equals(s, requiredScope, StringComparison.OrdinalIgnoreCase)))));
        }
    }
}
