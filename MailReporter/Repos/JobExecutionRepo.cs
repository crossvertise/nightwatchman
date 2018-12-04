using DomainModel;
using System;

namespace Repos
{
    public class JobExecutionRepo : AMongoRepo<JobExecution>
    {
        public JobExecutionRepo(string connectionString, string databaseName) : base(connectionString, databaseName)
        { }
    }
}
