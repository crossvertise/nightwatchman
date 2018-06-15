using System;
using System.Collections.Generic;
using System.Text;

namespace DomainModel
{
    public class JobExecution
    {
        public string Id { get; set; }

        public Tuple<string,string> Job { get; set; }

        public DateTime? Started { get; set; }

        public DateTime Finished { get; set; }

        public string OriginalSubject { get; set; }

        public string OriginalBody { get; set; }

        public JobExecutionStatus Status { get; set; }

        public string ErrorSummary { get; set; }

        public NotificationEmail NotificationEmail { get; set; }
    }
}
