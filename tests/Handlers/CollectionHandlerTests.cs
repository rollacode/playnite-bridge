namespace PlayniteBridge.Tests.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web.Script.Serialization;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;

    [TestFixture]
    public class CollectionHandlerTests
    {
        private CollectionHandler _handler;
        private IPlayniteAPI _api;
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            _api = Substitute.For<IPlayniteAPI>();
            _handler = new CollectionHandler(_api, new CollectionResolver());
            _json = new JavaScriptSerializer();
        }

        [Test]
        public void GetCollection_Null_ReturnsEmptyList()
        {
            var result = _handler.GetCollection<Category>(null);
            Assert.IsInstanceOf<List<object>>(result);
            Assert.AreEqual(0, ((List<object>)result).Count);
        }

        [Test]
        public void GetEmulators_Null_ReturnsEmptyList()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            db.Emulators.Returns((IItemCollection<Emulator>)null);
            _api.Database.Returns(db);

            var result = _handler.GetEmulators();
            Assert.IsInstanceOf<List<object>>(result);
        }

        [Test]
        public void DeleteCollectionItem_InvalidGuid_ReturnsError()
        {
            var coll = Substitute.For<IItemCollection<Category>>();
            var result = _json.Serialize(_handler.DeleteCollectionItem(coll, "not-a-guid"));
            Assert.That(result, Does.Contain("Invalid ID"));
        }

        [Test]
        public void DeleteCollectionItem_NotFound_ReturnsError()
        {
            var coll = Substitute.For<IItemCollection<Category>>();
            var id = Guid.NewGuid();
            coll.Get(id).Returns((Category)null);
            var result = _json.Serialize(_handler.DeleteCollectionItem(coll, id.ToString()));
            Assert.That(result, Does.Contain("Item not found"));
        }
    }
}
