namespace PlayniteBridge.Tests.Server
{
    using System;
    using System.Collections.Generic;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    [TestFixture]
    public class RouterTests
    {
        private Router _router;
        private IPlayniteAPI _api;

        [SetUp]
        public void SetUp()
        {
            _api = Substitute.For<IPlayniteAPI>();
            var db = Substitute.For<IGameDatabaseAPI>();
            _api.Database.Returns(db);

            db.Categories.Returns(Substitute.For<IItemCollection<Category>>());
            db.Tags.Returns(Substitute.For<IItemCollection<Tag>>());
            db.Genres.Returns(Substitute.For<IItemCollection<Genre>>());
            db.Features.Returns(Substitute.For<IItemCollection<GameFeature>>());
            db.Platforms.Returns(Substitute.For<IItemCollection<Platform>>());
            db.Sources.Returns(Substitute.For<IItemCollection<GameSource>>());
            db.Companies.Returns(Substitute.For<IItemCollection<Company>>());
            db.Series.Returns(Substitute.For<IItemCollection<Series>>());
            db.AgeRatings.Returns(Substitute.For<IItemCollection<AgeRating>>());
            db.Regions.Returns(Substitute.For<IItemCollection<Region>>());
            db.CompletionStatuses.Returns(Substitute.For<IItemCollection<CompletionStatus>>());
            db.FilterPresets.Returns(Substitute.For<IItemCollection<FilterPreset>>());
            db.Emulators.Returns(Substitute.For<IItemCollection<Emulator>>());
            db.Games.Returns(Substitute.For<IItemCollection<Game>>());

            var resolver = new CollectionResolver();
            var serializer = new GameSerializationService();
            var queryService = new GameQueryService();
            var evalService = new EvalService();
            var jsonHelper = new JsonHelper();
            var pluginService = new PluginIntegrationService("C:\\fake\\path");

            var auth = new AuthHandler(() => "C:\\fake\\data", () => false, () => "fake skill");
            auth.LoadOrCreateToken();

            var games = new GamesHandler(_api, resolver, serializer);
            var query = new QueryHandler(_api, queryService, serializer);
            var collections = new CollectionHandler(_api, resolver);
            var view = new ViewHandler(_api, serializer);
            var app = new AppHandler(_api, 19821);
            var automation = new AutomationHandler(_api, resolver, () => "C:\\fake\\data");
            var pluginData = new PluginDataHandler(_api, pluginService);
            var eval = new EvalHandler(_api, null, evalService, jsonHelper, serializer);

            _router = new Router(_api, games, query, collections, view, app, automation, auth, pluginData, eval);
        }

        [Test]
        public void Route_ApiIndex_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_UnknownPath_ReturnsNull()
        {
            var ctx = new RequestContext { Path = "/api/nonexistent", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNull(result);
        }

        [Test]
        public void Route_AuthRotate_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/auth/rotate", Method = "POST" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_SkillMd_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/skill.md", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetGames_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/games", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetCategories_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/categories", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetGenres_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/genres", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetTags_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/tags", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetFeatures_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/features", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetPlatforms_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/platforms", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetSources_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/sources", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetCompanies_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/companies", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetSeries_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/series", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetAgeRatings_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/age-ratings", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetRegions_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/regions", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetCompletionStatuses_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/completion-statuses", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetFilterPresets_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/filter-presets", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetEmulators_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/emulators", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetMissingArt_ReturnsResult()
        {
            var ctx = new RequestContext { Path = "/api/games/missing-art", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GetGameById_NotFound()
        {
            var id = Guid.NewGuid();
            _api.Database.Games.Get(id).Returns((Game)null);
            var ctx = new RequestContext { Path = $"/api/games/{id}", Method = "GET" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
            var json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result);
            Assert.That(json, Does.Contain("Game not found"));
        }

        [Test]
        public void Route_WrongMethod_ReturnsNull()
        {
            var ctx = new RequestContext { Path = "/api/games", Method = "DELETE" };
            var result = _router.Route(ctx);
            Assert.IsNull(result);
        }

        [TestCase("/api/games/query", "POST")]
        [TestCase("/api/auto-categorize", "POST")]
        [TestCase("/api/stats", "GET")]
        public void Route_VariousEndpoints_ReturnNonNull(string path, string method)
        {
            var body = new Dictionary<string, object>();
            var ctx = new RequestContext { Path = path, Method = method, Body = body };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
        }

        [Test]
        public void Route_GameAction_Launch_NotFound()
        {
            var id = Guid.NewGuid();
            _api.Database.Games.Get(id).Returns((Game)null);
            var ctx = new RequestContext { Path = $"/api/games/{id}/launch", Method = "POST" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
            var json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result);
            Assert.That(json, Does.Contain("Game not found"));
        }

        [Test]
        public void Route_DeleteCategory_InvalidId()
        {
            var ctx = new RequestContext { Path = "/api/categories/not-a-guid", Method = "DELETE" };
            var result = _router.Route(ctx);
            Assert.IsNotNull(result);
            var json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result);
            Assert.That(json, Does.Contain("Invalid ID"));
        }
    }
}
