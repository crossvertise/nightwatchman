namespace BusinessLogic
{
    using System;

    using Azure.Core;
    using Azure.Identity;

    using BusinessLogic.Implementations;
    using BusinessLogic.Interfaces;

    using Microsoft.Extensions.DependencyInjection;

    public static class DIExtensions
    {
        public static IServiceCollection RegisterServices(this IServiceCollection service)
        {
            return service
                .AddHttpClient()
                .AddSingleton(TimeProvider.System)
                // Managed identity on the App Service, the developer's Azure CLI / Visual Studio login locally.
                .AddSingleton<TokenCredential>(_ => new DefaultAzureCredential())
                .AddScoped<IJobExecutionService, JobExecutionService>()
                .AddScoped<ISendInBlueService, SendInBlueService>()
                .AddScoped<IJobService, JobService>()
                .AddScoped<IJobStatusReportService, JobStatusReportService>()
                .AddScoped<IServiceBusStatusClient, AzureServiceBusStatusClient>()
                .AddScoped<IFunctionHealthService, FunctionHealthService>()
                .RegisterRepositories();
        }
    }
}
