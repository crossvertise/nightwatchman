namespace Repos
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel.Alerting;

    public interface IAlertStateRepo
    {
        /// <summary>Every monitor currently considered broken. Recovered monitors have no row.</summary>
        Task<ICollection<AlertState>> GetAll();

        Task Create(AlertState state);

        Task Update(AlertState state);

        Task Delete(string id);
    }
}
