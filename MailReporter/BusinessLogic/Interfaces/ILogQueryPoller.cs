namespace BusinessLogic.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using BusinessLogic.Models.Http;

    /// <summary>
    /// Runs the queries configured under <c>LogQueryHealth:Monitors</c> against Log Analytics and
    /// records each verdict as a job execution, so "the API threw 5xx" becomes an ordinary tile.
    /// </summary>
    public interface ILogQueryPoller
    {
        /// <summary>Runs every configured monitor; one entry per monitor, in configuration order.</summary>
        Task<IReadOnlyList<HttpHealthPollResult>> RunAll(CancellationToken cancellationToken);
    }
}
