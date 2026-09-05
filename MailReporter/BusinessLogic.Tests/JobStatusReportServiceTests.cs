using BusinessLogic.Interfaces;

using DomainModel;
using DomainModel.DTO;

using Moq;

using NUnit.Framework;

using Repos;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    [TestFixture]
    public class JobStatusReportServiceTests
    {
        private Mock<IJobExecutionService> _jobExecutionService = null!;
        private Mock<IJobExecutionRepo> _jobExecutionRepo = null!;

        [SetUp]
        public void Setup()
        {
            _jobExecutionService = new Mock<IJobExecutionService>();
            _jobExecutionRepo = new Mock<IJobExecutionRepo>();
        }

        private JobStatusReportService CreateService(params JobOverview[] overviews)
        {
            _jobExecutionService.Setup(s => s.GetOverview()).ReturnsAsync(overviews);
            return new JobStatusReportService(_jobExecutionService.Object, _jobExecutionRepo.Object);
        }

        private static JobOverview Overview(
            string name,
            JobExecutionStatus status,
            DateTime? lastRun,
            bool isDue = false,
            string? jobId = "1")
        {
            var executions = lastRun == null
                ? new List<JobExecution>()
                : new List<JobExecution>
                {
                    new JobExecution
                    {
                        Id = "exec-1",
                        JobId = jobId,
                        JobName = name,
                        Status = status,
                        Finished = lastRun.Value,
                        OriginalSubject = name + " subject",
                        OriginalBody = "<p>should never leave the service</p>",
                    },
                };

            return new JobOverview
            {
                Job = new Job { Id = jobId, Name = name, ExpectedInterval = TimeSpan.FromHours(24) },
                LastExecutions = executions,
                LastRun = lastRun,
                NextRun = lastRun + TimeSpan.FromHours(24),
                IsDue = isDue,
                LastStatus = lastRun == null ? JobExecutionStatus.Unknown : status,
            };
        }

        [Test]
        public async Task GetHealthSummary_AllJobsSucceeded_ReportsHealthy()
        {
            var service = CreateService(
                Overview("Backup", JobExecutionStatus.Success, DateTime.UtcNow.AddHours(-2)),
                Overview("Etl", JobExecutionStatus.Success, DateTime.UtcNow.AddHours(-3), jobId: "2"));

            var summary = await service.GetHealthSummary();

            Assert.That(summary.IsHealthy, Is.True);
            Assert.That(summary.Problems, Is.Empty);
            Assert.That(summary.TotalJobs, Is.EqualTo(2));
            Assert.That(summary.HealthyCount, Is.EqualTo(2));
            Assert.That(summary.LookbackHours, Is.EqualTo(24));
        }

        [Test]
        public async Task GetHealthSummary_FailedJob_IsReportedAsFailedProblem()
        {
            var lastRun = DateTime.UtcNow.AddHours(-1);
            var service = CreateService(Overview("Backup", JobExecutionStatus.Error, lastRun));

            var summary = await service.GetHealthSummary();

            Assert.That(summary.IsHealthy, Is.False);
            Assert.That(summary.FailedCount, Is.EqualTo(1));
            Assert.That(summary.HealthyCount, Is.EqualTo(0));

            var problem = summary.Problems.Single();
            Assert.That(problem.Reason, Is.EqualTo(JobStatusReportService.ReasonFailed));
            Assert.That(problem.JobName, Is.EqualTo("Backup"));
            Assert.That(problem.LastStatus, Is.EqualTo("Error"));
            Assert.That(problem.LastRunUtc, Is.EqualTo(lastRun));
            Assert.That(problem.LastSubject, Is.EqualTo("Backup subject"));
        }

        [Test]
        public async Task GetHealthSummary_OverdueJob_IsReportedAsOverdueProblem()
        {
            var service = CreateService(
                Overview("Etl", JobExecutionStatus.Success, DateTime.UtcNow.AddHours(-30), isDue: true));

            var summary = await service.GetHealthSummary();

            Assert.That(summary.OverdueCount, Is.EqualTo(1));
            Assert.That(summary.Problems.Single().Reason, Is.EqualTo(JobStatusReportService.ReasonOverdue));
        }

        [Test]
        public async Task GetHealthSummary_JobWithUnknownStatus_IsReportedAsUnknownStatusProblem()
        {
            var service = CreateService(
                Overview("Etl", JobExecutionStatus.Unknown, DateTime.UtcNow.AddHours(-1)));

            var summary = await service.GetHealthSummary();

            Assert.That(summary.UnknownCount, Is.EqualTo(1));
            Assert.That(summary.Problems.Single().Reason, Is.EqualTo(JobStatusReportService.ReasonUnknownStatus));
        }

        [Test]
        public async Task GetHealthSummary_JobWithoutAnyExecution_IsReportedAsNeverRan()
        {
            var service = CreateService(Overview("FreshJob", JobExecutionStatus.Unknown, null));

            var summary = await service.GetHealthSummary();

            Assert.That(summary.NeverRanCount, Is.EqualTo(1));

            var problem = summary.Problems.Single();
            Assert.That(problem.Reason, Is.EqualTo(JobStatusReportService.ReasonNeverRan));
            Assert.That(problem.LastRunUtc, Is.Null);
        }

        [Test]
        public async Task GetJobSummaries_MapsOverviewToFlatSummary()
        {
            var lastRun = DateTime.UtcNow.AddHours(-2);
            var service = CreateService(Overview("Backup", JobExecutionStatus.Success, lastRun));

            var summaries = await service.GetJobSummaries();

            var summary = summaries.Single();
            Assert.That(summary.Name, Is.EqualTo("Backup"));
            Assert.That(summary.LastStatus, Is.EqualTo("Success"));
            Assert.That(summary.LastRunUtc, Is.EqualTo(lastRun));
            Assert.That(summary.NextRunUtc, Is.EqualTo(lastRun.AddHours(24)));
            Assert.That(summary.ExpectedInterval, Is.EqualTo(TimeSpan.FromHours(24).ToString()));
        }

        [Test]
        public async Task GetJobDetail_MatchesByNameCaseInsensitively_AndCarriesNoMailBody()
        {
            var service = CreateService(Overview("Backup", JobExecutionStatus.Success, DateTime.UtcNow.AddHours(-2)));
            _jobExecutionRepo.Setup(r => r.GetLastExecutionsById("1", 10)).ReturnsAsync(new List<JobExecution>
            {
                new JobExecution
                {
                    Id = "exec-1",
                    JobId = "1",
                    JobName = "Backup",
                    Status = JobExecutionStatus.Success,
                    Finished = DateTime.UtcNow.AddHours(-2),
                    OriginalSubject = "Backup completed",
                    OriginalBody = "<p>secret</p>",
                    NotificationEmail = new NotificationEmail { Sender = "backup@example.com" },
                },
            });

            var detail = await service.GetJobDetail("bAcKuP");

            Assert.That(detail, Is.Not.Null);
            Assert.That(detail!.Name, Is.EqualTo("Backup"));
            Assert.That(detail.LastStatus, Is.EqualTo("Success"));

            var execution = detail.RecentExecutions.Single();
            Assert.That(execution.Subject, Is.EqualTo("Backup completed"));
            Assert.That(execution.Sender, Is.EqualTo("backup@example.com"));

            // The DTO must not expose the mail body — assert by property surface, not by value.
            Assert.That(execution.GetType().GetProperty("OriginalBody"), Is.Null);
        }

        [Test]
        public async Task GetJobDetail_UnknownJob_ReturnsNull()
        {
            var service = CreateService(Overview("Backup", JobExecutionStatus.Success, DateTime.UtcNow));

            Assert.That(await service.GetJobDetail("does-not-exist"), Is.Null);
        }

        [Test]
        public async Task GetRecentExecutions_WithStatusFilter_ReturnsOnlyMatchingExecutions()
        {
            var service = CreateService();
            _jobExecutionRepo.Setup(r => r.GetExecutionsSince(It.IsAny<DateTime>(), 100)).ReturnsAsync(new List<JobExecution>
            {
                Execution("a", JobExecutionStatus.Success),
                Execution("b", JobExecutionStatus.Error),
            });

            var executions = await service.GetRecentExecutions(status: "error");

            Assert.That(executions.Select(e => e.Id), Is.EqualTo(new[] { "b" }));
            Assert.That(executions.Single().Status, Is.EqualTo("Error"));
        }

        [Test]
        public void GetRecentExecutions_WithInvalidStatus_Throws()
        {
            var service = CreateService();
            _jobExecutionRepo.Setup(r => r.GetExecutionsSince(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync(new List<JobExecution>());

            Assert.ThrowsAsync<ArgumentException>(() => service.GetRecentExecutions(status: "kaputt"));
        }

        [Test]
        public async Task GetRecentExecutions_UsesTheRequestedTimeWindow()
        {
            var service = CreateService();
            DateTime since = default;
            _jobExecutionRepo.Setup(r => r.GetExecutionsSince(It.IsAny<DateTime>(), It.IsAny<int>()))
                .Callback<DateTime, int>((s, _) => since = s)
                .ReturnsAsync(new List<JobExecution>());

            await service.GetRecentExecutions(hours: 48);

            Assert.That(since, Is.EqualTo(DateTime.UtcNow.AddHours(-48)).Within(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public async Task GetRecentFailures_ReturnsErrorsAndWarningsOnly()
        {
            var service = CreateService();
            _jobExecutionRepo.Setup(r => r.GetExecutionsSince(It.IsAny<DateTime>(), 50)).ReturnsAsync(new List<JobExecution>
            {
                Execution("ok", JobExecutionStatus.Success),
                Execution("warn", JobExecutionStatus.Warning),
                Execution("err", JobExecutionStatus.Error),
                Execution("unknown", JobExecutionStatus.Unknown),
            });

            var failures = await service.GetRecentFailures();

            Assert.That(failures.Select(f => f.Id), Is.EquivalentTo(new[] { "warn", "err" }));
        }

        [Test]
        public async Task GetUnclassifiedExecutions_RespectsTheLimit()
        {
            var service = CreateService();
            _jobExecutionRepo.Setup(r => r.GetUnclassifiedJobs()).ReturnsAsync(new List<JobExecution>
            {
                Execution("1", JobExecutionStatus.Unknown),
                Execution("2", JobExecutionStatus.Unknown),
                Execution("3", JobExecutionStatus.Unknown),
            });

            var executions = await service.GetUnclassifiedExecutions(2);

            Assert.That(executions, Has.Count.EqualTo(2));
        }

        private static JobExecution Execution(string id, JobExecutionStatus status) => new JobExecution
        {
            Id = id,
            JobId = "1",
            JobName = "Backup",
            Status = status,
            Finished = DateTime.UtcNow.AddHours(-1),
            OriginalSubject = "subject " + id,
            OriginalBody = "<p>body</p>",
        };
    }
}
