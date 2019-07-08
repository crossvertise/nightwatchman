using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Repos;

namespace BusinessLogic
{
    public static class DiExtenstions
    {
        public static IServiceCollection AddRepos(this IServiceCollection service)
        {
            return service
                .AddScoped<IJobRepo, JobRepo>()
                .AddScoped<IJobExecutionRepo, JobExecutionRepo>();
        }
    }
}
