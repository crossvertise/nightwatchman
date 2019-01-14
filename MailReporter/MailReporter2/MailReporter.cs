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

using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace MailReporter2
{
    public static class MailReporter
    {
        [FunctionName("MailReporter")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", "head", Route = null)]HttpRequest req, TraceWriter log, ExecutionContext context)
        {
            log.Info($"C# HTTP trigger function processed a request. HTTP Method: {req.Method}");
            
            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(context.FunctionAppDirectory)
                    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

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

                var connectionString = config["MongoDbConnectionString"];
                var databaseName = config["MongoDbDatabaseName"];
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
                log.Error("An error occured:" + e.ToString(), e);
                throw;
            }
            return new OkObjectResult("Event processed successfully");
        }
    }
}
