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
            Assert.IsTrue(result.IsSuccess);
            Assert.IsEmpty(result.ErrorMessage);
        }

        [Test]
        public async Task ProcessSendInBlueEvent_EmptyEvent()
        {
            _sendInBluePayload = new JObject();
            var result = await _sendInBlueService.ProcessEvent(_sendInBluePayload);
            Assert.IsFalse(result.IsSuccess);
            Assert.IsNotEmpty(result.ErrorMessage);
        }
    }
}