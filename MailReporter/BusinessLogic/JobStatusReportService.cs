namespace BusinessLogic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;

    using DomainModel;
    using DomainModel.DTO;
    using DomainModel.DTO.Report;

    using Repos;

    /// <summary>
    /// Read-only reporting on top of <see cref="IJobExecutionService"/>. Produces the small,
    /// body-free DTOs that the MCP tools and other consumers hand out.
    /// </summary>
    public class JobStatusReportService : IJobStatusReportService
    {
        public const string ReasonFailed = "Failed";
        public const string ReasonOverdue = "Overdue";
        public const string ReasonUnknownStatus = "UnknownStatus";
        public const string ReasonNeverRan = "NeverRan";

        private readonly IJobExecutionService _jobExecutionService;
        private readonly IJobExecutionRepo _jobExecutionRepo;

        public JobStatusReportService(IJobExecutionService jobExecutionService, IJobExecutionRepo jobExecutionRepo)
        {
            _jobExecutionService = jobExecutionService;
            _jobExecutionRepo = jobExecutionRepo;
        }

        public async Task<HealthSummary> GetHealthSummary(int lookbackHours = 24)
        {
            var overviews = (await _jobExecutionService.GetOverview()).ToList();

            var summary = new HealthSummary
            {
                GeneratedAtUtc = DateTime.UtcNow,
                LookbackHours = lookbackHours,
                TotalJobs = overviews.Count,
            };

            foreach (var overview in overviews)
            {
                var problem = DetectProblem(overview);
                if (problem == null)
                {
                    summary.HealthyCount++;
                    continue;
                }

                summary.Problems.Add(problem);

                switch (problem.Reason)
                {
                    case ReasonFailed:
                        summary.FailedCount++;
                        break;
                    case ReasonOverdue:
                        summary.OverdueCount++;
                        break;
                    case ReasonUnknownStatus:
                        summary.UnknownCount++;
                        break;
                    default:
                        summary.NeverRanCount++;
                        break;
                }
            }

            summary.IsHealthy = summary.Problems.Count == 0;

            return summary;
        }

        public async Task<IList<JobSummary>> GetJobSummaries()
        {
            var overviews = await _jobExecutionService.GetOverview();
            return overviews.Select(ToJobSummary).ToList();
        }

        public async Task<JobDetail> GetJobDetail(string jobIdOrName, int recentExecutionLimit = 10)
        {
            var overview = await FindOverview(jobIdOrName);
            if (overview == null)
            {
                return null;
            }

            var executions = await LoadExecutions(overview.Job, recentExecutionLimit);

            return new JobDetail
            {
                Id = overview.Job.Id,
                Name = overview.Job.Name,
                EmailSender = overview.Job.EmailSender,
                SubjectContains = overview.Job.SubjectContains,
                SubjectRegex = overview.Job.SubjectRegex,
                SuccessSubjectRegex = overview.Job.SuccessSubjectRegex,
                ErrorSubjectRegex = overview.Job.ErrorSubjectRegex,
                ExpectedInterval = overview.Job.ExpectedInterval.ToString(),
                LastStatus = overview.LastStatus.ToString(),
                LastRunUtc = overview.LastRun,
                NextRunUtc = overview.NextRun,
                IsDue = overview.IsDue,
                RecentExecutions = executions.Select(ToExecutionSummary).ToList(),
            };
        }

        public async Task<IList<ExecutionSummary>> GetJobExecutions(string jobIdOrName, int limit = 10)
        {
            var overview = await FindOverview(jobIdOrName);
            if (overview == null)
            {
                return new List<ExecutionSummary>();
            }

            var executions = await LoadExecutions(overview.Job, limit);
            return executions.Select(ToExecutionSummary).ToList();
        }

        public async Task<IList<ExecutionSummary>> GetRecentExecutions(int hours = 24, int limit = 100, string status = null)
        {
            var executions = await _jobExecutionRepo.GetExecutionsSince(DateTime.UtcNow.AddHours(-Math.Abs(hours)), limit);

            IEnumerable<JobExecution> filtered = executions;

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse<JobExecutionStatus>(status.Trim(), true, out var parsed))
                {
                    throw new ArgumentException(
                        "Unknown status '" + status + "'. Valid values: Success, Warning, Error, Unknown.", nameof(status));
                }

                filtered = filtered.Where(e => e.Status == parsed);
            }

            return filtered.Select(ToExecutionSummary).ToList();
        }

        public async Task<IList<ExecutionSummary>> GetRecentFailures(int hours = 24, int limit = 50)
        {
            var executions = await _jobExecutionRepo.GetExecutionsSince(DateTime.UtcNow.AddHours(-Math.Abs(hours)), limit);

            return executions
                .Where(e => e.Status == JobExecutionStatus.Error || e.Status == JobExecutionStatus.Warning)
                .Select(ToExecutionSummary)
                .ToList();
        }

        public async Task<IList<ExecutionSummary>> GetUnclassifiedExecutions(int limit = 50)
        {
            var executions = await _jobExecutionRepo.GetUnclassifiedJobs();
            return executions.Take(limit).Select(ToExecutionSummary).ToList();
        }

        private static JobProblem DetectProblem(JobOverview overview)
        {
            string reason;

            if (overview.LastRun == null)
            {
                reason = ReasonNeverRan;
            }
            else if (overview.LastStatus == JobExecutionStatus.Error)
            {
                reason = ReasonFailed;
            }
            else if (overview.IsDue)
            {
                reason = ReasonOverdue;
            }
            else if (overview.LastStatus == JobExecutionStatus.Unknown)
            {
                reason = ReasonUnknownStatus;
            }
            else
            {
                return null;
            }

            var lastExecution = overview.LastExecutions?.FirstOrDefault();

            return new JobProblem
            {
                JobId = overview.Job.Id,
                JobName = overview.Job.Name,
                Reason = reason,
                LastStatus = overview.LastStatus.ToString(),
                LastRunUtc = overview.LastRun,
                ExpectedByUtc = overview.NextRun,
                LastSubject = lastExecution?.OriginalSubject,
            };
        }

        private static JobSummary ToJobSummary(JobOverview overview) => new JobSummary
        {
            Id = overview.Job.Id,
            Name = overview.Job.Name,
            LastStatus = overview.LastStatus.ToString(),
            LastRunUtc = overview.LastRun,
            NextRunUtc = overview.NextRun,
            IsDue = overview.IsDue,
            ExpectedInterval = overview.Job.ExpectedInterval.ToString(),
        };

        private static ExecutionSummary ToExecutionSummary(JobExecution execution) => new ExecutionSummary
        {
            Id = execution.Id,
            JobId = execution.JobId,
            JobName = execution.JobName,
            Status = execution.Status.ToString(),
            StartedUtc = execution.Started,
            FinishedUtc = execution.Finished,
            Subject = execution.OriginalSubject,
            Sender = execution.NotificationEmail?.Sender,
            ErrorSummary = execution.ErrorSummary,
        };

        private async Task<JobOverview> FindOverview(string jobIdOrName)
        {
            var overviews = (await _jobExecutionService.GetOverview()).ToList();

            return overviews.FirstOrDefault(o => o.Job.Id == jobIdOrName)
                ?? overviews.FirstOrDefault(o => string.Equals(o.Job.Name, jobIdOrName, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<IList<JobExecution>> LoadExecutions(Job job, int limit) =>
            job.Id != null
                ? await _jobExecutionRepo.GetLastExecutionsById(job.Id, limit)
                : await _jobExecutionRepo.GetLastExecutionsByName(job.Name, limit);
    }
}
