namespace PlayniteBridge.Tests.Handlers
{
    using System.IO;
    using NUnit.Framework;
    using PlayniteBridge.Handlers;

    [TestFixture]
    public class AuthHandlerTests
    {
        private string _tempDir;
        private AuthHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "pb_test_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
            _handler = new AuthHandler(() => _tempDir, () => false, () => "test skill content");
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        [Test]
        public void LoadOrCreateToken_CreatesNewToken()
        {
            _handler.LoadOrCreateToken();
            Assert.IsNotNull(_handler.ApiToken);
            Assert.That(_handler.ApiToken, Does.StartWith("pb_"));
        }

        [Test]
        public void LoadOrCreateToken_PersistsAndLoads()
        {
            _handler.LoadOrCreateToken();
            var firstToken = _handler.ApiToken;

            var handler2 = new AuthHandler(() => _tempDir, () => false, () => "test");
            handler2.LoadOrCreateToken();
            Assert.AreEqual(firstToken, handler2.ApiToken);
        }

        [Test]
        public void RotateToken_ChangesToken()
        {
            _handler.LoadOrCreateToken();
            var oldToken = _handler.ApiToken;
            _handler.RotateToken();
            Assert.AreNotEqual(oldToken, _handler.ApiToken);
            Assert.That(_handler.ApiToken, Does.StartWith("pb_"));
        }

        [Test]
        public void HandleRotateToken_ReturnsNewToken()
        {
            _handler.LoadOrCreateToken();
            var result = _handler.HandleRotateToken();
            Assert.IsNotNull(result);
            var json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result);
            Assert.That(json, Does.Contain("pb_"));
            Assert.That(json, Does.Contain("\"ok\":true"));
        }

        [Test]
        public void HandleGetSkillMd_ReturnsContent()
        {
            var result = _handler.HandleGetSkillMd();
            var json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result);
            Assert.That(json, Does.Contain("test skill content"));
        }

        [Test]
        public void ValidateAuth_ValidToken_ReturnsTrue()
        {
            _handler.LoadOrCreateToken();
            Assert.IsTrue(_handler.ValidateAuth($"Bearer {_handler.ApiToken}"));
        }

        [Test]
        public void ValidateAuth_InvalidToken_ReturnsFalse()
        {
            _handler.LoadOrCreateToken();
            Assert.IsFalse(_handler.ValidateAuth("Bearer wrong_token"));
        }

        [Test]
        public void ValidateAuth_NullHeader_ReturnsFalse()
        {
            _handler.LoadOrCreateToken();
            Assert.IsFalse(_handler.ValidateAuth(null));
        }

        [Test]
        public void ValidateAuth_EmptyHeader_ReturnsFalse()
        {
            _handler.LoadOrCreateToken();
            Assert.IsFalse(_handler.ValidateAuth(""));
        }

        [Test]
        public void ValidateAuth_NoBearerPrefix_ReturnsFalse()
        {
            _handler.LoadOrCreateToken();
            Assert.IsFalse(_handler.ValidateAuth(_handler.ApiToken));
        }

        [Test]
        public void Token_Length_Is51Chars()
        {
            _handler.LoadOrCreateToken();
            // pb_ (3) + 48 hex chars = 51
            Assert.AreEqual(51, _handler.ApiToken.Length);
        }
    }
}
