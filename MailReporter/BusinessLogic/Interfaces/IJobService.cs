namespace BusinessLogic.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel;

    public interface IJobService
    {
        Task Create(Job job);

        Task Delete(string jobId);

        Task<Job> GetById(string id);

        Task<IEnumerable<Job>> GetAll();

        Task SeedJobs();

        Task Update(Job job);
    }
}
