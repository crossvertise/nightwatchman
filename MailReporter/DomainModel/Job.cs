using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace DomainModel
{
    public class Job
    {
        [BsonId(IdGenerator = typeof(StringObjectIdGenerator))]
        public string Id { get; set; }

        public string Name { get; set; }

        public string EmailSender { get; set; }

        public string SubjectContains { get; set; }

        public string SubjectRegex { get; set; }

        public string SuccessSubjectRegex { get; set; }

        public string ErrorSubjectRegex { get; set; }

        public string SuccessBodyRegex { get; set; }

        public string ErrorBodyRegex { get; set; }

        public TimeSpan ExpectedInterval { get; set; }

    }
}
