namespace Mvc.Models.SendInBlue
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class TransactionalEmailResponse
    {
        [JsonProperty("event")]
        public string Event { get; set; } // soft_bounce, hard_bounce, etc

        [JsonProperty("email")]
        public string Email { get; set; } // recipient of message

        [JsonProperty("id")]
        public int Id { get; set; } // webhook id

        [JsonProperty("date")]
        public string Date { get; set; } // date sent listed in YEAR-MONTH-DAY, HOUR:MINUTE:SECOND in your timezone

        [JsonProperty("ts")]
        public int Time { get; set; } // timestamp in seconds of when event occurred

        [JsonProperty("message-id")]
        public string MessageId { get; set; } // internal message id

        [JsonProperty("ts_event")]
        public int TimeStampEvent { get; set; } // time stamp in seconds GMT of when event occurred

        [JsonProperty("subject")]
        public string Subject { get; set; } // message subject

        [JsonProperty("X-Mailin-custom")]
        public string XMailinCustom { get; set; } // custom added header

        [JsonProperty("sending_ip")]
        public string Sending_Ip { get; set; } // ip used to send message

        [JsonProperty("template_id")]
        public int TemplateId { get; set; } // internal id of the template

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } // tags you might have used to identify your message

        [JsonProperty("reason")]
        public string Reason { get; set; } // the reason the message has been soft bounced

        [JsonProperty("ts_epoch")]
        public string TimeStampEpoch { get; set; } // time stamp in seconds UTC of when message was sent
    }
}