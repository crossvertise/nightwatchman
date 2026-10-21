namespace BusinessLogic.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using DomainModel.DTO;

    public interface IFunctionHealthService
    {
        /// <summary>One entry per configured function; empty when none are configured.</summary>
        Task<IReadOnlyList<FunctionOverview>> GetOverview();
    }
}
