namespace BusinessLogic.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel;
    using DomainModel.DTO;

    public interface IJobExecutionService
    {
        Task<IEnumerable<JobOverview>> GetOverview();

        Task<long> MigrateData(string oldname, string newname, string newJobId);

        Task<JobExecution> ConvertMailToJobExecution(NotificationEmail mail);

        Task Create(JobExecution jobExecution);

        Task<int> ReclassifyUnclassified();
    }
}
