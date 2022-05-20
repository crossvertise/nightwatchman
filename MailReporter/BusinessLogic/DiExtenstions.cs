using System;
using System.Collections.Generic;
using System.Text;
using BusinessLogic.Implementations;
using BusinessLogic.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogic
{
    public static class DiExtenstions
    {
        public static IServiceCollection AddBusinessLogic(this IServiceCollection service)
        {
            return service
                .AddScoped<IJobExecutionService, JobExecutionService>()
                .AddScoped<ISendInBlueService,SendInBlueService>()
                .AddScoped<IJobService, JobService>()
                .AddRepos();
        }
    }
}
