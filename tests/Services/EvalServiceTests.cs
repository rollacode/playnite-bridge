namespace PlayniteBridge.Tests.Services
{
    using System.Reflection;
    using NUnit.Framework;
    using PlayniteBridge.Services;

    [TestFixture]
    public class EvalServiceTests
    {
        private EvalService _svc;

        [SetUp]
        public void SetUp()
        {
            _svc = new EvalService();
        }

        // --- IsExpression ---

        [Test]
        public void IsExpression_NoSemicolon_True()
        {
            Assert.IsTrue(_svc.IsExpression("1 + 1"));
        }

        [Test]
        public void IsExpression_WithSemicolon_False()
        {
            Assert.IsFalse(_svc.IsExpression("var x = 1; return x;"));
        }

        // --- GenerateSource ---

        [Test]
        public void GenerateSource_Expression_WrapsInReturn()
        {
            var source = _svc.GenerateSource("42", true);
            Assert.IsTrue(source.Contains("return (42);"));
            Assert.IsTrue(source.Contains("public class ScriptHost"));
        }

        [Test]
        public void GenerateSource_Statements_NoAutoReturn()
        {
            var source = _svc.GenerateSource("var x = 1; return x;", false);
            Assert.IsTrue(source.Contains("var x = 1; return x;"));
            Assert.IsTrue(source.Contains("return null;")); // fallback
        }

        [Test]
        public void GenerateSource_IncludesUsings()
        {
            var source = _svc.GenerateSource("1", true);
            Assert.IsTrue(source.Contains("using System;"));
            Assert.IsTrue(source.Contains("using System.Linq;"));
            Assert.IsTrue(source.Contains("using Playnite.SDK;"));
            Assert.IsTrue(source.Contains("using Playnite.SDK.Models;"));
        }

        // --- Compile ---

        [Test]
        public void Compile_ValidExpression_Succeeds()
        {
            var source = _svc.GenerateSource("1 + 1", true);
            var result = _svc.Compile(source);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Result);
            Assert.IsInstanceOf<MethodInfo>(result.Result);
        }

        [Test]
        public void Compile_InvalidCode_ReturnsErrors()
        {
            var source = _svc.GenerateSource("this is not valid c#;;;", false);
            var result = _svc.Compile(source);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Compilation failed", result.Error);
            Assert.IsNotNull(result.CompilationErrors);
            Assert.IsTrue(result.CompilationErrors.Count > 0);
        }

        // --- Execute ---

        [Test]
        public void Execute_SimpleExpression_ReturnsResult()
        {
            var source = _svc.GenerateSource("1 + 1", true);
            var compiled = _svc.Compile(source);
            Assert.IsTrue(compiled.Success);

            var method = (MethodInfo)compiled.Result;
            var result = _svc.Execute(method, new object[] { null, null }, 5000, false, null);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Result);
        }

        [Test]
        public void Execute_StringExpression_ReturnsString()
        {
            var source = _svc.GenerateSource("\"hello\" + \" world\"", true);
            var compiled = _svc.Compile(source);
            var method = (MethodInfo)compiled.Result;
            var result = _svc.Execute(method, new object[] { null, null }, 5000, false, null);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("hello world", result.Result);
            Assert.AreEqual("String", result.ResultType);
        }

        [Test]
        public void Execute_Timeout_ReturnsError()
        {
            var source = _svc.GenerateSource("System.Threading.Thread.Sleep(10000); return 1;", false);
            var compiled = _svc.Compile(source);
            var method = (MethodInfo)compiled.Result;
            var result = _svc.Execute(method, new object[] { null, null }, 500, false, null);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("timed out"));
        }

        [Test]
        public void Execute_RuntimeError_ReturnsException()
        {
            var source = _svc.GenerateSource("throw new System.InvalidOperationException(\"test error\");", false);
            var compiled = _svc.Compile(source);
            var method = (MethodInfo)compiled.Result;
            var result = _svc.Execute(method, new object[] { null, null }, 5000, false, null);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("test error", result.Error);
            Assert.AreEqual("InvalidOperationException", result.ExceptionType);
        }

        [Test]
        public void Execute_NullReturn_Succeeds()
        {
            var source = _svc.GenerateSource("return null;", false);
            var compiled = _svc.Compile(source);
            var method = (MethodInfo)compiled.Result;
            var result = _svc.Execute(method, new object[] { null, null }, 5000, false, null);
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Result);
        }

        [Test]
        public void Execute_WithUiDispatcher_CallsDispatcher()
        {
            var source = _svc.GenerateSource("42", true);
            var compiled = _svc.Compile(source);
            var method = (MethodInfo)compiled.Result;

            bool dispatcherCalled = false;
            var result = _svc.Execute(method, new object[] { null, null }, 5000, true, action =>
            {
                dispatcherCalled = true;
                action();
            });
            Assert.IsTrue(result.Success);
            Assert.AreEqual(42, result.Result);
            Assert.IsTrue(dispatcherCalled);
        }
    }
}
