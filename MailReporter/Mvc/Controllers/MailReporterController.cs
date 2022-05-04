using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusinessLogic;
using DomainModel;
using Mandrill.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Mvc.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Mvc.Models.SendInBlue;

namespace Mvc.Controllers
{
    public class MailReporterController : Controller
    {
        private readonly IJobExecutionService jobExecutionService;

        public MailReporterController(IJobExecutionService jobExecutionService)
        {
            this.jobExecutionService = jobExecutionService;
        }

        [AllowAnonymous]
        [HttpGet, HttpPost, HttpHead]
        [MandrillWebhook(KeyAppSetting = "MandrillWebhookKey")]
        public async Task<IActionResult> Mandrill()
        {
            //log.Info($"C# HTTP trigger function processed a request. HTTP Method: {req.Method}");
            var req = HttpContext.Request;
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
                //log.Info($"Processing {webhookEvents.Count} event(s)...");

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

                    var jobExecution = await jobExecutionService.ConvertMailToJobExecution(mail);
                    //log.Info(JsonConvert.SerializeObject(jobExecution, Formatting.None));

                    await jobExecutionService.Create(jobExecution);

                }
            }
            catch (Exception e)
            {
                //log.Error(e.Message + "\r\n" + e.StackTrace);
                throw;
            }
            return new OkObjectResult("Event processed successfully");
        }

        [AllowAnonymous]
        [HttpGet, HttpPost, HttpHead]
        public async Task<IActionResult> SendInBlue(JObject payload)
        {
            //log.Info($"C# HTTP trigger function processed a request. HTTP Method: {req.Method}");
            var req = HttpContext.Request;
            try
            {
                if (req.Method.ToUpper() == "HEAD")
                {
                    return new OkObjectResult("Hello SendinBlue!");
                }

                if (!req.HasFormContentType)
                {
                    return new BadRequestObjectResult("No form content received");
                }
                SendInBlueWebhookPayload sendInBlueWebhookPayload = JsonConvert.DeserializeObject<SendInBlueWebhookPayload>(JsonConvert.SerializeObject(payload));
                var webhookEvents = sendInBlueWebhookPayload?.SendInBlueItemDetails;
                if (webhookEvents == null)
                {
                    return new BadRequestObjectResult("No webhook events found");
                }
                //log.Info($"Processing {webhookEvents.Count} event(s)...");

                foreach (var webhookEvent in webhookEvents)
                {
                    var mail = new NotificationEmail
                    {
                        Sender = webhookEvent.From.Address,
                        //Recipient = msg.
                        Subject = webhookEvent.Subject,

                        BodyHtml = webhookEvent.RawHtmlBody,
                        BodyText = webhookEvent.RawTextBody,
                    };

                    var jobExecution = await jobExecutionService.ConvertMailToJobExecution(mail);
                    //log.Info(JsonConvert.SerializeObject(jobExecution, Formatting.None));

                    await jobExecutionService.Create(jobExecution);

                }
            }
            catch (Exception e)
            {
                //log.Error(e.Message + "\r\n" + e.StackTrace);
                throw;
            }
            return new OkObjectResult("Event processed successfully");
        }
    }
}