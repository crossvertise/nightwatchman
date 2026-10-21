using BusinessLogic.Interfaces;
using BusinessLogic.Models.Functions;

using DomainModel;

using Microsoft.Extensions.Configuration;

using Moq;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace BusinessLogic.Tests
{
    [TestFixture]
    public class FunctionHealthServiceTests
    {
        private const string Namespace = "/subscriptions/sub/resourceGroups/rg/providers/Microsoft.ServiceBus/namespaces/ns";
        private static readonly DateTime Now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        private Mock<IServiceBusStatusClient> _client = null!;

        [SetUp]
        public void Setup()
        {
            _client = new Mock<IServiceBusStatusClient>();
        }

        [Test]
        public async Task No_monitors_configured_gives_no_tiles_and_asks_Azure_nothing()
        {
            var service = CreateService(new Dictionary<string, string?> { ["AzureFunctions:ServiceBusNamespaceResourceId"] = Namespace });

            var overview = await service.GetOverview();

            Assert.That(overview, Is.Empty);
            _client.VerifyNoOtherCalls();
        }

        /// <summary>
        /// The live mailer on 20./21.10.2026 (IM-581): mails waited for hours, nothing was sent.
        /// </summary>
        [Test]
        public async Task Messages_that_wait_while_nothing_is_completed_are_stuck()
        {
            Returns(new ServiceBusEntityStatus { ActiveMessages = 11, ActiveMessagesEarlier = 9, CompletedMessages = 0, DeadLetterMessages = 126 });

            var tile = (await CreateService(Settings()).GetOverview()).Single();

            Assert.That(tile.IsStuck, Is.True);
            Assert.That(tile.Error, Is.Null);
            Assert.That(tile.WaitingMessages, Is.EqualTo(11));
            Assert.That(tile.CompletedMessages, Is.EqualTo(0));
            Assert.That(tile.DeadLetteredMessages, Is.EqualTo(126));
            Assert.That(tile.WindowMinutes, Is.EqualTo(30));
        }

        [Test]
        public async Task Messages_that_wait_while_others_are_completed_are_not_stuck()
        {
            Returns(new ServiceBusEntityStatus { ActiveMessages = 40, ActiveMessagesEarlier = 120, CompletedMessages = 200 });

            var tile = (await CreateService(Settings()).GetOverview()).Single();

            Assert.That(tile.IsStuck, Is.False);
        }

        /// <summary>
        /// A message that arrives after a quiet period waits while the function starts up (a
        /// Consumption cold start takes up to a minute). That is not a stuck function.
        /// </summary>
        [Test]
        public async Task A_message_that_just_arrived_is_not_stuck()
        {
            Returns(new ServiceBusEntityStatus { ActiveMessages = 1, ActiveMessagesEarlier = 0, CompletedMessages = 0 });

            var tile = (await CreateService(Settings()).GetOverview()).Single();

            Assert.That(tile.IsStuck, Is.False);
        }

        [Test]
        public async Task Nothing_waiting_is_not_stuck()
        {
            Returns(new ServiceBusEntityStatus { ActiveMessages = 0, ActiveMessagesEarlier = 4, CompletedMessages = 0, DeadLetterMessages = 2052 });

            var tile = (await CreateService(Settings()).GetOverview()).Single();

            Assert.That(tile.IsStuck, Is.False);
        }

        [Test]
        public async Task Asks_for_the_window_and_grace_period_back_from_now()
        {
            Returns(new ServiceBusEntityStatus());
            var settings = Settings();
            settings["AzureFunctions:StuckAfterMinutes"] = "45";
            settings["AzureFunctions:GraceMinutes"] = "5";

            var tile = (await CreateService(settings).GetOverview()).Single();

            _client.Verify(c => c.GetStatus(Namespace, It.Is<FunctionMonitor>(m => m.Queue == "xv-mailer-queue"), Now.AddMinutes(-45), Now.AddMinutes(-5), Now));
            Assert.That(tile.WindowMinutes, Is.EqualTo(45));
        }

        [Test]
        public async Task Defaults_to_a_30_minute_window_and_a_10_minute_grace_period()
        {
            Returns(new ServiceBusEntityStatus());
            var settings = Settings();
            settings.Remove("AzureFunctions:StuckAfterMinutes");
            settings.Remove("AzureFunctions:GraceMinutes");

            await CreateService(settings).GetOverview();

            _client.Verify(c => c.GetStatus(Namespace, It.IsAny<FunctionMonitor>(), Now.AddMinutes(-30), Now.AddMinutes(-10), Now));
        }

        [Test]
        public async Task A_function_that_cannot_be_read_shows_why_and_the_others_still_load()
        {
            var settings = Settings();
            settings["AzureFunctions:Monitors:1:Name"] = "CRM integration";
            settings["AzureFunctions:Monitors:1:Topic"] = "xv-crm-integration-topic";
            settings["AzureFunctions:Monitors:1:Subscription"] = "all-messages";
            _client.Setup(c => c.GetStatus(It.IsAny<string>(), It.Is<FunctionMonitor>(m => m.Queue == "xv-mailer-queue"), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ThrowsAsync(new HttpRequestException("Azure answered 403 Forbidden"));
            _client.Setup(c => c.GetStatus(It.IsAny<string>(), It.Is<FunctionMonitor>(m => m.Topic == "xv-crm-integration-topic"), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new ServiceBusEntityStatus { CompletedMessages = 62 });

            var tiles = await CreateService(settings).GetOverview();

            Assert.That(tiles, Has.Count.EqualTo(2));
            Assert.That(tiles[0].Error, Is.EqualTo("Azure answered 403 Forbidden"));
            Assert.That(tiles[0].IsStuck, Is.False);
            Assert.That(tiles[0].WaitingMessages, Is.Null);
            Assert.That(tiles[1].Error, Is.Null);
            Assert.That(tiles[1].CompletedMessages, Is.EqualTo(62));
        }

        [Test]
        public async Task A_function_can_use_another_namespace()
        {
            Returns(new ServiceBusEntityStatus());
            var settings = Settings();
            settings["AzureFunctions:Monitors:0:NamespaceResourceId"] = "/other/namespace";

            await CreateService(settings).GetOverview();

            _client.Verify(c => c.GetStatus("/other/namespace", It.IsAny<FunctionMonitor>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()));
        }

        [Test]
        public async Task Without_a_namespace_the_tile_says_so_and_Azure_is_not_asked()
        {
            var settings = Settings();
            settings.Remove("AzureFunctions:ServiceBusNamespaceResourceId");

            var tile = (await CreateService(settings).GetOverview()).Single();

            Assert.That(tile.Error, Is.EqualTo(FunctionHealthService.NoNamespaceConfigured));
            _client.VerifyNoOtherCalls();
        }

        private void Returns(ServiceBusEntityStatus status) =>
            _client.Setup(c => c.GetStatus(It.IsAny<string>(), It.IsAny<FunctionMonitor>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(status);

        private FunctionHealthService CreateService(Dictionary<string, string?> settings) =>
            new FunctionHealthService(
                _client.Object,
                new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
                new FixedTimeProvider(Now));

        private static Dictionary<string, string?> Settings() => new Dictionary<string, string?>
        {
            ["AzureFunctions:ServiceBusNamespaceResourceId"] = Namespace,
            ["AzureFunctions:StuckAfterMinutes"] = "30",
            ["AzureFunctions:GraceMinutes"] = "10",
            ["AzureFunctions:Monitors:0:Name"] = "Mailer",
            ["AzureFunctions:Monitors:0:FunctionApp"] = "xv-functions-mailer-live-v2",
            ["AzureFunctions:Monitors:0:Queue"] = "xv-mailer-queue",
        };

        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;

            public FixedTimeProvider(DateTime now)
            {
                _now = new DateTimeOffset(now);
            }

            public override DateTimeOffset GetUtcNow() => _now;
        }
    }
}
