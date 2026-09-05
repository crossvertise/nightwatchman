namespace BusinessLogic.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel.DTO.Report;

    public interface IJobStatusReportService
    {
        Task<HealthSummary> GetHealthSummary(int lookbackHours = 24);

        Task<IList<JobSummary>> GetJobSummaries();

        Task<JobDetail> GetJobDetail(string jobIdOrName, int recentExecutionLimit = 10);

        Task<IList<ExecutionSummary>> GetJobExecutions(string jobIdOrName, int limit = 10);

        Task<IList<ExecutionSummary>> GetRecentExecutions(int hours = 24, int limit = 100, string status = null);

        Task<IList<ExecutionSummary>> GetRecentFailures(int hours = 24, int limit = 50);

        Task<IList<ExecutionSummary>> GetUnclassifiedExecutions(int limit = 50);
    }
}
