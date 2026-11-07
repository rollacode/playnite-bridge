namespace PlayniteBridge.Tests.Handlers
{
    using System.Collections.Generic;
    using System.Web.Script.Serialization;
    using NSubstitute;
    using NUnit.Framework;
    using Playnite.SDK;
    using PlayniteBridge.Handlers;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    [TestFixture]
    public class EvalHandlerTests
    {
        private EvalHandler _handler;
        private JavaScriptSerializer _json;

        [SetUp]
        public void SetUp()
        {
            var api = Substitute.For<IPlayniteAPI>();
            var evalService = new EvalService();
            var jsonHelper = new JsonHelper();
            var serializer = new GameSerializationService();
            _handler = new EvalHandler(api, null, evalService, jsonHelper, serializer);
            _json = new JavaScriptSerializer();
        }

        [Test]
        public void HandleEval_MissingCode_ReturnsError()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object>()
            };
            var result = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(result, Does.Contain("Missing"));
            Assert.That(result, Does.Contain("400"));
        }

        [Test]
        public void HandleEval_EmptyCode_ReturnsError()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "" } }
            };
            var result = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(result, Does.Contain("Missing"));
            Assert.That(result, Does.Contain("400"));
        }

        [Test]
        public void HandleEval_SimpleExpression_ReturnsResult()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "1 + 2" } }
            };
            var result = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(result, Does.Contain("\"success\":true"));
            Assert.That(result, Does.Contain("3"));
        }

        [Test]
        public void HandleEval_CompilationError_ReturnsFalse()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "this is not valid C# code!!!" } }
            };
            var result = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(result, Does.Contain("\"success\":false"));
        }

        [Test]
        public void HandleEval_InvalidTimeoutTypes_Return400()
        {
            var cases = new object[]
            {
                "abc",                                   // string that is not a number
                new Dictionary<string, object>(),        // object
                new object[] { 1 },                      // array
                99999999999999999999m,                   // beyond Int32
            };

            foreach (var value in cases)
            {
                var ctx = new RequestContext
                {
                    Body = new Dictionary<string, object> { { "code", "1" }, { "timeoutMs", value } }
                };

                var json = _json.Serialize(_handler.HandleEval(ctx));
                Assert.That(json, Does.Contain("\"code\":400"), $"value {value} should be rejected");
                Assert.That(json, Does.Contain("timeoutMs"), $"value {value} should name the field");
            }
        }

        [Test]
        public void HandleEval_NullTimeout_FallsBackToDefault()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "42" }, { "timeoutMs", null } }
            };

            var json = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(json, Does.Contain("\"success\":true"), "An explicit null timeout means 'use the default'.");
        }

        [Test]
        public void HandleEval_NumericTimeoutAsString_IsAccepted()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "42" }, { "timeoutMs", "5000" } }
            };

            var json = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(json, Does.Contain("\"success\":true"));
        }

        [Test]
        public void HandleEval_MalformedJsonBody_Returns400NotThrow()
        {
            var ctx = new RequestContext { RawBody = "{\"code\":" };

            var json = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(json, Does.Contain("\"code\":400"));
            Assert.That(json, Does.Contain("Malformed JSON"));
        }

        [Test]
        public void HandleEval_NullBody_Returns400NotThrow()
        {
            var ctx = new RequestContext { RawBody = "" };

            var json = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(json, Does.Contain("\"code\":400"));
        }

        [Test]
        public void HandleEval_CompilationError_ReturnsStructuredDetails()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "this is not valid C# code!!!" } }
            };
            var result = _json.Serialize(_handler.HandleEval(ctx));

            Assert.That(result, Does.Contain("\"errorsDetailed\""));
            Assert.That(result, Does.Contain("ErrorNumber"));
            Assert.That(result, Does.Contain("\"Message\""));
        }

        [Test]
        public void HandleEval_CustomTimeout_Accepted()
        {
            var ctx = new RequestContext
            {
                Body = new Dictionary<string, object> { { "code", "42" }, { "timeoutMs", 5000 } }
            };
            var result = _json.Serialize(_handler.HandleEval(ctx));
            Assert.That(result, Does.Contain("\"success\":true"));
        }
    }
}
