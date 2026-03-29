namespace PlayniteBridge.Tests.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Web.Script.Serialization;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using Playnite.SDK.Plugins;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Server;

    [TestFixture]
    public class AppHandlerTests
    {
        private AppHandler _handler;
        private IPlayniteAPI _api;
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            _api = Substitute.For<IPlayniteAPI>();
            _handler = new AppHandler(_api, 19821);
            _json = new JavaScriptSerializer();

            var appInfo = Substitute.For<IPlayniteInfoAPI>();
            appInfo.ApplicationVersion.Returns(new Version(10, 0));
            appInfo.Mode.Returns(ApplicationMode.Desktop);
            appInfo.IsPortable.Returns(false);
            appInfo.InOfflineMode.Returns(false);
            _api.ApplicationInfo.Returns(appInfo);

            var paths = Substitute.For<IPlaynitePathsAPI>();
            paths.ApplicationPath.Returns("C:\\Playnite");
            paths.ConfigurationPath.Returns("C:\\Playnite\\Config");
            paths.ExtensionsDataPath.Returns("C:\\Playnite\\ExtensionsData");
            _api.Paths.Returns(paths);

            var addons = Substitute.For<IAddons>();
            addons.Addons.Returns(new List<string> { "addon1" });
            addons.DisabledAddons.Returns(new List<string>());
            addons.Plugins.Returns(new List<Plugin>());
            _api.Addons.Returns(addons);
        }

        [Test]
        public void GetAppInfo_ReturnsVersionAndPort()
        {
            var result = _json.Serialize(_handler.GetAppInfo());
            Assert.That(result, Does.Contain("\"apiPort\":19821"));
            Assert.That(result, Does.Contain("10.0"));
        }

        [Test]
        public void GetAddons_ReturnsInstalledList()
        {
            var result = _json.Serialize(_handler.GetAddons());
            Assert.That(result, Does.Contain("addon1"));
        }

        [Test]
        public void GetStats_EmptyDB_ReturnsTotalZero()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            var games = Substitute.For<IItemCollection<Game>>();
            games.Count.Returns(0);
            db.Games.Returns(games);
            _api.Database.Returns(db);

            var result = _json.Serialize(_handler.GetStats());
            Assert.That(result, Does.Contain("\"totalGames\":0"));
        }

        [Test]
        public void GetPlugins_ReturnsStructure()
        {
            var result = _json.Serialize(_handler.GetPlugins());
            Assert.That(result, Does.Contain("\"loaded\""));
            Assert.That(result, Does.Contain("\"installed\""));
            Assert.That(result, Does.Contain("\"disabled\""));
        }

        [Test]
        public void SendNotification_ReturnsOk()
        {
            var notifications = Substitute.For<INotificationsAPI>();
            _api.Notifications.Returns(notifications);

            var ctx = new RequestContext
            {
                Path = "/api/notifications",
                Method = "POST",
                Body = new Dictionary<string, object> { { "text", "Hello" } }
            };

            var result = _json.Serialize(_handler.SendNotification(ctx));
            Assert.That(result, Does.Contain("\"ok\":true"));
            notifications.Received().Add(Arg.Any<string>(), "Hello", NotificationType.Info);
        }

        [Test]
        public void SendNotification_ErrorType()
        {
            var notifications = Substitute.For<INotificationsAPI>();
            _api.Notifications.Returns(notifications);

            var ctx = new RequestContext
            {
                Path = "/api/notifications",
                Method = "POST",
                Body = new Dictionary<string, object> { { "text", "Oops" }, { "type", "error" } }
            };

            _handler.SendNotification(ctx);
            notifications.Received().Add(Arg.Any<string>(), "Oops", NotificationType.Error);
        }

        [Test]
        public void GetApiIndex_ContainsEndpoints()
        {
            var result = _json.Serialize(_handler.GetApiIndex());
            Assert.That(result, Does.Contain("Playnite Bridge API"));
            Assert.That(result, Does.Contain("/api/games"));
            Assert.That(result, Does.Contain("/api/eval"));
        }
    }
}
