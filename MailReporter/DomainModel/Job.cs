using System;

namespace DomainModel
{
    public class Job
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string EmailSender { get; set; }

        public string SubjectRegex { get; set; }

        public string SuccessSubjectRegex { get; set; }

        public string ErrorSubjectRegex { get; set; }

        public string SuccessBodyRegex { get; set; }

        public string ErrorBodyRegex { get; set; }

        public string ExpectedSchedule { get; set; }

    }
}
