namespace Repos
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel;

    public interface IJobExecutionRepo
    {
        Task<IList<JobExecution>> GetLastExecutionsByName(string jobName, int limit = 3);

        Task<IList<JobExecution>> GetLastExecutionsById(string jobId, int limit = 3);

        Task<long> MigrateData(string oldname, string newname, string newJobId);

        Task Create(JobExecution jobExecution);

        Task Update(JobExecution jobExecution);

        Task<IList<JobExecution>> GetUnclassifiedJobs();
    }
}