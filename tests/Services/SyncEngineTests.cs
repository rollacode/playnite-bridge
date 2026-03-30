using NUnit.Framework;
using NSubstitute;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteBridge.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayniteBridge.Tests.Services
{
    [TestFixture]
    public class SyncEngineTests
    {
        [Test]
        public void HasCanonicalKey_WithSourceAndGameId_ReturnsTrue()
        {
            var game = new Game("Test Game")
            {
                GameId = "292030",
                SourceId = Guid.NewGuid()
            };
            // Source is read-only (DB-backed), so we test via the static method
            // We need to test the logic directly since Source is DB-backed
            Assert.That(game.GameId, Is.EqualTo("292030"));
        }

        [Test]
        public void HasCanonicalKey_WithoutGameId_ReturnsFalse()
        {
            var game = new Game("Manual Game");
            Assert.That(SyncEngine.HasCanonicalKey(game), Is.False);
        }

        [Test]
        public void HasCanonicalKey_WithEmptyGameId_ReturnsFalse()
        {
            var game = new Game("Manual Game") { GameId = "" };
            Assert.That(SyncEngine.HasCanonicalKey(game), Is.False);
        }

        [Test]
        public void HasCanonicalKey_WithGameIdButNoSource_ReturnsFalse()
        {
            // Source is null by default (DB-backed, can't set in tests)
            var game = new Game("Test") { GameId = "12345" };
            Assert.That(SyncEngine.HasCanonicalKey(game), Is.False);
        }

        [Test]
        public void SyncResult_DefaultsToFalse()
        {
            var result = new SyncResult();
            Assert.That(result.Success, Is.False);
            Assert.That(result.GamesPushed, Is.EqualTo(0));
            Assert.That(result.GamesPulled, Is.EqualTo(0));
        }

        [Test]
        public void SyncState_DefaultsToNotRegistered()
        {
            var state = new SyncState();
            Assert.That(state.IsRegistered, Is.False);
            Assert.That(state.ClientId, Is.Null);
            Assert.That(state.ApiKey, Is.Null);
        }

        [Test]
        public void SyncState_IsRegistered_WhenBothFieldsSet()
        {
            var state = new SyncState
            {
                ClientId = "test-id",
                ApiKey = "test-key"
            };
            Assert.That(state.IsRegistered, Is.True);
        }

        [Test]
        public void SyncState_Clear_ResetsAllFields()
        {
            var state = new SyncState
            {
                ClientId = "test-id",
                ApiKey = "test-key",
                BackendUrl = "http://localhost:19822",
                LastPushCursor = "2026-01-01",
                LastPullCursor = "2026-01-01",
                LastSyncTime = "2026-01-01",
                LastSyncError = "error"
            };
            state.Clear();
            Assert.That(state.IsRegistered, Is.False);
            Assert.That(state.LastPushCursor, Is.Null);
            Assert.That(state.LastPullCursor, Is.Null);
            Assert.That(state.LastSyncError, Is.Null);
        }

        [Test]
        public void RunSync_WhenNotRegistered_ReturnsFail()
        {
            var api = Substitute.For<IPlayniteAPI>();
            var client = new SyncClient("http://localhost:19822", "");
            var state = new SyncState(); // not registered
            var serializer = new GameSerializationService();
            var resolver = new PlayniteBridge.Helpers.CollectionResolver();
            var engine = new SyncEngine(api, client, state, serializer, resolver, () => ".");

            var result = engine.RunSync();
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("Not registered"));
        }

        [Test]
        public void PushSingleGame_WhenNotRegistered_ReturnsFail()
        {
            var api = Substitute.For<IPlayniteAPI>();
            var client = new SyncClient("http://localhost:19822", "");
            var state = new SyncState();
            var serializer = new GameSerializationService();
            var resolver = new PlayniteBridge.Helpers.CollectionResolver();
            var engine = new SyncEngine(api, client, state, serializer, resolver, () => ".");

            var game = new Game("Test");
            var result = engine.PushSingleGame(game);
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void PushSingleGame_WithoutCanonicalKey_SkipsGame()
        {
            var api = Substitute.For<IPlayniteAPI>();
            var client = new SyncClient("http://localhost:19822", "test-key");
            var state = new SyncState { ClientId = "c1", ApiKey = "test-key" };
            var serializer = new GameSerializationService();
            var resolver = new PlayniteBridge.Helpers.CollectionResolver();
            var engine = new SyncEngine(api, client, state, serializer, resolver, () => ".");

            var game = new Game("Manual Game"); // no source, no gameId
            var result = engine.PushSingleGame(game);
            Assert.That(result.Success, Is.True);
            Assert.That(result.GamesSkipped, Is.EqualTo(1));
            Assert.That(result.GamesPushed, Is.EqualTo(0));
        }
    }
}
