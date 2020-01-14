using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic
{
    public static class DiExtenstions
    {
        public static IServiceCollection AddBusinessLogic(this IServiceCollection service)
        {
            return service
                .AddScoped<IJobExecutionService, JobExecutionService>()
                .AddScoped<IJobService, JobService>()
                .AddRepos();
        }
    }
}
