namespace BusinessLogic.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using BusinessLogic.Models.Http;

    /// <summary>
    /// Polls the endpoints configured under <c>HttpHealth:Monitors</c> and records each verdict as a
    /// job execution, so an endpoint that reports its own health becomes an ordinary tile.
    /// </summary>
    public interface IHttpHealthPoller
    {
        /// <summary>Polls every configured monitor; one entry per monitor, in configuration order.</summary>
        Task<IReadOnlyList<HttpHealthPollResult>> PollAll(CancellationToken cancellationToken);
    }
}
