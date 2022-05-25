namespace Repos
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel;

    public interface IJobRepo
    {
        Task<ICollection<Job>> GetAll();

        Task<Job> GetById(string id);

        Task Create(Job job);

        Task CreateMany(IEnumerable<Job> jobs);

        Task Update(Job job);

        Task Delete(string id);
    }
}