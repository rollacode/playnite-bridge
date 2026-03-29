namespace PlayniteBridge.Tests.Handlers
{
    using System.Collections.Generic;
    using System.Web.Script.Serialization;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    [TestFixture]
    public class QueryHandlerTests
    {
        private QueryHandler _handler;
        private IPlayniteAPI _api;
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            _api = Substitute.For<IPlayniteAPI>();
            var db = Substitute.For<IGameDatabaseAPI>();
            var games = Substitute.For<IItemCollection<Game>>();
            games.GetEnumerator().Returns(new List<Game>().GetEnumerator());
            db.Games.Returns(games);
            _api.Database.Returns(db);

            _handler = new QueryHandler(_api, new GameQueryService(), new GameSerializationService());
            _json = new JavaScriptSerializer();
        }

        [Test]
        public void QueryGames_EmptyDB_ReturnsEmpty()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object>()
            };
            var result = _json.Serialize(_handler.QueryGames(ctx));
            Assert.That(result, Does.Contain("\"total\":0"));
        }

        [Test]
        public void QueryGames_WithGroupBy_ReturnsGrouped()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "groupBy", "source" } }
            };
            var result = _handler.QueryGames(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void QueryGames_InvalidGroupBy_ReturnsError()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "groupBy", "invalidfield" } }
            };
            var result = _json.Serialize(_handler.QueryGames(ctx));
            Assert.That(result, Does.Contain("Invalid groupBy"));
        }

        [Test]
        public void QueryGames_WithPagination_Respected()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "offset", 10 }, { "limit", 5 } }
            };
            var result = _json.Serialize(_handler.QueryGames(ctx));
            Assert.That(result, Does.Contain("\"offset\":10"));
            Assert.That(result, Does.Contain("\"limit\":5"));
        }
    }
}
