using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BusinessLogic;
using DomainModel;
using Mandrill.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace MailReporter
{
    public static class MailReporter
    {
        [FunctionName("MailReporter")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processed a request.");

            // parse query parameter
            string name = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
                .Value;

            var content = await req.Content.ReadAsStringAsync();
            var validJson = content.Replace("mandrill_events=", string.Empty);

            if (string.IsNullOrWhiteSpace(validJson))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "No valid JSON found");
            }

            var webhookEvents = JsonConvert.DeserializeObject<List<WebHookEvent>>(validJson);
            if (webhookEvents == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, "No valid JSON found");
            }

            var mailLoggingService = new MailLoggingService();

            foreach (var webhookEvent in webhookEvents)
            {
                var msg = webhookEvent.Msg;
                var mail = new NotificationEmail
                {
                    Sender = msg.FromEmail,
                    //Recipient = msg.
                    Subject = msg.Subject,

                    BodyHtml = msg.Html,
                    BodyText = msg.Text,
                };

                var jobExecution = await mailLoggingService.ConvertMailToJobExecution(mail);

                await mailLoggingService.SaveJobExecution(jobExecution);

            }

            return req.CreateResponse(HttpStatusCode.OK, "Event processed successfully");
        }
        
    }
}
