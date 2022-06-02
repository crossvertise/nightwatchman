namespace BusinessLogic
{
    using BusinessLogic.Implementations;
    using BusinessLogic.Interfaces;

    using Microsoft.Extensions.DependencyInjection;

    public static class DIExtensions
    {
        public static IServiceCollection RegisterServices(this IServiceCollection service)
        {
            return service
                .AddScoped<IJobExecutionService, JobExecutionService>()
                .AddScoped<ISendInBlueService, SendInBlueService>()
                .AddScoped<IJobService, JobService>()
                .RegisterRepositories();
        }
    }
}
