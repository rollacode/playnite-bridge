using NUnit.Framework;
using NSubstitute;
using Playnite.SDK;
using Playnite.SDK.Models;
using PlayniteBridge.Services;
using System;
using System.IO;
using System.Security.Cryptography;

namespace PlayniteBridge.Tests.Services
{
    [TestFixture]
    public class ImageSyncTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"playnite_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }

        [Test]
        public void ComputeFileHash_ReturnsNull_ForNullPath()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            var hash = GameSerializationService.ComputeFileHash(db, null);
            Assert.That(hash, Is.Null);
        }

        [Test]
        public void ComputeFileHash_ReturnsNull_ForEmptyPath()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            var hash = GameSerializationService.ComputeFileHash(db, "");
            Assert.That(hash, Is.Null);
        }

        [Test]
        public void ComputeFileHash_ReturnsNull_ForHttpUrl()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            var hash = GameSerializationService.ComputeFileHash(db, "https://example.com/image.jpg");
            Assert.That(hash, Is.Null);
        }

        [Test]
        public void ComputeFileHash_ReturnsHash_ForRealFile()
        {
            var testFile = Path.Combine(_tempDir, "test.jpg");
            File.WriteAllBytes(testFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 });

            var db = Substitute.For<IGameDatabaseAPI>();
            db.GetFullFilePath("some/path.jpg").Returns(testFile);

            var hash = GameSerializationService.ComputeFileHash(db, "some/path.jpg");
            Assert.That(hash, Is.Not.Null);
            Assert.That(hash.Length, Is.EqualTo(64)); // SHA-256 = 64 hex chars
        }

        [Test]
        public void ComputeFileHash_IsDeterministic()
        {
            var testFile = Path.Combine(_tempDir, "test2.jpg");
            File.WriteAllBytes(testFile, new byte[] { 1, 2, 3, 4, 5 });

            var db = Substitute.For<IGameDatabaseAPI>();
            db.GetFullFilePath("path").Returns(testFile);

            var hash1 = GameSerializationService.ComputeFileHash(db, "path");
            var hash2 = GameSerializationService.ComputeFileHash(db, "path");
            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void ComputeFileHash_DifferentContent_DifferentHash()
        {
            var file1 = Path.Combine(_tempDir, "a.jpg");
            var file2 = Path.Combine(_tempDir, "b.jpg");
            File.WriteAllBytes(file1, new byte[] { 1, 2, 3 });
            File.WriteAllBytes(file2, new byte[] { 4, 5, 6 });

            var db = Substitute.For<IGameDatabaseAPI>();
            db.GetFullFilePath("a").Returns(file1);
            db.GetFullFilePath("b").Returns(file2);

            var hash1 = GameSerializationService.ComputeFileHash(db, "a");
            var hash2 = GameSerializationService.ComputeFileHash(db, "b");
            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void ComputeFileHash_ReturnsNull_ForMissingFile()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            db.GetFullFilePath("missing").Returns(Path.Combine(_tempDir, "nonexistent.jpg"));

            var hash = GameSerializationService.ComputeFileHash(db, "missing");
            Assert.That(hash, Is.Null);
        }

        [Test]
        public void ReadImageFile_ReturnsBytes_ForRealFile()
        {
            var testFile = Path.Combine(_tempDir, "img.png");
            var content = new byte[] { 0x89, 0x50, 0x4E, 0x47, 10, 20, 30 };
            File.WriteAllBytes(testFile, content);

            var db = Substitute.For<IGameDatabaseAPI>();
            db.GetFullFilePath("img").Returns(testFile);

            var bytes = GameSerializationService.ReadImageFile(db, "img");
            Assert.That(bytes, Is.EqualTo(content));
        }

        [Test]
        public void ReadImageFile_ReturnsNull_ForHttpUrl()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            var bytes = GameSerializationService.ReadImageFile(db, "http://example.com/img.jpg");
            Assert.That(bytes, Is.Null);
        }

        [Test]
        public void ReadImageFile_ReturnsNull_ForMissing()
        {
            var db = Substitute.For<IGameDatabaseAPI>();
            db.GetFullFilePath("x").Returns(Path.Combine(_tempDir, "nope.jpg"));

            var bytes = GameSerializationService.ReadImageFile(db, "x");
            Assert.That(bytes, Is.Null);
        }
    }
}
