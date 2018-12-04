using System;
using System.Collections.Generic;
using System.Configuration;
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
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", "head", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"C# HTTP trigger function processed a request. HTTP Method: {req.Method}");

            try
            {
                if (req.Method == HttpMethod.Head)
                {
                    return req.CreateResponse(HttpStatusCode.OK);
                }

                var content = await req.Content.ReadAsFormDataAsync();
                var validJson = content["mandrill_events"].Replace("mandrill_events =", string.Empty);

                if (string.IsNullOrWhiteSpace(validJson))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, "No valid JSON found");
                }

                var webhookEvents = JsonConvert.DeserializeObject<List<WebHookEvent>>(validJson);
                if (webhookEvents == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, "No webhook events found");
                }
                log.Info($"Processing {webhookEvents.Count} event(s)...");

                var connectionString = ConfigurationManager.AppSettings["MongoDbConnectionString"];
                var databaseName = ConfigurationManager.AppSettings["MongoDbDatabaseName"];
                var mailLoggingService = new MailLoggingService(connectionString, databaseName);

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

                    var jobExecution = mailLoggingService.ConvertMailToJobExecution(mail);
                    log.Info(JsonConvert.SerializeObject(jobExecution, Formatting.None));

                    await mailLoggingService.SaveJobExecution(jobExecution);

                }
            }
            catch(Exception e)
            {
                log.Error(e.Message + "\r\n" + e.StackTrace);
                throw e;
            }
            return req.CreateResponse(HttpStatusCode.OK, "Event processed successfully");
        }
        
    }
}
