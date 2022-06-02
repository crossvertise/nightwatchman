namespace BusinessLogic.Implementations
{
    using BusinessLogic.Interfaces;
    using BusinessLogic.Models.SendInBlue;

    using DomainModel;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using System.Threading.Tasks;

    public class SendInBlueService : ISendInBlueService
    {
        private readonly IJobExecutionService _jobExecutionService;

        public SendInBlueService(IJobExecutionService jobExecutionService)
        {
            _jobExecutionService = jobExecutionService;
        }

        public async Task<(bool IsSuccess, string ErrorMessage)> ProcessEvent(JObject sendInBlueEvent)
        {
            var sendInBlueWebhookPayload = JsonConvert.DeserializeObject<SendInBlueWebhookPayload>(JsonConvert.SerializeObject(sendInBlueEvent));
            var webhookEvents = sendInBlueWebhookPayload?.SendInBlueItemDetails;

            if (webhookEvents == null)
            {
                return (IsSuccess: false, ErrorMessage: SendInBlueConstants.EmptyEvent);
            }

            foreach (var webhookEvent in webhookEvents)
            {
                var mail = new NotificationEmail
                {
                    Sender = webhookEvent.From.Address,
                    Subject = webhookEvent.Subject,

                    BodyHtml = webhookEvent.RawHtmlBody,
                    BodyText = webhookEvent.RawTextBody,
                };

                var jobExecution = await _jobExecutionService.ConvertMailToJobExecution(mail);

                await _jobExecutionService.Create(jobExecution);

            }

            return (IsSuccess: true, ErrorMessage: string.Empty);
        }
    }
}
