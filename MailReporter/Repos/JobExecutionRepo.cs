using DomainModel;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repos
{
    public class JobExecutionRepo : AMongoRepo<JobExecution>, IJobExecutionRepo
    {
        protected override Func<JobExecution, string> IdProperty => (j => j.Id);

        public JobExecutionRepo(string connectionString, string databaseName) : base(connectionString, databaseName)
        { }

        public async Task<IList<JobExecution>> GetLastExecutionsByName(string jobName, int limit = 3)
        {
            var result = await Collection.Find(j => j.JobName == jobName).SortByDescending(j => j.Started).Limit(limit).ToListAsync();
            return result;
        }
    }
}
