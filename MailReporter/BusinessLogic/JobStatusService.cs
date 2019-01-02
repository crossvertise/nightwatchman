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
    }

    public class JobStatusService : IJobStatusService
    {
        public JobExecutionRepo JobExecutionRepo { get; set; }

        public JobRepo JobRepo { get; set; }

        public JobStatusService(string mongoDbConnectionString, string mongoDbDatabaseName)
        {
            JobExecutionRepo = new JobExecutionRepo(mongoDbConnectionString, mongoDbDatabaseName);
            JobRepo = new JobRepo(); //(mongoDbConnectionString, mongoDbDatabaseName);
        }

        public JobStatusService(IConfiguration configuration)
            : this(configuration.GetSection("Jobs")["MongoDbConnectionString"], configuration.GetSection("Jobs")["MongoDbDatabaseName"])
        {
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
                    NextRun = lastRun != null ? lastRun + job.ExpectedInterval : null,
                    IsDue = lastRun != null ?  lastRun + job.ExpectedInterval < DateTime.UtcNow : false,
                    LastStatus = lastExecution != null ? lastExecution.Status : DomainModel.JobExecutionStatus.Unknown,
                });
            }

            return jobOverviews;
        }

    }
}
