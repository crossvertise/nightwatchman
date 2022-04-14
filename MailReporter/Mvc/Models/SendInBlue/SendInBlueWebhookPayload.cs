namespace Xv.Mvc.SendInBlue.Models
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Xv.Mvc.SendInBlue.Models;

    public class SendInBlueWebhookPayload
    {
        [JsonProperty("items")]
        public List<SendInBlueItemDetail> SendInBlueItemDetails { get; set; }
    }
}