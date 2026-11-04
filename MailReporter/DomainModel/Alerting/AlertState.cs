namespace DomainModel.Alerting
{
    using System;

    using MongoDB.Bson.Serialization.Attributes;
    using MongoDB.Bson.Serialization.IdGenerators;

    /// <summary>
    /// What the alerter has already said about one monitor. A row exists only while that monitor is
    /// considered broken; recovering deletes it.
    /// </summary>
    /// <remarks>
    /// This is what makes alerting edge-triggered: a problem is announced once, stays quiet while it
    /// persists, and — because the row is gone after recovery — is announced again if it comes back.
    /// Without it, every ten-minute tick would repost the same failure.
    /// </remarks>
    public class AlertState
    {
        [BsonId(IdGenerator = typeof(StringObjectIdGenerator))]
        public string Id { get; set; }

        /// <summary>Stable monitor key, e.g. <c>job:Payment Health Live</c> or <c>function:Mailer</c>.</summary>
        public string Key { get; set; }

        /// <summary>Human-readable name used in the alert text.</summary>
        public string DisplayName { get; set; }

        /// <summary>Why it is broken, e.g. <c>Failed</c>, <c>Overdue</c>, <c>Stuck</c>.</summary>
        public string Reason { get; set; }

        /// <summary>Extra context for the alert, typically the last subject line or error.</summary>
        public string Detail { get; set; }

        /// <summary>When this problem was first observed.</summary>
        public DateTime FirstSeenUtc { get; set; }

        /// <summary>
        /// When it was announced, or <c>null</c> while an announcement is still deferred (a batch job
        /// that broke overnight waits for the morning). A problem that recovers before it was ever
        /// announced passes in silence — there is nothing for anyone to act on.
        /// </summary>
        public DateTime? AlertedAtUtc { get; set; }
    }
}
