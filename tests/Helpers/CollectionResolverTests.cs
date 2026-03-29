namespace PlayniteBridge.Tests.Helpers
{
    using System.Collections;
    using NUnit.Framework;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;

    [TestFixture]
    public class CollectionResolverTests
    {
        private CollectionResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _resolver = new CollectionResolver();
        }

        // GetNameList requires Game with DB-backed read-only collections
        // (Categories, Tags, etc.) which can't be set in unit tests.
        // These are tested via integration tests against Playnite.

        [Test]
        public void GetNameList_NullCollections_ReturnsEmptyList()
        {
            var game = new Game("Test");
            var result = _resolver.GetNameList(game, "categories");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetNameList_UnknownField_ReturnsEmpty()
        {
            var game = new Game("Test");
            var result = _resolver.GetNameList(game, "unknown");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetNameList_AllSupportedFields_NoException()
        {
            var game = new Game("Test");
            Assert.DoesNotThrow(() => _resolver.GetNameList(game, "categories"));
            Assert.DoesNotThrow(() => _resolver.GetNameList(game, "tags"));
            Assert.DoesNotThrow(() => _resolver.GetNameList(game, "features"));
            Assert.DoesNotThrow(() => _resolver.GetNameList(game, "genres"));
        }

        [Test]
        public void ResolveIds_NullNames_ReturnsEmpty()
        {
            var result = _resolver.ResolveIds<Category>(null, null);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void ResolveIds_EmptyArrayList_ReturnsEmpty()
        {
            var result = _resolver.ResolveIds<Category>(null, new ArrayList());
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void GetOrCreate_NullCollection_ReturnsNull()
        {
            var result = _resolver.GetOrCreate<Category>(null, "test");
            Assert.IsNull(result);
        }

        [Test]
        public void GetOrCreate_EmptyName_ReturnsNull()
        {
            var result = _resolver.GetOrCreate<Category>(null, "");
            Assert.IsNull(result);
        }

        [Test]
        public void GetOrCreate_NullName_ReturnsNull()
        {
            var result = _resolver.GetOrCreate<Category>(null, null);
            Assert.IsNull(result);
        }
    }
}
