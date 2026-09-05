using BusinessLogic.Implementations;
using BusinessLogic.Interfaces;

using Moq;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

using System.IO;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    [TestFixture]
    public class SendInBlueServiceTests
    {
        ISendInBlueService _sendInBlueService;
        private readonly Mock<IJobExecutionService> _jobExecutionService = new Mock<IJobExecutionService>();
        JObject _sendInBluePayload;

        [SetUp]
        public void Setup()
        {
            _sendInBluePayload = JObject.Parse(File.ReadAllText("./TestData/SendInBlue.Event.json"));
            _sendInBlueService = new SendInBlueService(_jobExecutionService.Object);
        }

        [Test]
        public async Task ProcessSendInBlueEvent()
        {
            var result = await _sendInBlueService.ProcessEvent(_sendInBluePayload);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.ErrorMessage, Is.Empty);
        }

        [Test]
        public async Task ProcessSendInBlueEvent_EmptyEvent()
        {
            _sendInBluePayload = new JObject();
            var result = await _sendInBlueService.ProcessEvent(_sendInBluePayload);
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Empty);
        }
        /// <summary>
        /// Brevo sends the Received header as a plain string when the mail carries only one such
        /// header (regression: this made the webhook answer 500 and Brevo retry for days).
        /// </summary>
        [Test]
        public async Task ProcessSendInBlueEvent_ReceivedHeaderAsSingleString_IsAccepted()
        {
            var item = (JObject)_sendInBluePayload["Items"]![0]!;
            item["Headers"]!["Received"] = "from mail.example.com by inbound1.sendinblue.com; Fri, 4 Sep 2026 04:05:13 +0000";

            var result = await _sendInBlueService.ProcessEvent(_sendInBluePayload);

            Assert.That(result.IsSuccess, Is.True);
            _jobExecutionService.Verify(s => s.ConvertMailToJobExecution(It.IsAny<DomainModel.NotificationEmail>()), Times.AtLeastOnce);
        }

        [Test]
        public async Task ProcessSendInBlueEvent_ReceivedHeaderMissing_IsAccepted()
        {
            var item = (JObject)_sendInBluePayload["Items"]![0]!;
            ((JObject)item["Headers"]!).Remove("Received");

            var result = await _sendInBlueService.ProcessEvent(_sendInBluePayload);

            Assert.That(result.IsSuccess, Is.True);
        }
    }
}
