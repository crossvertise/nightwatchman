namespace BusinessLogic
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;

    using DomainModel;

    using Repos;

    public class JobService : IJobService
    {
        private readonly IJobRepo _jobRepo;

        public JobService(IJobRepo jobRepo)
        {
            _jobRepo = jobRepo;
        }

        public async Task<Job> GetById(string id) => await _jobRepo.GetById(id);

        public async Task<IEnumerable<Job>> GetAll() => await _jobRepo.GetAll();

        public async Task Create(Job job) => await _jobRepo.Create(job);

        public async Task Update(Job job) => await _jobRepo.Update(job);

        public async Task Delete(string jobId) => await _jobRepo.Delete(jobId);

        public async Task SeedJobs()
        {
            var allJobs = new List<Job>
            {
                new Job {Name = "Nightly Database Backup", EmailSender = "backup@example.com", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Sales Data ETL", SubjectContains = "[Sales ETL]", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Search Index Rebuild", SubjectRegex = "^Search Index Rebuild", ExpectedInterval = TimeSpan.FromHours(24), ErrorSubjectRegex = "Search Index Rebuild completed.  0 warnings, [1-9][0-9]* errors", SuccessSubjectRegex = "Search Index Rebuild completed.  0 warnings, 0 errors"},
            };

            await _jobRepo.CreateMany(allJobs);
        }
    }
}
