using DomainModel;
using System;
using System.Collections.Generic;

namespace Repos
{
    public class JobRepo //: AMongoRepo<Job>
    {
        //public JobRepo(string connectionString, string databaseName) : base(connectionString, databaseName)
        public JobRepo()
        { }

        public IList<Job> GetAll()
        {
            var allJobs = new List<Job>
            {
                new Job {Name = "Cinema ETL Live", SubjectRegex = "[Live] Cinema ETL", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "OOH ETL Live", SubjectRegex = "OOH ETL Live", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Online ETL", SubjectRegex = "'Update Online'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Radio ETL", SubjectRegex = "'Update Radio'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Print ETL", SubjectRegex = "'Update Print'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "XV Routines", SubjectRegex = "'Xv Routines'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Online Data", SubjectRegex = "'Import MSW Online Data'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Full Text Search Index", SubjectRegex = "[Xv.WebJobs.FullTextSearch][Live]", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Medien-DB", SubjectRegex = "media-ws-client, xvsql", ExpectedInterval = new TimeSpan(24,0,0)},
            };
            return allJobs;
        }

    }
}
