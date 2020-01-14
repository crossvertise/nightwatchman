using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainModel.DTO;
using Microsoft.Extensions.Configuration;
using Repos;

namespace BusinessLogic
{
    public interface IJobStatusService
    {
        Task<IEnumerable<JobOverview>> GetOverview();

        Task<long> MigrateData(string oldname, string newname, string newJobId);
        Task ReclassifyUnclassified();
    }

    public class JobStatusService : IJobStatusService
    {
        public IJobExecutionRepo JobExecutionRepo { get; set; }

        public IJobRepo JobRepo { get; set; }

        public JobStatusService(IJobRepo jobRepo, IJobExecutionRepo jobExecutionRepo)
        {
            JobExecutionRepo = jobExecutionRepo;
            JobRepo = jobRepo;
        }

        public async Task<IEnumerable<JobOverview>> GetOverview()
        {
            var allJobs = await JobRepo.GetAll();
            var jobOverviews = new List<JobOverview>();

            foreach (var job in allJobs)
            {
                var lastExecutions = (job.Id != null) 
                    ? (await JobExecutionRepo.GetLastExecutionsById(job.Id, 5)).ToList() 
                    : (await JobExecutionRepo.GetLastExecutionsByName(job.Name, 5)).ToList();
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

        public async Task<long> MigrateData(string oldname, string newname, string newJobId)
        {
            return await JobExecutionRepo.MigrateData(oldname, newname, newJobId);
        }

        public async Task ReclassifyUnclassified()
        {
            var unclassifiedJobs = await JobExecutionRepo.GetUnclassifiedJobs();

        }
    }
}
