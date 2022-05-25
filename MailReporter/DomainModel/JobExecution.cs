namespace DomainModel
{
    using System;

    using MongoDB.Bson.Serialization.Attributes;
    using MongoDB.Bson.Serialization.IdGenerators;

    public class JobExecution
    {
        [BsonId(IdGenerator = typeof(StringObjectIdGenerator))]
        public string Id { get; set; }

        public string JobId { get; set; }

        public string JobName { get; set; }

        public DateTime? Started { get; set; }

        public DateTime Finished { get; set; }

        public string OriginalSubject { get; set; }

        public string OriginalBody { get; set; }

        public JobExecutionStatus Status { get; set; }

        public string ErrorSummary { get; set; }

        public NotificationEmail NotificationEmail { get; set; }
    }
}
