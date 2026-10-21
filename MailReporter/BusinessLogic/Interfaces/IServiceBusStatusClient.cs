namespace BusinessLogic.Interfaces
{
    using System;
    using System.Threading.Tasks;

    using BusinessLogic.Models.Functions;

    using DomainModel;

    public interface IServiceBusStatusClient
    {
        /// <summary>
        /// Reads the current message counts of the monitor's queue or subscription, the messages
        /// completed from <paramref name="from"/> to <paramref name="to"/>, and the highest number
        /// of waiting messages from <paramref name="from"/> to <paramref name="earlierUntil"/>.
        /// </summary>
        Task<ServiceBusEntityStatus> GetStatus(string namespaceResourceId, FunctionMonitor monitor, DateTime from, DateTime earlierUntil, DateTime to);
    }
}
