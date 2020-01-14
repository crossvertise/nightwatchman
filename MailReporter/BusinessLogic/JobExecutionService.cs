using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DomainModel;
using DomainModel.DTO;
using Microsoft.Extensions.Configuration;
using Repos;

namespace BusinessLogic
{
    public interface IJobExecutionService
    {
        Task<IEnumerable<JobOverview>> GetOverview();

        Task<long> MigrateData(string oldname, string newname, string newJobId);

        Task<JobExecution> ConvertMailToJobExecution(NotificationEmail mail);

        Task Create(JobExecution jobExecution);

        Task ReclassifyUnclassified();
    }

    public class JobExecutionService : IJobExecutionService
    {
        private readonly IJobExecutionRepo _jobExecutionRepo;

        private readonly IJobRepo _jobRepo;

        private readonly IConfiguration _configuration;

        private IEnumerable<Job> _allJobs;

        public JobExecutionService(IJobRepo jobRepo, IJobExecutionRepo jobExecutionRepo, IConfiguration configuration)
        {
            _jobExecutionRepo = jobExecutionRepo;
            _jobRepo = jobRepo;
            _configuration = configuration;
        }

        public async Task<IEnumerable<JobOverview>> GetOverview()
        {
            if(_allJobs == null)
                _allJobs = await _jobRepo.GetAll();

            var jobOverviews = new List<JobOverview>();

            foreach (var job in _allJobs)
            {
                var lastExecutions = (job.Id != null) 
                    ? (await _jobExecutionRepo.GetLastExecutionsById(job.Id, 5)).ToList() 
                    : (await _jobExecutionRepo.GetLastExecutionsByName(job.Name, 5)).ToList();
                var lastExecution = lastExecutions.FirstOrDefault();
                var lastRun = lastExecutions.FirstOrDefault()?.Finished;

                jobOverviews.Add(new JobOverview
                {
                    Job = job,
                    LastExecutions = lastExecutions,
                    LastRun = lastRun,
                    NextRun = lastRun != null ? lastRun + job.ExpectedInterval : null,
                    IsDue = lastRun != null ?  lastRun + job.ExpectedInterval < DateTime.UtcNow : false,
                    LastStatus = lastExecution != null ? lastExecution.Status : DomainModel.JobExecutionStatus.Unknown,
                });
            }

            return jobOverviews;
        }

        public async Task<JobExecution> ConvertMailToJobExecution(NotificationEmail mail)
        {
            var jobExecution = new JobExecution
            {
                OriginalBody = mail.BodyHtml,
                OriginalSubject = mail.Subject,
                NotificationEmail = mail,
                Started = DateTime.UtcNow, // TODO
                Finished = DateTime.UtcNow,
                ErrorSummary = "" //TODO
            };

            // Determine matching job
            Job job = await ClassifyExecution(jobExecution);

            jobExecution.JobId = job.Id;
            jobExecution.JobName = job.Name;

            var status = DetermineJobStatus(jobExecution, job);

            jobExecution.Status = status;

            return jobExecution;
        }

        private async Task<Job> ClassifyExecution(JobExecution jobExecution)
        {
            if (_allJobs == null)
                _allJobs = await _jobRepo.GetAll();

            // Determine matching job
            Job job = null;

            // match by sender email address
            var sender = jobExecution.NotificationEmail.Sender;
            if (!string.IsNullOrWhiteSpace(sender))
            {
                job = _allJobs.Where(j => j.EmailSender != null).FirstOrDefault(j => j.EmailSender.Equals(sender.Trim(), StringComparison.InvariantCultureIgnoreCase));
            }

            // Match by subject regex
            if (job == null)
            {
                var jobs = _allJobs.Where(j => !string.IsNullOrWhiteSpace(j.SubjectRegex) && Regex.IsMatch(jobExecution.OriginalSubject, j.SubjectRegex));
                if (jobs.Count() > 1)
                {
                    job = new Job { Name = "Ambiguous Subject RegEx: " + string.Join(" ,", jobs.Select(j => j.Name)) };
                }
                job = jobs.FirstOrDefault();
            }

            // Match by subject contains
            if (job == null)
            {
                var jobs = _allJobs.Where(j => !string.IsNullOrWhiteSpace(j.SubjectContains) && jobExecution.OriginalSubject.Contains(j.SubjectContains));
                if (jobs.Count() > 1)
                {
                    job = new Job { Name = "Ambiguous Subject Contains: " + string.Join(" ,", jobs.Select(j => j.Name)) };
                }
                job = jobs.FirstOrDefault();
            }

            if (job == null)
            {
                job = new Job { Name = "Unknown" };
            }

            return job;
        }

        private JobExecutionStatus DetermineJobStatus(JobExecution jobExecution, Job job)
        {
            JobExecutionStatus status;
            // determine the status
            var errorWords = _configuration.GetSection("Jobs")["ErrorWords"].Split(',').Select(w => w.Trim()).Where(w => string.IsNullOrWhiteSpace(w)).ToList();
            var successWords = _configuration.GetSection("Jobs")["SuccessWords"].Split(',').Select(w => w.Trim()).Where(w => string.IsNullOrWhiteSpace(w)).ToList(); ;

            if (!errorWords.Any() || !successWords.Any())
                throw new InvalidOperationException("SuccessWords or ErrorWords not properly defined in the config.");

            if (job.ErrorSubjectRegex != null && job.SuccessSubjectRegex != null)
            {
                if (job.ErrorSubjectRegex != null && Regex.IsMatch(jobExecution.OriginalSubject, job.ErrorSubjectRegex))
                {
                    status = JobExecutionStatus.Error;
                }
                else if (job.SuccessSubjectRegex != null && Regex.IsMatch(jobExecution.OriginalSubject, job.SuccessSubjectRegex))
                {
                    status = JobExecutionStatus.Success;
                }
                else
                {
                    status = JobExecutionStatus.Unknown;
                }
            }
            else
            {
                if (errorWords.Any(e => jobExecution.OriginalSubject.ToLowerInvariant().Contains(e)))
                {
                    status = JobExecutionStatus.Error;
                }
                else if (successWords.Any(s => jobExecution.OriginalSubject.ToLowerInvariant().Contains(s)))
                {
                    status = JobExecutionStatus.Success;
                }
                else
                {
                    status = JobExecutionStatus.Unknown;
                }
            }

            return status;
        }

        public async Task Create(JobExecution jobExecution)
        {
            await _jobExecutionRepo.Create(jobExecution);
        }


        public async Task<long> MigrateData(string oldname, string newname, string newJobId)
        {
            return await _jobExecutionRepo.MigrateData(oldname, newname, newJobId);
        }

        public async Task ReclassifyUnclassified()
        {
            var unclassifiedJobs = await _jobExecutionRepo.GetUnclassifiedJobs();

            foreach (var jobExecution in unclassifiedJobs)
            {
                var job = await ClassifyExecution(jobExecution);
                var status = DetermineJobStatus(jobExecution, job);
                jobExecution.JobId = job.Id;
                jobExecution.JobName = job.Name;
                jobExecution.Status = status;

                await _jobExecutionRepo.Update(jobExecution);
            }

        }
    }
}
