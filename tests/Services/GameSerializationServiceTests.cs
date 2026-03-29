namespace PlayniteBridge.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Web.Script.Serialization;
    using NUnit.Framework;
    using Playnite.SDK.Models;
    using PlayniteBridge.Services;

    [TestFixture]
    public class GameSerializationServiceTests
    {
        private GameSerializationService _svc;
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            _svc = new GameSerializationService();
            _json = new JavaScriptSerializer();
        }

        private Dictionary<string, object> ToDict(object obj)
        {
            return _json.Deserialize<Dictionary<string, object>>(
                _json.Serialize(obj));
        }

        [Test]
        public void SerializeCompact_BasicGame_IncludesAllFields()
        {
            var game = new Game("Test Game")
            {
                Playtime = 7200,
                PlayCount = 5,
                IsInstalled = true,
                Favorite = true,
                Hidden = false
            };

            var dict = ToDict(_svc.SerializeCompact(game));

            Assert.AreEqual("Test Game", dict["name"]);
            Assert.AreEqual(7200, dict["playtime"]);
            Assert.AreEqual(5, dict["playCount"]);
            Assert.AreEqual(true, dict["isInstalled"]);
            Assert.AreEqual(true, dict["favorite"]);
            Assert.AreEqual(false, dict["hidden"]);
        }

        [Test]
        public void SerializeCompact_IdIsValidGuid()
        {
            var game = new Game("Test");
            var dict = ToDict(_svc.SerializeCompact(game));
            Assert.IsTrue(Guid.TryParse(dict["id"].ToString(), out _));
        }

        [Test]
        public void SerializeCompact_NullCollections_ReturnEmptyArrays()
        {
            var game = new Game("Empty");
            var dict = ToDict(_svc.SerializeCompact(game));

            Assert.IsInstanceOf<System.Collections.ArrayList>(dict["genres"]);
            Assert.AreEqual(0, ((System.Collections.ArrayList)dict["genres"]).Count);
            Assert.AreEqual(0, ((System.Collections.ArrayList)dict["categories"]).Count);
            Assert.AreEqual(0, ((System.Collections.ArrayList)dict["tags"]).Count);
        }

        [Test]
        public void SerializeFull_IncludesExtraFields()
        {
            var game = new Game("Full Game")
            {
                Description = "A great game",
                Notes = "My notes",
                SortingName = "full game, a",
                CommunityScore = 85,
                CriticScore = 90,
                UserScore = 95,
                Version = "1.2.3"
            };

            var dict = ToDict(_svc.SerializeFull(game));

            Assert.AreEqual("A great game", dict["description"]);
            Assert.AreEqual("My notes", dict["notes"]);
            Assert.AreEqual("full game, a", dict["sortingName"]);
            Assert.AreEqual(85, dict["communityScore"]);
            Assert.AreEqual(90, dict["criticScore"]);
            Assert.AreEqual(95, dict["userScore"]);
            Assert.AreEqual("1.2.3", dict["version"]);
        }

        [Test]
        public void SerializeFull_HasCoverFlags()
        {
            var game = new Game("Test") { Icon = "icon.png", CoverImage = "", BackgroundImage = null };
            var dict = ToDict(_svc.SerializeFull(game));
            Assert.AreEqual(true, dict["hasIcon"]);
            Assert.AreEqual(false, dict["hasCover"]);
            Assert.AreEqual(false, dict["hasBackground"]);
        }

        [Test]
        public void SerializeFull_ReleaseDate_FormattedCorrectly()
        {
            var game = new Game("Test") { ReleaseDate = new ReleaseDate(2023, 6, 15) };
            var dict = ToDict(_svc.SerializeFull(game));
            Assert.AreEqual("2023-06-15", dict["releaseDate"]);
        }

        [Test]
        public void SerializeFull_NullReleaseDate()
        {
            var game = new Game("Test");
            var dict = ToDict(_svc.SerializeFull(game));
            Assert.IsNull(dict["releaseDate"]);
        }

        [Test]
        public void SerializeFull_ContainsAllExpectedKeys()
        {
            var game = new Game("Test");
            var dict = ToDict(_svc.SerializeFull(game));

            var expectedKeys = new[] { "id", "name", "sortingName", "description", "notes",
                "genres", "categories", "tags", "features", "platforms", "developers",
                "publishers", "series", "ageRatings", "regions", "source", "gameId",
                "pluginId", "completionStatus", "releaseDate", "isInstalled",
                "installDirectory", "hidden", "favorite", "playtime", "playCount",
                "lastActivity", "added", "modified", "communityScore", "criticScore",
                "userScore", "links", "hasIcon", "hasCover", "hasBackground", "version" };

            foreach (var key in expectedKeys)
                Assert.IsTrue(dict.ContainsKey(key), $"Missing key: {key}");
        }
    }
}
