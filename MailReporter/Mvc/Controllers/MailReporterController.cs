using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Mvc.Controllers
{
    public class MailReporterController : Controller
    {
        private ILogger log = null;

        [HttpGet, HttpPost]
        public static async Task<IActionResult> Mandrill()
        {
            log.Info($"C# HTTP trigger function processed a request. HTTP Method: {req.Method}");

            try
            {


                if (req.Method.ToUpper() == "HEAD")
                {
                    return new OkObjectResult("Hello Mandrill!");
                }

                if (!req.HasFormContentType)
                {
                    return new BadRequestObjectResult("No form content received");
                }
                var content = req.Form;
                var validJson = content["mandrill_events"].First().Replace("mandrill_events =", string.Empty);

                if (string.IsNullOrWhiteSpace(validJson))
                {
                    return new BadRequestObjectResult("No valid JSON found");
                }

                var webhookEvents = JsonConvert.DeserializeObject<List<WebHookEvent>>(validJson);
                if (webhookEvents == null)
                {
                    return new BadRequestObjectResult("No webhook events found");
                }
                log.Info($"Processing {webhookEvents.Count} event(s)...");

                var connectionString = "";// config["MongoDbConnectionString"];
                var databaseName = "";// config["MongoDbDatabaseName"];
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
            catch (Exception e)
            {
                log.Error(e.Message + "\r\n" + e.StackTrace);
                throw e;
            }
            return new OkObjectResult("Event processed successfully");
        }
    }
}