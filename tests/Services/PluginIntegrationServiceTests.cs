namespace PlayniteBridge.Tests.Services
{
    using System;
    using System.Collections.Generic;
    using System.Web.Script.Serialization;
    using NUnit.Framework;
    using PlayniteBridge.Services;

    [TestFixture]
    public class PluginIntegrationServiceTests
    {
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            _json = new JavaScriptSerializer();
        }

        private Dictionary<string, object> ToDict(object obj) =>
            _json.Deserialize<Dictionary<string, object>>(_json.Serialize(obj));

        [Test]
        public void ParseAchievements_ValidJson_ReturnsCorrectCounts()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{
                ""Name"": ""Test Game"",
                ""Items"": [
                    {""Name"": ""First Blood"", ""Description"": ""Kill one enemy"", ""Percent"": 85.5, ""GamerScore"": 10, ""IsHidden"": false, ""DateUnlocked"": ""2025-01-15T10:30:00Z""},
                    {""Name"": ""Veteran"", ""Description"": ""Finish campaign"", ""Percent"": 12.3, ""GamerScore"": 50, ""IsHidden"": false, ""DateUnlocked"": ""0001-01-01T00:00:00""},
                    {""Name"": ""Secret"", ""Description"": """", ""Percent"": 2.1, ""GamerScore"": 100, ""IsHidden"": true, ""DateUnlocked"": ""2025-02-20T14:00:00Z""}
                ],
                ""SourcesLink"": {""Name"": ""Steam"", ""GameName"": ""Test Game"", ""Url"": ""https://example.com""}
            }";

            var result = ToDict(svc.ParseAchievements(json, Guid.NewGuid(), "Test Game"));

            Assert.AreEqual(3, result["total"]);
            Assert.AreEqual(2, result["unlocked"]);
            Assert.AreEqual(1, result["locked"]);
            Assert.AreEqual("Steam", result["source"]);
            Assert.AreEqual(true, result["installed"]);
        }

        [Test]
        public void ParseAchievements_EmptyItems_ReturnsZeroCounts()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{""Name"": ""Empty"", ""Items"": []}";

            var result = ToDict(svc.ParseAchievements(json, Guid.NewGuid(), "Empty"));
            Assert.AreEqual(0, result["total"]);
            Assert.AreEqual(0, result["unlocked"]);
        }

        [Test]
        public void ParseAchievements_NullDateUnlocked_NotCounted()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{""Name"": ""Game"", ""Items"": [
                {""Name"": ""Ach1"", ""Percent"": 50, ""GamerScore"": 10, ""IsHidden"": false}
            ]}";

            var result = ToDict(svc.ParseAchievements(json, Guid.NewGuid(), "Game"));
            Assert.AreEqual(1, result["total"]);
            Assert.AreEqual(0, result["unlocked"]);
        }

        [Test]
        public void ParseAchievements_NoSourcesLink_SourceIsNull()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{""Name"": ""Game"", ""Items"": []}";

            var result = ToDict(svc.ParseAchievements(json, Guid.NewGuid(), "Game"));
            Assert.IsNull(result["source"]);
        }

        [Test]
        public void ParseActivity_ValidJson_ReturnsCorrectSessions()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{
                ""Name"": ""Test Game"",
                ""Items"": [
                    {""DateSession"": ""2025-10-24T09:00:00Z"", ""ElapsedSeconds"": 3600, ""GameActionName"": ""Play""},
                    {""DateSession"": ""2025-10-25T14:00:00Z"", ""ElapsedSeconds"": 7200, ""GameActionName"": ""Play""}
                ],
                ""SessionPlaytime"": 10800
            }";

            var result = ToDict(svc.ParseActivity(json, Guid.NewGuid(), "Test Game"));
            Assert.AreEqual(2, result["sessionCount"]);
            Assert.AreEqual(10800, result["totalSeconds"]);
            Assert.AreEqual(3.0, result["totalHours"]);
            Assert.AreEqual(true, result["installed"]);
        }

        [Test]
        public void ParseActivity_EmptySessions()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{""Name"": ""Game"", ""Items"": [], ""SessionPlaytime"": 0}";

            var result = ToDict(svc.ParseActivity(json, Guid.NewGuid(), "Game"));
            Assert.AreEqual(0, result["sessionCount"]);
            Assert.AreEqual(0.0, result["totalHours"]);
        }

        [Test]
        public void ParseActivity_NoItems_ReturnsEmpty()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{""Name"": ""Game"", ""SessionPlaytime"": 0}";

            var result = ToDict(svc.ParseActivity(json, Guid.NewGuid(), "Game"));
            Assert.AreEqual(0, result["sessionCount"]);
        }

        [Test]
        public void ParseActivity_LargePlaytime()
        {
            var svc = new PluginIntegrationService("dummy");
            var json = @"{""Name"": ""Game"", ""Items"": [
                {""DateSession"": ""2025-01-01T00:00:00Z"", ""ElapsedSeconds"": 360000, ""GameActionName"": ""Play""}
            ], ""SessionPlaytime"": 360000}";

            var result = ToDict(svc.ParseActivity(json, Guid.NewGuid(), "Game"));
            Assert.AreEqual(100.0, result["totalHours"]);
        }
    }
}
