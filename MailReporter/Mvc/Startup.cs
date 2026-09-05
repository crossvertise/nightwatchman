namespace Mvc
{
    using BusinessLogic;

    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Authorization;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Identity.Web;

    using ModelContextProtocol.AspNetCore;

    using Mvc.Mcp;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            var authenticationBuilder = services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme);
            authenticationBuilder.AddMicrosoftIdentityWebApp(Configuration.GetSection("AzureAd"));
            authenticationBuilder.AddMcpAuthentication(Configuration);

            services.AddMcpAuthorization(Configuration);

            services.AddHttpClient();

            services.AddMcpServer()
                .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
                .WithToolsFromAssembly()
                .WithPromptsFromAssembly();

            services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            }).AddNewtonsoftJson();

            services.RegisterServices();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            // Honour X-Forwarded-Proto from the hosting front end so the OAuth metadata
            // advertises https:// and not the internal http:// origin.
            app.Use((context, next) =>
            {
                var proto = context.Request.Headers["X-Forwarded-Proto"].ToString();
                if (!string.IsNullOrEmpty(proto))
                {
                    context.Request.Scheme = proto.Split(',')[0].Trim();
                }

                return next();
            });

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                // Anonymous OAuth metadata / proxy endpoints required by MCP clients.
                endpoints.MapMcpOAuthProxy(Configuration);

                // Minimal API endpoint — the global MVC AuthorizeFilter does not apply here,
                // so the MCP policy is attached explicitly.
                endpoints.MapMcp("/mcp").RequireAuthorization(McpAuthExtensions.PolicyName);
            });
        }
    }
}
