namespace Mvc.Models.SendInBlue
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    
    public class SendInBlueWebhookPayload
    {
        [JsonProperty("items")]
        public List<SendInBlueItemDetail> SendInBlueItemDetails { get; set; }
    }
}