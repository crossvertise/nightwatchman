namespace BusinessLogic.Models.SendInBlue
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class TransactionalEmailResponse
    {
        /// <summary>
        /// Soft_bounce, Hard_bounce, etc.
        /// </summary>
        [JsonProperty("event")]
        public string Event { get; set; }

        /// <summary>
        /// Recipient of message.
        /// </summary>
        [JsonProperty("email")]
        public string Email { get; set; }

        /// <summary>
        /// Webhook ID
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Date sent listed in YEAR-MONTH-DAY, HOUR:MINUTE:SECOND in your timezone
        /// </summary>
        [JsonProperty("date")]
        public string Date { get; set; }

        /// <summary>
        /// Timestamp in seconds of when event occurred
        /// </summary>
        [JsonProperty("ts")]
        public int Time { get; set; }

        /// <summary>
        /// Internal message ID.
        /// </summary>
        [JsonProperty("message-id")]
        public string MessageId { get; set; }

        /// <summary>
        /// Timestamp in seconds GMT of when event occurred.
        /// </summary>
        [JsonProperty("ts_event")]
        public int TimeStampEvent { get; set; }

        [JsonProperty("subject")]
        public string Subject { get; set; }

        /// <summary>
        /// Custom added header
        /// </summary>
        [JsonProperty("X-Mailin-custom")]
        public string XMailinCustom { get; set; }

        [JsonProperty("sending_ip")]
        public string Sending_Ip { get; set; }

        /// <summary>
        /// Internal id of the template.
        /// </summary>
        [JsonProperty("template_id")]
        public int TemplateId { get; set; }

        /// <summary>
        /// Tags you might have used to identify your message.
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; }

        /// <summary>
        /// The reason the message has been soft bounced.
        /// </summary>
        [JsonProperty("reason")]
        public string Reason { get; set; }

        /// <summary>
        /// Timestamp in seconds UTC of when message was sent.
        /// </summary>
        [JsonProperty("ts_epoch")]
        public string TimeStampEpoch { get; set; }
    }
}