namespace Mvc.Models
{
    using System.Collections.Generic;

    using DomainModel.DTO;

    public class DashboardViewModel
    {
        public IEnumerable<JobOverview> Jobs { get; set; }

        public IReadOnlyList<FunctionOverview> Functions { get; set; }
    }
}
