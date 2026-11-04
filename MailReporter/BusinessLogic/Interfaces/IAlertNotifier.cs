namespace BusinessLogic.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Where an alert is delivered.</summary>
    public interface IAlertNotifier
    {
        /// <summary>
        /// Posts one message. Returns false when it could not be delivered — the caller then leaves the
        /// problem unannounced so the next tick tries again, rather than losing the alert silently.
        /// </summary>
        Task<bool> Send(string text, CancellationToken cancellationToken);
    }
}
