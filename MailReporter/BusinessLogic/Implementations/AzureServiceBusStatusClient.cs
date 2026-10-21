namespace BusinessLogic.Implementations
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    using Azure.Core;

    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.Functions;

    using DomainModel;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Reads Service Bus state through Azure Resource Manager: the entity itself for the current
    /// counts, Azure Monitor for the history. Read-only; the app's identity needs the Reader role on
    /// the Service Bus namespace.
    /// </summary>
    public class AzureServiceBusStatusClient : IServiceBusStatusClient
    {
        public const string ArmBaseUrl = "https://management.azure.com";
        public const string ArmScope = "https://management.azure.com/.default";
        public const string EntityApiVersion = "2021-11-01";
        public const string MetricsApiVersion = "2023-10-01";

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TokenCredential _credential;

        public AzureServiceBusStatusClient(IHttpClientFactory httpClientFactory, TokenCredential credential)
        {
            _httpClientFactory = httpClientFactory;
            _credential = credential;
        }

        public async Task<ServiceBusEntityStatus> GetStatus(string namespaceResourceId, FunctionMonitor monitor, DateTime from, DateTime earlierUntil, DateTime to)
        {
            using var timeout = new CancellationTokenSource(RequestTimeout);
            var token = await _credential.GetTokenAsync(new TokenRequestContext(new[] { ArmScope }), timeout.Token);

            var entity = await GetJson(EntityUrl(namespaceResourceId, monitor), token.Token, timeout.Token);
            var metrics = await GetJson(MetricsUrl(namespaceResourceId, monitor, from, to), token.Token, timeout.Token);

            var counts = entity["properties"]?["countDetails"];

            return new ServiceBusEntityStatus
            {
                ActiveMessages = counts?.Value<long?>("activeMessageCount") ?? 0,
                DeadLetterMessages = counts?.Value<long?>("deadLetterMessageCount") ?? 0,
                CompletedMessages = (long)Points(metrics, "CompleteMessage").Sum(p => p.Value<double?>("total") ?? 0),
                ActiveMessagesEarlier = (long)Points(metrics, "ActiveMessages")
                    .Where(p => p.Value<DateTime>("timeStamp").ToUniversalTime() <= earlierUntil)
                    .Select(p => p.Value<double?>("maximum") ?? 0)
                    .DefaultIfEmpty(0)
                    .Max(),
            };
        }

        public static string EntityUrl(string namespaceResourceId, FunctionMonitor monitor)
        {
            var path = string.IsNullOrWhiteSpace(monitor.Queue)
                ? $"/topics/{Uri.EscapeDataString(monitor.Topic)}/subscriptions/{Uri.EscapeDataString(monitor.Subscription)}"
                : $"/queues/{Uri.EscapeDataString(monitor.Queue)}";

            return $"{ArmBaseUrl}{namespaceResourceId.TrimEnd('/')}{path}?api-version={EntityApiVersion}";
        }

        public static string MetricsUrl(string namespaceResourceId, FunctionMonitor monitor, DateTime from, DateTime to)
        {
            var timespan = $"{from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}/{to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)}";
            var filter = $"EntityName eq '{monitor.MetricsEntityName}'";

            return $"{ArmBaseUrl}{namespaceResourceId.TrimEnd('/')}/providers/Microsoft.Insights/metrics"
                + $"?api-version={MetricsApiVersion}"
                + "&metricnames=ActiveMessages,CompleteMessage"
                + "&aggregation=Maximum,Total"
                + "&interval=PT1M"
                + $"&timespan={Uri.EscapeDataString(timespan)}"
                + $"&$filter={Uri.EscapeDataString(filter)}";
        }

        private static JToken[] Points(JObject metrics, string metricName) =>
            (metrics["value"] as JArray ?? new JArray())
                .Where(m => string.Equals(m["name"]?.Value<string>("value"), metricName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(m => m["timeseries"] as JArray ?? new JArray())
                .SelectMany(t => t["data"] as JArray ?? new JArray())
                .ToArray();

        private async Task<JObject> GetJson(string url, string accessToken, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = ErrorMessage(body);
                throw new HttpRequestException($"Azure answered {(int)response.StatusCode} {response.ReasonPhrase}{(message == null ? string.Empty : ": " + message)}");
            }

            return JObject.Parse(body);
        }

        private static string ErrorMessage(string body)
        {
            try
            {
                return JObject.Parse(body)["error"]?.Value<string>("message");
            }
            catch (JsonReaderException)
            {
                return null;
            }
        }
    }
}
