namespace PlayniteBridge.Tests.Handlers
{
    using System;
    using System.Web.Script.Serialization;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Services;

    [TestFixture]
    public class PluginDataHandlerTests
    {
        private PluginDataHandler _handler;
        private IPlayniteAPI _api;
        private IGameDatabaseAPI _db;
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            _api = Substitute.For<IPlayniteAPI>();
            _db = Substitute.For<IGameDatabaseAPI>();
            _api.Database.Returns(_db);
            _db.Games.Returns(Substitute.For<IItemCollection<Game>>());

            var pluginService = new PluginIntegrationService("C:\\fake\\extensions");
            _handler = new PluginDataHandler(_api, pluginService);
            _json = new JavaScriptSerializer();
        }

        [Test]
        public void GetAchievements_GameNotFound_ReturnsError()
        {
            var id = Guid.NewGuid();
            _db.Games.Get(id).Returns((Game)null);
            var result = _json.Serialize(_handler.GetAchievements(id));
            Assert.That(result, Does.Contain("Game not found"));
        }

        [Test]
        public void GetAchievements_NoPluginData_ReturnsNotInstalled()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test") { Id = id };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.GetAchievements(id));
            Assert.That(result, Does.Contain("SuccessStory plugin data not found"));
        }

        [Test]
        public void GetActivity_GameNotFound_ReturnsError()
        {
            var id = Guid.NewGuid();
            _db.Games.Get(id).Returns((Game)null);
            var result = _json.Serialize(_handler.GetActivity(id));
            Assert.That(result, Does.Contain("Game not found"));
        }

        [Test]
        public void GetActivity_NoPluginData_ReturnsNotInstalled()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test") { Id = id };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.GetActivity(id));
            Assert.That(result, Does.Contain("GameActivity plugin data not found"));
        }
    }
}
