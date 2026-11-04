namespace BusinessLogic.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    using BusinessLogic.Models.Alerting;

    /// <summary>
    /// Compares every tile against what has already been announced and posts only the differences.
    /// </summary>
    public interface IAlertEvaluator
    {
        Task<AlertRunSummary> Run(CancellationToken cancellationToken);
    }
}
