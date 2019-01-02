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
                new Job {Name = "Cinema ETL Live", SubjectContains = "[Live] Cinema ETL", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "OOH ETL Live", SubjectContains = "OOH ETL Live", ExpectedInterval = new TimeSpan(24,0,0), ErrorSubjectRegex = "OOH ETL Live completed.  0 warnings, [1-9][0-9]* errors", SuccessSubjectRegex = "OOH ETL Live completed.  0 warnings, 0 errors"},
                new Job {Name = "Online ETL", SubjectContains = "'Update Online'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Radio ETL", SubjectContains = "'Update Radio'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Print ETL", SubjectContains = "'Update Print'", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "XV Routines", SubjectContains = "'Xv Routines'", ExpectedInterval = new TimeSpan(24,0,0)},    
                new Job {Name = "Full Text Search Index", SubjectContains = "[Xv.WebJobs.FullTextSearch][Live]", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Medien-DB", SubjectContains = "media-ws-client, xvsql", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Media-Geo", SubjectContains = "mediageo-ws-client, xvsql", ExpectedInterval = new TimeSpan(24,0,0)},
                new Job {Name = "Import MSW Online Data", SubjectContains = "MSW Online Import", ExpectedInterval = new TimeSpan(24,0,0)},

            };
            return allJobs;
        }

    }
}
