using System.Collections.Generic;
using System.Threading.Tasks;
using DomainModel;

namespace Repos
{
    public interface IJobExecutionRepo
    {
        Task<IList<JobExecution>> GetLastExecutionsByName(string jobName, int limit = 3);

        Task Create(JobExecution jobExecution);
    }
}