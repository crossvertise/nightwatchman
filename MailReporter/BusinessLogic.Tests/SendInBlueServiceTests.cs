using BusinessLogic.Implementations;
using BusinessLogic.Interfaces;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.IO;

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
             _sendInBluePayload = JObject.Parse(File.ReadAllText(@"c:\videogames.json"));
            _sendInBlueService = new SendInBlueService(_jobExecutionService.Object);
        }

        [Test]
        public void ProcessSendInBlueEvent()
        {
            _sendInBlueService.ProcessEvent();
            Assert.Pass();
        }
    }
}