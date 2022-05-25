namespace Repos
{
    using DomainModel;

    using Microsoft.Extensions.Configuration;

    using System;

    public class JobRepo : AMongoRepo<Job>, IJobRepo
    {
        public JobRepo(IConfiguration configuration) : base(configuration) { }

        protected override Func<Job, string> IdProperty => (j => j.Id);
    }
}
