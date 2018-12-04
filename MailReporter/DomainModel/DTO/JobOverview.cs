using System;
using System.Collections.Generic;
using System.Text;

namespace DomainModel.DTO
{
    public class JobOverview
    {
        public Job Job { get; set; }

        public List<JobExecution> LastExecutions { get; set; }

        public DateTime? LastRun { get; set; }

        public DateTime? NextRun { get; set; }

        public bool IsDue { get; set; }

        public JobExecutionStatus LastStatus { get; set; }
    }
}
