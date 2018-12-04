using DomainModel;
using System;

namespace Repos
{
    public class JobRepo : AMongoRepo<Job>
    {
        public JobRepo(string connectionString, string databaseName) : base(connectionString, databaseName)
        { }

    }
}
