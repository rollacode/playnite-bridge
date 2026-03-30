using NUnit.Framework;
using NSubstitute;
using Playnite.SDK.Models;
using PlayniteBridge.Helpers;
using System.Collections;
using System.Collections.Generic;

namespace PlayniteBridge.Tests.Helpers
{
    [TestFixture]
    public class CollectionResolverSyncTests
    {
        [Test]
        public void ResolveIds_AcceptsListOfString()
        {
            var resolver = new CollectionResolver();
            var collection = Substitute.For<Playnite.SDK.IItemCollection<Category>>();
            collection.GetEnumerator().Returns(new List<Category>().GetEnumerator());

            // SyncEngine passes List<string>, not ArrayList
            var names = new List<string> { "RPG", "Action" };
            var ids = resolver.ResolveIds(collection, names);

            // Should create 2 categories (not return empty because of ArrayList cast)
            Assert.That(ids.Count, Is.EqualTo(2));
        }

        [Test]
        public void ResolveIds_AcceptsArrayList()
        {
            var resolver = new CollectionResolver();
            var collection = Substitute.For<Playnite.SDK.IItemCollection<Category>>();
            collection.GetEnumerator().Returns(new List<Category>().GetEnumerator());

            // GamesHandler passes ArrayList (from JavaScriptSerializer)
            var names = new ArrayList { "RPG", "Action" };
            var ids = resolver.ResolveIds(collection, names);

            Assert.That(ids.Count, Is.EqualTo(2));
        }

        [Test]
        public void ResolveIds_HandlesNull()
        {
            var resolver = new CollectionResolver();
            var collection = Substitute.For<Playnite.SDK.IItemCollection<Category>>();

            var ids = resolver.ResolveIds(collection, null);
            Assert.That(ids.Count, Is.EqualTo(0));
        }

        [Test]
        public void ResolveIds_HandlesEmptyList()
        {
            var resolver = new CollectionResolver();
            var collection = Substitute.For<Playnite.SDK.IItemCollection<Category>>();

            var ids = resolver.ResolveIds(collection, new List<string>());
            Assert.That(ids.Count, Is.EqualTo(0));
        }
    }
}
