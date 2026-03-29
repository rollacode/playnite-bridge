namespace PlayniteBridge.Tests.Helpers
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using PlayniteBridge.Helpers;

    [TestFixture]
    public class DictExtensionsTests
    {
        [Test]
        public void GetValueOrNull_ExistingKey_ReturnsValue()
        {
            var dict = new Dictionary<string, string> { { "key", "value" } };
            Assert.AreEqual("value", dict.GetValueOrNull("key"));
        }

        [Test]
        public void GetValueOrNull_MissingKey_ReturnsNull()
        {
            var dict = new Dictionary<string, string> { { "key", "value" } };
            Assert.IsNull(dict.GetValueOrNull("missing"));
        }

        [Test]
        public void GetValueOrDefault_ExistingKey_ReturnsValue()
        {
            var dict = new Dictionary<string, int> { { "key", 42 } };
            Assert.AreEqual(42, dict.GetValueOrDefault("key", 0));
        }

        [Test]
        public void GetValueOrDefault_MissingKey_ReturnsDefault()
        {
            var dict = new Dictionary<string, int> { { "key", 42 } };
            Assert.AreEqual(99, dict.GetValueOrDefault("missing", 99));
        }

        [Test]
        public void GetValueOrNull_EmptyDict_ReturnsNull()
        {
            var dict = new Dictionary<string, string>();
            Assert.IsNull(dict.GetValueOrNull("anything"));
        }
    }
}
