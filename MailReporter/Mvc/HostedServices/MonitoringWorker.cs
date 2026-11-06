namespace Mvc.HostedServices
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.Alerting;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// The loop behind the push half of Nightwatchman: every tick it polls the configured health
    /// endpoints, then announces any tile that changed state to Teams.
    /// </summary>
    /// <remarks>
    /// <b>This needs Always On.</b> Without it the App Service unloads when idle and the loop simply
    /// stops — silently, because a monitor that is not running raises nothing. The polled jobs have an
    /// <c>ExpectedInterval</c> so a dead poller eventually shows up as an overdue tile, but only an
    /// external check of the site itself (the PRTG poll of <c>/Dashboard/Prtg</c>) catches a dead app.
    /// </remarks>
    public class MonitoringWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MonitoringWorker> _logger;

        public MonitoringWorker(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<MonitoringWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = _configuration.GetSection(AlertingOptions.SectionName).Get<AlertingOptions>() ?? new AlertingOptions();

            if (!options.Enabled)
            {
                _logger.LogInformation("Monitoring worker is disabled (Alerting:Enabled is false).");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, options.IntervalMinutes));
            _logger.LogInformation("Monitoring worker started; evaluating every {Interval}.", interval);

            using var timer = new PeriodicTimer(interval);

            do
            {
                await Tick(stoppingToken);
            }
            while (await WaitForNextTick(timer, stoppingToken));
        }

        private static async Task<bool> WaitForNextTick(PeriodicTimer timer, CancellationToken stoppingToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task Tick(CancellationToken stoppingToken)
        {
            // Every tick is isolated: a failing poll, an unreachable webhook or a Mongo blip must cost
            // one cycle, never the loop. A dead loop is the one failure this app cannot report.
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var poller = scope.ServiceProvider.GetRequiredService<IHttpHealthPoller>();
                var polls = await poller.PollAll(stoppingToken);

                foreach (var skipped in polls.Where(p => !p.Recorded))
                {
                    _logger.LogWarning(
                        "Health monitor {Monitor} recorded nothing: {Reason}.",
                        skipped.MonitorName,
                        skipped.SkippedReason);
                }

                var evaluator = scope.ServiceProvider.GetRequiredService<IAlertEvaluator>();
                var summary = await evaluator.Run(stoppingToken);

                if (summary.Alerted > 0 || summary.Recovered > 0 || summary.FailedToSend > 0)
                {
                    _logger.LogInformation(
                        "Monitoring tick: {Problems} problem(s), {Alerted} announced, {Recovered} recovered, {Deferred} deferred, {FailedToSend} undelivered.",
                        summary.Problems,
                        summary.Alerted,
                        summary.Recovered,
                        summary.Deferred,
                        summary.FailedToSend);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monitoring tick failed; the loop continues.");
            }
        }
    }
}
