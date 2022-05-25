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
                new Job {Name = "Cinema ETL Live", SubjectContains = "[Live] Cinema ETL", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "OOH ETL Live", SubjectContains = "OOH ETL Live", ExpectedInterval = TimeSpan.FromHours(24), ErrorSubjectRegex = "OOH ETL Live completed.  0 warnings, [1-9][0-9]* errors", SuccessSubjectRegex = "OOH ETL Live completed.  0 warnings, 0 errors"},
                new Job {Name = "Online ETL Live", SubjectContains = "'[LIVE] Update Online'", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Radio ETL Live", SubjectContains = "'[LIVE] Update Radio'", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Print ETL Live", SubjectContains = "'[LIVE] Update Print'", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "XV Routines Live", SubjectContains = "'[LIVE] Xv Routines'", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Full Text Search Index Live", SubjectContains = "[Xv.WebJobs.FullTextSearch][Live]", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Import MSW Medien-DB Live", SubjectContains = "[LIVE] [COMMAND] Import MSW Media Data", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Import MSW Media-Geo Live", SubjectContains = "'[LIVE] [COMMAND] Import MSW MediaGeo Data'", ExpectedInterval = TimeSpan.FromHours(24)},
                new Job {Name = "Import MSW Online Data Live", SubjectContains = "[LIVE] Import MSW Online Data", ExpectedInterval = TimeSpan.FromHours(24)},
            };

            await _jobRepo.CreateMany(allJobs);
        }
    }
}
