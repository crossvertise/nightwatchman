namespace BusinessLogic.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using BusinessLogic.Models.Http;

    using DomainModel.DTO.Report;

    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Reads the findings out of a health endpoint's response body and turns them into short lines
    /// for the dashboard tile and the Teams alert.
    /// </summary>
    public static class HealthIssueReader
    {
        /// <summary>Lines shown per tile or alert; the rest is summarised as "… and N more".</summary>
        public const int MaxLines = 5;

        // The body is stored cut off at HttpHealthPoller.MaxErrorSummaryLength, so parsing it as one
        // JSON document fails exactly when there are many findings. Each finding is a flat object,
        // so they are matched one by one and a cut-off last one is simply skipped.
        private static readonly Regex FlatObject = new Regex(@"\{[^{}]*\}", RegexOptions.Compiled);

        public static IReadOnlyList<HealthIssue> Parse(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return Array.Empty<HealthIssue>();
            }

            var issues = new List<HealthIssue>();
            foreach (Match match in FlatObject.Matches(body))
            {
                JObject item;
                try
                {
                    item = JObject.Parse(match.Value);
                }
                catch (Exception)
                {
                    continue;
                }

                var reason = Text(item, "reason");
                if (string.IsNullOrWhiteSpace(reason))
                {
                    continue;
                }

                issues.Add(new HealthIssue
                {
                    Reason = reason,
                    PaymentId = Number(item, "paymentId"),
                    OrderId = Number(item, "orderId"),
                    Provider = Text(item, "provider"),
                    SinceUtc = Date(item, "sinceUtc"),
                });
            }

            return issues;
        }

        /// <summary>
        /// Base address of the polled host (e.g. <c>https://staging.crossvertise.com</c>) for the
        /// monitor that writes to <paramref name="jobName"/>, so order numbers can link to its manager
        /// pages. Null when the job is not an HTTP monitor.
        /// </summary>
        public static string HostFor(HttpHealthOptions options, string jobName)
        {
            var monitor = options?.Monitors?.FirstOrDefault(m =>
                string.Equals(m.TargetJobName, jobName, StringComparison.OrdinalIgnoreCase));

            return monitor != null && Uri.TryCreate(monitor.Url, UriKind.Absolute, out var uri)
                ? $"{uri.Scheme}://{uri.Authority}"
                : null;
        }

        public static string OrderUrl(string host, int orderId) =>
            $"{host}/de-de/manager/ordersales/edit/{orderId.ToString(CultureInfo.InvariantCulture)}";

        /// <summary>
        /// "technical failure · CreditCard · payment 8629 · order 60172 · since 06.11. 18:52 UTC".
        /// With <paramref name="markdown"/> the order number becomes a link (Teams).
        /// </summary>
        public static string Describe(HealthIssue issue, string host, bool markdown)
        {
            var parts = new List<string> { DescribeReason(issue.Reason) };

            if (!string.IsNullOrWhiteSpace(issue.Provider))
            {
                parts.Add(issue.Provider);
            }

            if (issue.PaymentId.HasValue)
            {
                parts.Add($"payment {issue.PaymentId.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            if (issue.OrderId.HasValue)
            {
                var order = issue.OrderId.Value.ToString(CultureInfo.InvariantCulture);
                parts.Add(markdown && host != null
                    ? $"order [{order}]({OrderUrl(host, issue.OrderId.Value)})"
                    : $"order {order}");
            }

            if (issue.SinceUtc.HasValue)
            {
                parts.Add($"since {issue.SinceUtc.Value.ToString("dd.MM. HH:mm", CultureInfo.InvariantCulture)} UTC");
            }

            return string.Join(" · ", parts);
        }

        public static string DescribeReason(string reason) => reason switch
        {
            "StuckFunds" => "authorized, order still Draft",
            "TechnicalFailure" => "technical failure",
            "CaptureFailure" => "capture failed",
            _ => reason,
        };

        private static string Text(JObject item, string name)
        {
            var token = item.GetValue(name, StringComparison.OrdinalIgnoreCase);
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private static int? Number(JObject item, string name)
        {
            var token = item.GetValue(name, StringComparison.OrdinalIgnoreCase);
            return token != null && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static DateTime? Date(JObject item, string name)
        {
            var token = item.GetValue(name, StringComparison.OrdinalIgnoreCase);
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Date)
            {
                return DateTime.SpecifyKind(token.Value<DateTime>(), DateTimeKind.Utc);
            }

            return DateTime.TryParse(token.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value)
                ? value
                : null;
        }
    }
}
