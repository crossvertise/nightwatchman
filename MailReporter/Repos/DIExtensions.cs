namespace BusinessLogic
{
    using Microsoft.Extensions.DependencyInjection;

    using Repos;

    public static class DIExtensions
    {
        public static IServiceCollection RegisterRepositories(this IServiceCollection service) => service
            .AddScoped<IJobRepo, JobRepo>()
            .AddScoped<IJobExecutionRepo, JobExecutionRepo>();
    }
}
