namespace BusinessLogic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.Functions;

    using DomainModel;
    using DomainModel.DTO;

    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Watches the Service Bus-triggered Azure Functions configured under <c>AzureFunctions</c>.
    /// </summary>
    /// <remarks>
    /// A function on the Consumption plan can stop without anyone noticing: its worker fails to
    /// start in time ("Exceeded language worker restart retry count") and the host stays dead, or the
    /// platform does not wake it up for new messages. Either way, messages wait and nothing is
    /// completed. That is what a tile turns red on.
    /// <para>
    /// The "waiting earlier" check is per queue or topic. On a topic with a subscription nobody reads
    /// (e.g. a "log" subscription), it is always true, so such a tile can briefly show stuck while
    /// the function starts up for the first message after a quiet period.
    /// </para>
    /// </remarks>
    public class FunctionHealthService : IFunctionHealthService
    {
        public const string NoNamespaceConfigured = "No Service Bus namespace configured";

        private readonly IServiceBusStatusClient _statusClient;
        private readonly IConfiguration _configuration;
        private readonly TimeProvider _timeProvider;

        public FunctionHealthService(IServiceBusStatusClient statusClient, IConfiguration configuration, TimeProvider timeProvider)
        {
            _statusClient = statusClient;
            _configuration = configuration;
            _timeProvider = timeProvider;
        }

        public async Task<IReadOnlyList<FunctionOverview>> GetOverview()
        {
            var options = _configuration.GetSection(AzureFunctionsOptions.SectionName).Get<AzureFunctionsOptions>() ?? new AzureFunctionsOptions();
            if (options.Monitors == null || options.Monitors.Count == 0)
            {
                return Array.Empty<FunctionOverview>();
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var overviews = await Task.WhenAll(options.Monitors.Select(m => GetOverview(m, options, now)));
            return overviews;
        }

        private async Task<FunctionOverview> GetOverview(FunctionMonitor monitor, AzureFunctionsOptions options, DateTime now)
        {
            var overview = new FunctionOverview { Monitor = monitor, WindowMinutes = options.StuckAfterMinutes };

            var namespaceResourceId = string.IsNullOrWhiteSpace(monitor.NamespaceResourceId)
                ? options.ServiceBusNamespaceResourceId
                : monitor.NamespaceResourceId;

            if (string.IsNullOrWhiteSpace(namespaceResourceId))
            {
                overview.Error = NoNamespaceConfigured;
                return overview;
            }

            try
            {
                var status = await _statusClient.GetStatus(
                    namespaceResourceId,
                    monitor,
                    now.AddMinutes(-options.StuckAfterMinutes),
                    now.AddMinutes(-options.GraceMinutes),
                    now);

                overview.WaitingMessages = status.ActiveMessages;
                overview.DeadLetteredMessages = status.DeadLetterMessages;
                overview.CompletedMessages = status.CompletedMessages;
                overview.IsStuck = status.ActiveMessages > 0 && status.ActiveMessagesEarlier > 0 && status.CompletedMessages == 0;
            }
            catch (Exception ex)
            {
                overview.Error = ex.Message;
            }

            return overview;
        }
    }
}
