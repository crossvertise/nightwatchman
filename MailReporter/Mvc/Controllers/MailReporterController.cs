namespace Mvc.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using BusinessLogic.Interfaces;

    using DomainModel;

    using Mandrill.Models;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    using Mvc.Attributes;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class MailReporterController : Controller
    {
        private readonly IJobExecutionService _jobExecutionService;
        private readonly ISendInBlueService _sendInBlueService;

        public MailReporterController(IJobExecutionService jobExecutionService, ISendInBlueService sendInBlueService)
        {
            _jobExecutionService = jobExecutionService;
            _sendInBlueService = sendInBlueService;
        }

        [AllowAnonymous]
        [HttpGet, HttpPost, HttpHead]
        [MandrillWebhook(KeyAppSetting = "MandrillWebhookKey")]
        public async Task<IActionResult> Mandrill()
        {
            var request = HttpContext.Request;
            if (request.Method.ToUpper() == "HEAD")
            {
                return new OkObjectResult("Hello Mandrill!");
            }

            if (!request.HasFormContentType)
            {
                return new BadRequestObjectResult("No form content received");
            }

            var content = request.Form;
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

            foreach (var message in webhookEvents.Select(we => we.Msg))
            {
                var mail = new NotificationEmail
                {
                    Sender = message.FromEmail,
                    Subject = message.Subject,

                    BodyHtml = message.Html,
                    BodyText = message.Text,
                };

                var jobExecution = await _jobExecutionService.ConvertMailToJobExecution(mail);

                await _jobExecutionService.Create(jobExecution);
            }

            return new OkObjectResult("Event processed successfully");
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> SendInBlue([FromBody] JObject payload)
        {
            await _sendInBlueService.ProcessEvent(payload);
            return new OkObjectResult("Event processed successfully");
        }
    }
}