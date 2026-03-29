namespace PlayniteBridge.Tests.Server
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using PlayniteBridge.Server;

    [TestFixture]
    public class RequestContextTests
    {
        [Test]
        public void Body_ParsedLazily_FromRawBody()
        {
            var ctx = new RequestContext
            {
                Path = "/api/test",
                Method = "POST",
                RawBody = "{\"key\": \"value\", \"num\": 42}"
            };

            Assert.IsNotNull(ctx.Body);
            Assert.AreEqual("value", ctx.Body["key"]);
            Assert.AreEqual(42, ctx.Body["num"]);
        }

        [Test]
        public void Body_NullRawBody_ReturnsNull()
        {
            var ctx = new RequestContext { Path = "/api/test", Method = "GET" };
            Assert.IsNull(ctx.Body);
        }

        [Test]
        public void Body_EmptyRawBody_ReturnsNull()
        {
            var ctx = new RequestContext { Path = "/api/test", Method = "GET", RawBody = "" };
            Assert.IsNull(ctx.Body);
        }

        [Test]
        public void Body_SetDirectly_SkipsJsonParsing()
        {
            var ctx = new RequestContext
            {
                Path = "/api/test",
                Method = "POST",
                Body = new Dictionary<string, object> { { "direct", true } }
            };

            Assert.IsTrue((bool)ctx.Body["direct"]);
        }

        [Test]
        public void Body_CachedAfterFirstAccess()
        {
            var ctx = new RequestContext
            {
                RawBody = "{\"a\": 1}"
            };

            var first = ctx.Body;
            var second = ctx.Body;
            Assert.AreSame(first, second);
        }
    }
}
