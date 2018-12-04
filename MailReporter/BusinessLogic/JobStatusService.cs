using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DomainModel.DTO;
using Repos;

namespace BusinessLogic
{
    public class JobStatusService
    {
        public JobExecutionRepo JobExecutionRepo { get; set; }

        public JobRepo JobRepo { get; set; }

        public JobStatusService(string mongoDbConnectionString, string mongoDbDatabaseName)
        {
            JobExecutionRepo = new JobExecutionRepo(mongoDbConnectionString, mongoDbDatabaseName);
            JobRepo = new JobRepo(); //(mongoDbConnectionString, mongoDbDatabaseName);
        }

        public async Task<IEnumerable<JobOverview>> GetOverview()
        {
            var allJobs = JobRepo.GetAll();
            var jobOverviews = new List<JobOverview>();

            foreach (var job in allJobs)
            {
                var lastExecutions = (await JobExecutionRepo.GetLastExecutionsByName(job.Name, 5)).ToList();
                var lastExecution = lastExecutions.FirstOrDefault();
                var lastRun = lastExecutions.FirstOrDefault()?.Finished;

                jobOverviews.Add(new JobOverview
                {
                    Job = job,
                    LastExecutions = lastExecutions,
                    LastRun = lastRun,
                    NextRun = lastRun + job.ExpectedInterval,
                    IsDue = lastRun + job.ExpectedInterval < DateTime.UtcNow,
                    LastStatus = lastExecution.Status,
                });
            }

            return jobOverviews;
        }

    }
}
