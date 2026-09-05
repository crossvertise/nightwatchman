using DomainModel;

using Microsoft.Extensions.Configuration;

using Moq;

using NUnit.Framework;

using Repos;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    [TestFixture]
    public class JobExecutionServiceTests
    {
        private Mock<IJobRepo> _jobRepo = null!;
        private Mock<IJobExecutionRepo> _jobExecutionRepo = null!;

        [SetUp]
        public void Setup()
        {
            _jobRepo = new Mock<IJobRepo>();
            _jobExecutionRepo = new Mock<IJobExecutionRepo>();
        }

        private JobExecutionService CreateService(ICollection<Job> jobs, string? successWords = "success, erfolgreich", string? errorWords = "failed, fehler")
        {
            _jobRepo.Setup(r => r.GetAll()).ReturnsAsync(jobs);

            var settings = new Dictionary<string, string?>
            {
                ["SuccessWords"] = successWords,
                ["ErrorWords"] = errorWords,
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            return new JobExecutionService(_jobRepo.Object, _jobExecutionRepo.Object, configuration);
        }

        private static NotificationEmail Mail(string sender, string subject) =>
            new NotificationEmail { Sender = sender, Subject = subject, BodyHtml = "<p>body</p>", BodyText = "body" };

        [Test]
        public async Task ConvertMailToJobExecution_MatchesJobByEmailSender_CaseInsensitiveAndTrimmed()
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "EtlJob", EmailSender = "etl@example.com" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail(" ETL@Example.com ", "ETL run completed"));

            Assert.That(execution.JobId, Is.EqualTo("1"));
            Assert.That(execution.JobName, Is.EqualTo("EtlJob"));
        }

        [Test]
        public async Task ConvertMailToJobExecution_SenderMatchTakesPrecedenceOverSubjectMatch()
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "SenderJob", EmailSender = "etl@example.com" },
                new Job { Id = "2", Name = "SubjectJob", SubjectContains = "ETL" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("etl@example.com", "ETL run completed"));

            Assert.That(execution.JobName, Is.EqualTo("SenderJob"));
        }

        [Test]
        public async Task ConvertMailToJobExecution_MatchesJobBySubjectRegex()
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "RegexJob", SubjectRegex = "^Backup .* finished$" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", "Backup db01 finished"));

            Assert.That(execution.JobName, Is.EqualTo("RegexJob"));
        }

        [Test]
        public async Task ConvertMailToJobExecution_MatchesJobBySubjectContains()
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "ContainsJob", SubjectContains = "Nightly Import" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", "Nightly Import succeeded"));

            Assert.That(execution.JobName, Is.EqualTo("ContainsJob"));
        }

        [Test]
        public async Task ConvertMailToJobExecution_NoMatch_ClassifiesAsUnknown()
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "SomeJob", SubjectContains = "does-not-match" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", "Something else entirely"));

            Assert.That(execution.JobName, Is.EqualTo("Unknown"));
            Assert.That(execution.JobId, Is.Null);
        }

        [TestCase("Import erfolgreich abgeschlossen", JobExecutionStatus.Success)]
        [TestCase("Import FAILED with 3 errors", JobExecutionStatus.Error)]
        [TestCase("Import statusbericht", JobExecutionStatus.Unknown)]
        public async Task ConvertMailToJobExecution_DeterminesStatusFromGlobalWordLists(string subject, JobExecutionStatus expected)
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "ImportJob", SubjectContains = "Import" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", subject));

            Assert.That(execution.Status, Is.EqualTo(expected));
        }

        [Test]
        public async Task ConvertMailToJobExecution_ErrorWordsTakePrecedenceOverSuccessWords()
        {
            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "ImportJob", SubjectContains = "Import" },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", "Import success with failed rows"));

            Assert.That(execution.Status, Is.EqualTo(JobExecutionStatus.Error));
        }

        [TestCase("JOB-42 completed without problems", JobExecutionStatus.Success)]
        [TestCase("JOB-42 aborted unexpectedly", JobExecutionStatus.Error)]
        [TestCase("JOB-42 progress report", JobExecutionStatus.Unknown)]
        public async Task ConvertMailToJobExecution_UsesJobSpecificRegexesWhenBothAreSet(string subject, JobExecutionStatus expected)
        {
            var service = CreateService(new List<Job>
            {
                new Job
                {
                    Id = "1",
                    Name = "RegexStatusJob",
                    SubjectContains = "JOB-42",
                    SuccessSubjectRegex = "completed without problems",
                    ErrorSubjectRegex = "aborted",
                },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", subject));

            Assert.That(execution.Status, Is.EqualTo(expected));
        }

        [Test]
        public async Task ConvertMailToJobExecution_OnlyOneJobRegexSet_FallsBackToGlobalWordLists()
        {
            var service = CreateService(new List<Job>
            {
                new Job
                {
                    Id = "1",
                    Name = "HalfRegexJob",
                    SubjectContains = "JOB-42",
                    ErrorSubjectRegex = "aborted",
                },
            });

            var execution = await service.ConvertMailToJobExecution(Mail("noreply@example.com", "JOB-42 erfolgreich"));

            Assert.That(execution.Status, Is.EqualTo(JobExecutionStatus.Success));
        }

        [Test]
        public void ConvertMailToJobExecution_MissingWordLists_Throws()
        {
            var service = CreateService(new List<Job>(), successWords: "", errorWords: "");

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ConvertMailToJobExecution(Mail("noreply@example.com", "anything")));
        }

        [Test]
        public async Task ReclassifyUnclassified_UpdatesOnlyExecutionsThatNowMatchAJob()
        {
            var matchable = new JobExecution { Id = "e1", JobName = "Unknown", OriginalSubject = "Nightly Import erfolgreich", NotificationEmail = Mail("noreply@example.com", "Nightly Import erfolgreich") };
            var unmatchable = new JobExecution { Id = "e2", JobName = "Unknown", OriginalSubject = "Random mail", NotificationEmail = Mail("noreply@example.com", "Random mail") };
            _jobExecutionRepo.Setup(r => r.GetUnclassifiedJobs()).ReturnsAsync(new List<JobExecution> { matchable, unmatchable });

            var service = CreateService(new List<Job>
            {
                new Job { Id = "1", Name = "ImportJob", SubjectContains = "Nightly Import" },
            });

            var reclassified = await service.ReclassifyUnclassified();

            Assert.That(reclassified, Is.EqualTo(1));
            Assert.That(matchable.JobName, Is.EqualTo("ImportJob"));
            Assert.That(matchable.Status, Is.EqualTo(JobExecutionStatus.Success));
            _jobExecutionRepo.Verify(r => r.Update(matchable), Times.Once);
            _jobExecutionRepo.Verify(r => r.Update(unmatchable), Times.Never);
        }

        [Test]
        public async Task GetOverview_ComputesNextRunAndDueState()
        {
            var job = new Job { Id = "1", Name = "EtlJob", ExpectedInterval = TimeSpan.FromHours(24) };
            var lastRun = DateTime.UtcNow.AddHours(-30);
            _jobExecutionRepo.Setup(r => r.GetLastExecutionsById("1", 5)).ReturnsAsync(new List<JobExecution>
            {
                new JobExecution { Finished = lastRun, Status = JobExecutionStatus.Success },
            });

            var service = CreateService(new List<Job> { job });

            var overview = new List<DomainModel.DTO.JobOverview>(await service.GetOverview());

            Assert.That(overview, Has.Count.EqualTo(1));
            Assert.That(overview[0].IsDue, Is.True);
            Assert.That(overview[0].NextRun, Is.EqualTo(lastRun.AddHours(24)));
            Assert.That(overview[0].LastStatus, Is.EqualTo(JobExecutionStatus.Success));
        }
    }
}
