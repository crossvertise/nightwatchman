namespace Repos
{
    using System;

    using DomainModel.Alerting;

    using Microsoft.Extensions.Configuration;

    public class AlertStateRepo : AMongoRepo<AlertState>, IAlertStateRepo
    {
        public AlertStateRepo(IConfiguration configuration) : base(configuration)
        {
        }

        protected override Func<AlertState, string> IdProperty => s => s.Id;
    }
}
