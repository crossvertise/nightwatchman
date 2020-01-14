using DomainModel;
using Microsoft.Extensions.Configuration;
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

        public JobExecutionRepo(IConfiguration configuration) : base(configuration)
        { }

        public async Task<IList<JobExecution>> GetLastExecutionsByName(string jobName, int limit = 3)
        {
            var result = await Collection.Find(j => j.JobName == jobName).SortByDescending(j => j.Started).Limit(limit).ToListAsync();
            return result;
        }

        public async Task<IList<JobExecution>> GetLastExecutionsById(string jobId, int limit = 3)
        {
            var result = await Collection.Find(j => j.JobId == jobId).SortByDescending(j => j.Started).Limit(limit).ToListAsync();
            return result;
        }

        public async Task<long> MigrateData(string oldname, string newname, string newJobId)
        {
            var update = Builders<JobExecution>.Update.Set(j => j.JobId, newJobId).Set(j => j.JobName, newname);
            var result = await Collection.UpdateManyAsync(j => j.JobName == oldname, update);
            return result.ModifiedCount;
        }

        public async Task<IList<JobExecution>> GetUnclassifiedJobs()
        {
            var result = await Collection.Find(j => j.JobName == null || j.JobName == "Unknown").SortByDescending(j => j.Started).Limit(1000).ToListAsync();
            return result;
        }
    }
}
