namespace BusinessLogic.Implementations
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.Alerting;

    using Microsoft.Extensions.Configuration;

    using Newtonsoft.Json;

    /// <summary>
    /// Posts alerts to a Microsoft Teams channel through an incoming-webhook URL.
    /// </summary>
    /// <remarks>
    /// The body is <c>{ "text": "..." }</c>, which both a Power Automate "when a Teams webhook request
    /// is received" flow and a classic Office 365 connector accept. Prefer the Power Automate flow:
    /// the classic connectors are being retired.
    /// </remarks>
    public class TeamsWebhookNotifier : IAlertNotifier
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public TeamsWebhookNotifier(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<bool> Send(string text, CancellationToken cancellationToken)
        {
            var options = _configuration.GetSection(AlertingOptions.SectionName).Get<AlertingOptions>() ?? new AlertingOptions();

            if (string.IsNullOrWhiteSpace(options.TeamsWebhookUrl))
            {
                // Unconfigured is not an error: a dev machine or a fresh environment simply does not post.
                return false;
            }

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RequestTimeout);

                var payload = JsonConvert.SerializeObject(new { text });
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                using var response = await client.PostAsync(options.TeamsWebhookUrl, content, timeout.Token);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                // Swallowed on purpose: a webhook outage must not take the monitoring loop down with it.
                // Returning false leaves the problem unannounced, so the next tick retries.
                return false;
            }
        }
    }
}
