namespace Mvc.Models
{
    using System.Collections.Generic;

    using DomainModel.DTO;

    public class DashboardViewModel
    {
        public IEnumerable<JobOverview> Jobs { get; set; }

        public IReadOnlyList<FunctionOverview> Functions { get; set; }

        /// <summary>Polled endpoints, to link a tile's findings to the right host.</summary>
        public BusinessLogic.Models.Http.HttpHealthOptions HttpHealth { get; set; } = new BusinessLogic.Models.Http.HttpHealthOptions();
    }
}
