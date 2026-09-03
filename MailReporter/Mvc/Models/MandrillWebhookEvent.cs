namespace Mvc.Models
{
    using Newtonsoft.Json;

    public class MandrillWebhookEvent
    {
        [JsonProperty("msg")]
        public MandrillMessage Msg { get; set; }
    }

    public class MandrillMessage
    {
        [JsonProperty("from_email")]
        public string FromEmail { get; set; }

        [JsonProperty("subject")]
        public string Subject { get; set; }

        [JsonProperty("html")]
        public string Html { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
