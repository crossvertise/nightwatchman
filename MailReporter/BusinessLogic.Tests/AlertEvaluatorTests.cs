using BusinessLogic.Implementations;
using BusinessLogic.Interfaces;
using BusinessLogic.Models.Alerting;

using DomainModel;
using DomainModel.Alerting;
using DomainModel.DTO;
using DomainModel.DTO.Report;

using Microsoft.Extensions.Configuration;

using Moq;

using NUnit.Framework;

using Repos;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    /// <summary>
    /// The alerting state machine: announce once, stay quiet, say when it is over, and speak up again
    /// if it comes back. Everything here is about what does NOT get posted — a monitor that repeats
    /// itself every ten minutes stops being read, which is the same as having no monitor.
    /// </summary>
    [TestFixture]
    public class AlertEvaluatorTests
    {
        private const string PaymentJob = "Payment Health Live";
        private const string BatchJob = "OOH ETL Live";

        private Mock<IJobStatusReportService> _reportService = null!;
        private Mock<IFunctionHealthService> _functionHealth = null!;
        private Mock<IAlertStateRepo> _alertStateRepo = null!;
        private Mock<IAlertNotifier> _notifier = null!;
        private List<AlertState> _stored = null!;

        [SetUp]
        public void Setup()
        {
            _reportService = new Mock<IJobStatusReportService>();
            _functionHealth = new Mock<IFunctionHealthService>();
            _alertStateRepo = new Mock<IAlertStateRepo>();
            _notifier = new Mock<IAlertNotifier>();
            _stored = new List<AlertState>();

            _reportService.Setup(s => s.GetHealthSummary(It.IsAny<int>())).ReturnsAsync(new HealthSummary());
            _functionHealth.Setup(s => s.GetOverview()).ReturnsAsync(Array.Empty<FunctionOverview>());
            _alertStateRepo.Setup(r => r.GetAll()).ReturnsAsync(() => _stored);
            _alertStateRepo.Setup(r => r.Create(It.IsAny<AlertState>()))
                .Callback<AlertState>(s => { s.Id ??= Guid.NewGuid().ToString(); _stored.Add(s); })
                .Returns(Task.CompletedTask);
            _alertStateRepo.Setup(r => r.Update(It.IsAny<AlertState>())).Returns(Task.CompletedTask);
            _alertStateRepo.Setup(r => r.Delete(It.IsAny<string>()))
                .Callback<string>(id => _stored.RemoveAll(s => s.Id == id))
                .Returns(Task.CompletedTask);
            _notifier.Setup(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        [Test]
        public async Task A_new_problem_is_announced_once()
        {
            Problems(Problem(PaymentJob, "Failed", "3 x authorized but order still Draft"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Alerted, Is.EqualTo(1));
            _notifier.Verify(n => n.Send(It.Is<string>(t => t.Contains(PaymentJob) && t.Contains("Failed")), It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_stored.Single().AlertedAtUtc, Is.Not.Null);
        }

        [Test]
        public async Task A_problem_that_is_still_broken_says_nothing()
        {
            _stored.Add(Alerted(PaymentJob, "Failed"));
            Problems(Problem(PaymentJob, "Failed", "still broken"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Alerted, Is.EqualTo(0));
            _notifier.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task A_problem_that_clears_is_reported_as_recovered_and_forgotten()
        {
            _stored.Add(Alerted(PaymentJob, "Failed"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Recovered, Is.EqualTo(1));
            _notifier.Verify(n => n.Send(It.Is<string>(t => t.Contains("green again")), It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(_stored, Is.Empty, "the row must go, otherwise a recurrence would stay silent");
        }

        [Test]
        public async Task The_same_problem_coming_back_is_announced_again()
        {
            // Recovery cleared the row; the second outage is a new incident and must be heard.
            Problems(Problem(PaymentJob, "Failed", "back again"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Alerted, Is.EqualTo(1));
        }

        [Test]
        public async Task A_problem_that_recovers_before_it_was_ever_announced_passes_in_silence()
        {
            _stored.Add(Pending(BatchJob, "Overdue"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Recovered, Is.EqualTo(0));
            _notifier.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(_stored, Is.Empty);
        }

        // ---------- quiet hours ----------

        [Test]
        public async Task A_batch_job_breaking_at_night_waits_for_the_morning()
        {
            Problems(Problem(BatchJob, "Overdue", "no mail since 22:20"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Deferred, Is.EqualTo(1));
            _notifier.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(_stored.Single().AlertedAtUtc, Is.Null, "it is remembered, just not announced yet");
        }

        [Test]
        public async Task The_same_batch_job_is_announced_once_the_working_day_starts()
        {
            _stored.Add(Pending(BatchJob, "Overdue"));
            Problems(Problem(BatchJob, "Overdue", "no mail since 22:20"));

            var summary = await Run(AtUtcHour(9));

            Assert.That(summary.Alerted, Is.EqualTo(1));
            Assert.That(_stored.Single().AlertedAtUtc, Is.Not.Null);
        }

        [Test]
        public async Task Payments_are_announced_at_any_hour()
        {
            Problems(Problem(PaymentJob, "Failed", "money held on a draft order"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Alerted, Is.EqualTo(1));
        }

        // ---------- queues ----------

        [Test]
        public async Task A_stuck_queue_is_announced_immediately_even_though_the_MCP_cannot_see_it()
        {
            _functionHealth.Setup(s => s.GetOverview()).ReturnsAsync(new[]
            {
                new FunctionOverview
                {
                    Monitor = new FunctionMonitor { Name = "Mailer", Queue = "xv-mailer-queue" },
                    IsStuck = true,
                    WaitingMessages = 42,
                    CompletedMessages = 0,
                    WindowMinutes = 30,
                },
            });

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Alerted, Is.EqualTo(1));
            _notifier.Verify(n => n.Send(It.Is<string>(t => t.Contains("Mailer")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task A_queue_that_cannot_be_read_is_not_treated_as_broken()
        {
            // A failed ARM read says nothing about the function; alerting on it would cry wolf.
            _functionHealth.Setup(s => s.GetOverview()).ReturnsAsync(new[]
            {
                new FunctionOverview
                {
                    Monitor = new FunctionMonitor { Name = "Mailer", Queue = "xv-mailer-queue" },
                    IsStuck = false,
                    Error = "403 from ARM",
                },
            });

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Problems, Is.EqualTo(0));
            _notifier.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ---------- alert text ----------

        [Test]
        public async Task A_payment_alert_lists_the_flagged_payments_with_a_link_to_the_order()
        {
            string? sent = null;
            _notifier.Setup(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((text, _) => sent = text)
                .ReturnsAsync(true);

            var problem = Problem(PaymentJob, "Failed", "1 x technical authorization failure");
            problem.Issues.Add(new HealthIssue
            {
                Reason = "TechnicalFailure",
                PaymentId = 8629,
                OrderId = 60172,
                Provider = "CreditCard",
                SinceUtc = new DateTime(2026, 11, 6, 18, 52, 21, DateTimeKind.Utc),
            });
            Problems(problem);

            await Run(AtUtcHour(3));

            Assert.That(sent, Does.Contain("1 x technical authorization failure"));
            Assert.That(sent, Does.Contain(
                "• technical failure · CreditCard · payment 8629 · order [60172](https://market.crossvertise.com/de-de/manager/ordersales/edit/60172) · since 06.11. 18:52 UTC"));
        }

        [Test]
        public async Task Only_the_first_five_payments_are_listed_and_the_rest_is_counted()
        {
            string? sent = null;
            _notifier.Setup(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((text, _) => sent = text)
                .ReturnsAsync(true);

            var problem = Problem(PaymentJob, "Failed", "7 x technical authorization failure");
            for (var i = 1; i <= 7; i++)
            {
                problem.Issues.Add(new HealthIssue { Reason = "TechnicalFailure", PaymentId = i, OrderId = 100 + i });
            }

            Problems(problem);

            await Run(AtUtcHour(3));

            Assert.That(sent!.Split('•').Length - 1, Is.EqualTo(5));
            Assert.That(sent, Does.Contain("… and 2 more"));
        }

        // ---------- delivery ----------

        [Test]
        public async Task An_undelivered_alert_is_retried_rather_than_lost()
        {
            _notifier.Setup(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            Problems(Problem(PaymentJob, "Failed", "webhook is down"));

            var summary = await Run(AtUtcHour(3));

            Assert.That(summary.Alerted, Is.EqualTo(0));
            Assert.That(summary.FailedToSend, Is.EqualTo(1));
            Assert.That(_stored.Single().AlertedAtUtc, Is.Null, "unsent means unannounced, so the next tick tries again");
        }

        [Test]
        public async Task Nothing_happens_at_all_when_alerting_is_disabled()
        {
            Problems(Problem(PaymentJob, "Failed", "ignored"));

            var summary = await Run(AtUtcHour(3), enabled: false);

            Assert.That(summary.Problems, Is.EqualTo(0));
            _notifier.Verify(n => n.Send(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _alertStateRepo.Verify(r => r.Create(It.IsAny<AlertState>()), Times.Never);
        }

        // ---------- helpers ----------

        private static DateTime AtUtcHour(int hour) => new DateTime(2026, 11, 4, hour, 0, 0, DateTimeKind.Utc);

        private void Problems(params JobProblem[] problems) =>
            _reportService.Setup(s => s.GetHealthSummary(It.IsAny<int>()))
                .ReturnsAsync(new HealthSummary { Problems = problems.ToList() });

        private static JobProblem Problem(string jobName, string reason, string subject) =>
            new JobProblem { JobName = jobName, Reason = reason, LastSubject = subject };

        private static AlertState Alerted(string jobName, string reason) => new AlertState
        {
            Id = Guid.NewGuid().ToString(),
            Key = AlertEvaluator.JobKeyPrefix + jobName,
            DisplayName = jobName,
            Reason = reason,
            FirstSeenUtc = AtUtcHour(1),
            AlertedAtUtc = AtUtcHour(1),
        };

        private static AlertState Pending(string jobName, string reason) => new AlertState
        {
            Id = Guid.NewGuid().ToString(),
            Key = AlertEvaluator.JobKeyPrefix + jobName,
            DisplayName = jobName,
            Reason = reason,
            FirstSeenUtc = AtUtcHour(1),
            AlertedAtUtc = null,
        };

        private Task<AlertRunSummary> Run(DateTime nowUtc, bool enabled = true)
        {
            // UTC rather than Europe/Berlin so the quiet-hours assertions do not move with the clock
            // change; the conversion itself is a single framework call with a UTC fallback.
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Alerting:Enabled"] = enabled ? "true" : "false",
                    ["Alerting:TeamsWebhookUrl"] = "https://example.invalid/webhook",
                    ["Alerting:TimeZone"] = "UTC",
                    ["Alerting:BatchReleaseHour"] = "8",
                    ["Alerting:BatchQuietHour"] = "22",
                    ["Alerting:ImmediateJobs:0"] = PaymentJob,
                    ["HttpHealth:Monitors:0:Name"] = PaymentJob,
                    ["HttpHealth:Monitors:0:Url"] = "https://market.crossvertise.com/api-fd/v1/internal/monitoring/payment-health",
                })
                .Build();

            var evaluator = new AlertEvaluator(
                _reportService.Object,
                _functionHealth.Object,
                _alertStateRepo.Object,
                _notifier.Object,
                configuration,
                new FixedTimeProvider(nowUtc));

            return evaluator.Run(CancellationToken.None);
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow);

            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
