using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DomainModel;
using Repos;

namespace BusinessLogic
{
    public class JobService : IJobService
    {
        private IJobRepo jobRepo;

        public JobService(IJobRepo jobRepo)
        {
            this.jobRepo = jobRepo;
        }

        public async Task<Job> GetById(string id)
        {
            return await jobRepo.GetById(id);
        }

        public async Task<IEnumerable<Job>> GetAll()
        {
            return await jobRepo.GetAll();
        }

        public async Task Create(Job job)
        {
            await jobRepo.Create(job);
        }

        public async Task Update(Job job)
        {
            await jobRepo.Update(job);
        }

        public async Task Delete(string jobId)
        {
            await jobRepo.Delete(jobId);
        }

        public async Task SeedJobs()
        {
            var allJobs = new List<Job>
            {
                new Job {Name = "Cinema ETL Live", SubjectContains = "[Live] Cinema ETL", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "OOH ETL Live", SubjectContains = "OOH ETL Live", ExpectedInterval = new TimeSpan(24,0,0), ErrorSubjectRegex = "OOH ETL Live completed.  0 warnings, [1-9][0-9]* errors", SuccessSubjectRegex = "OOH ETL Live completed.  0 warnings, 0 errors"},
                new Job {Name = "Online ETL Live", SubjectContains = "'[LIVE] Update Online'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Radio ETL Live", SubjectContains = "'[LIVE] Update Radio'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Print ETL Live", SubjectContains = "'[LIVE] Update Print'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "XV Routines Live", SubjectContains = "'[LIVE] Xv Routines'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Full Text Search Index Live", SubjectContains = "[Xv.WebJobs.FullTextSearch][Live]", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Medien-DB Live", SubjectContains = "[LIVE] [COMMAND] Import MSW Media Data", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Media-Geo Live", SubjectContains = "'[LIVE] [COMMAND] Import MSW MediaGeo Data'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Online Data Live", SubjectContains = "[LIVE] Import MSW Online Data", ExpectedInterval = new TimeSpan(24,0,0)},
            };

            await jobRepo.CreateMany(allJobs);
        }
    }
}
