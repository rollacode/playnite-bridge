namespace PlayniteBridge.Tests.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Web.Script.Serialization;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    [TestFixture]
    public class GamesHandlerTests
    {
        private GamesHandler _handler;
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

            _handler = new GamesHandler(_api, new CollectionResolver(), new GameSerializationService());
            _json = new JavaScriptSerializer();
        }

        [Test]
        public void GetGame_NotFound_ReturnsError()
        {
            var id = Guid.NewGuid();
            _db.Games.Get(id).Returns((Game)null);
            var result = _json.Serialize(_handler.GetGame(id));
            Assert.That(result, Does.Contain("Game not found"));
        }

        [Test]
        public void GetGame_Found_ReturnsDetails()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test Game") { Id = id };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.GetGame(id));
            Assert.That(result, Does.Contain("Test Game"));
        }

        [Test]
        public void DeleteGame_NotFound_ReturnsError()
        {
            var id = Guid.NewGuid();
            _db.Games.Get(id).Returns((Game)null);
            var result = _json.Serialize(_handler.DeleteGame(id));
            Assert.That(result, Does.Contain("Game not found"));
        }

        [Test]
        public void DeleteGame_Found_ReturnsOk()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test Game") { Id = id };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.DeleteGame(id));
            Assert.That(result, Does.Contain("\"ok\":true"));
            Assert.That(result, Does.Contain("Test Game"));
            _db.Games.Received().Remove(id);
        }

        [Test]
        public void LaunchGame_NotFound_ReturnsError()
        {
            var id = Guid.NewGuid();
            _db.Games.Get(id).Returns((Game)null);
            var result = _json.Serialize(_handler.LaunchGame(id));
            Assert.That(result, Does.Contain("Game not found"));
        }

        [Test]
        public void LaunchGame_NotInstalled_ReturnsError()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test") { Id = id, IsInstalled = false };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.LaunchGame(id));
            Assert.That(result, Does.Contain("not installed"));
        }

        [Test]
        public void InstallGame_AlreadyInstalled_ReturnsAlready()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test") { Id = id, IsInstalled = true };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.InstallGame(id));
            Assert.That(result, Does.Contain("already_installed"));
        }

        [Test]
        public void UninstallGame_NotInstalled_ReturnsNotInstalled()
        {
            var id = Guid.NewGuid();
            var game = new Game("Test") { Id = id, IsInstalled = false };
            _db.Games.Get(id).Returns(game);
            var result = _json.Serialize(_handler.UninstallGame(id));
            Assert.That(result, Does.Contain("not_installed"));
        }

        [Test]
        public void SetCompletionStatus_GameNotFound_ReturnsError()
        {
            var id = Guid.NewGuid();
            _db.Games.Get(id).Returns((Game)null);
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "status", "Completed" } }
            };
            var result = _json.Serialize(_handler.SetCompletionStatus(id, ctx));
            Assert.That(result, Does.Contain("Game not found"));
        }

        [Test]
        public void GetMissingArt_EmptyDB_ReturnsZero()
        {
            var games = Substitute.For<IItemCollection<Game>>();
            games.GetEnumerator().Returns(new List<Game>().GetEnumerator());
            games.Count.Returns(0);
            _db.Games.Returns(games);

            var result = _json.Serialize(_handler.GetMissingArt());
            Assert.That(result, Does.Contain("\"total\":0"));
            Assert.That(result, Does.Contain("\"missingArt\":0"));
        }

        [Test]
        public void GetGames_EmptyDB_ReturnsEmptyList()
        {
            var games = Substitute.For<IItemCollection<Game>>();
            games.GetEnumerator().Returns(new List<Game>().GetEnumerator());
            _db.Games.Returns(games);

            var ctx = new RequestContext { Path = "/api/games", Method = "GET" };
            var result = _json.Serialize(_handler.GetGames(ctx));
            Assert.That(result, Does.Contain("\"total\":0"));
        }
    }
}
