namespace PlayniteBridge.Tests.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Playnite.SDK.Models;
    using PlayniteBridge.Services;

    [TestFixture]
    public class GameQueryServiceTests
    {
        private GameQueryService _svc;
        private List<Game> _games;

        [SetUp]
        public void SetUp()
        {
            _svc = new GameQueryService();

            // Note: Game.Source, Game.Genres, Game.Developers are read-only in Playnite SDK
            // (loaded from DB). We can only set Ids and direct properties, or set writable lists
            // where the SDK allows it. For filter testing we rely on properties that ARE settable.
            _games = new List<Game>
            {
                MakeGame("Witcher 3", playtime: 108000, installed: true, hidden: false, favorite: true, year: 2015),
                MakeGame("Factorio", playtime: 640000, installed: true, hidden: false, year: 2020),
                MakeGame("Overwatch 2", playtime: 1030000, installed: false, hidden: false, year: 2022),
                MakeGame("Hidden Game", playtime: 100, installed: false, hidden: true, year: 2023),
                MakeGame("Empty Game", playtime: 0, installed: false, hidden: false, year: 2021),
            };
        }

        // --- Filter: hidden ---

        [Test]
        public void Filter_NoFilters_ExcludesHidden()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object>()).ToList();
            Assert.AreEqual(4, result.Count);
            Assert.IsFalse(result.Any(g => g.Name == "Hidden Game"));
        }

        [Test]
        public void Filter_HiddenTrue_IncludesHidden()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "hidden", true } }).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Hidden Game", result[0].Name);
        }

        [Test]
        public void Filter_HiddenFalse_ExcludesHidden()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "hidden", false } }).ToList();
            Assert.AreEqual(4, result.Count);
        }

        // --- Filter: text search ---

        [Test]
        public void Filter_ByName_CaseInsensitive()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "q", "witcher" } }).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Witcher 3", result[0].Name);
        }

        [Test]
        public void Filter_ByName_Substring()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "q", "Game" } }).ToList();
            Assert.AreEqual(1, result.Count); // "Empty Game" (Hidden Game excluded by default)
        }

        [Test]
        public void Filter_ByName_NoMatch()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "q", "nonexistent" } }).ToList();
            Assert.AreEqual(0, result.Count);
        }

        // --- Filter: booleans ---

        [Test]
        public void Filter_Installed()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "installed", true } }).ToList();
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(g => g.IsInstalled));
        }

        [Test]
        public void Filter_NotInstalled()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "installed", false } }).ToList();
            Assert.IsTrue(result.All(g => !g.IsInstalled));
        }

        [Test]
        public void Filter_Favorite()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "favorite", true } }).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Witcher 3", result[0].Name);
        }

        // --- Filter: playtime ---

        [Test]
        public void Filter_PlaytimeMin()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "playtimeMin", 500000 } }).ToList();
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Filter_PlaytimeMax()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "playtimeMax", 0 } }).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Empty Game", result[0].Name);
        }

        [Test]
        public void Filter_PlaytimeRange()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "playtimeMin", 100000 }, { "playtimeMax", 700000 } }).ToList();
            Assert.AreEqual(2, result.Count);
        }

        // --- Filter: release year ---

        [Test]
        public void Filter_ReleaseYearMin()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "releaseYearMin", 2022 } }).ToList();
            Assert.AreEqual(1, result.Count); // Overwatch 2 (Hidden excluded)
        }

        [Test]
        public void Filter_ReleaseYearRange()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "releaseYearMin", 2020 }, { "releaseYearMax", 2021 } }).ToList();
            Assert.AreEqual(2, result.Count);
        }

        // --- Filter: uncategorized ---

        [Test]
        public void Filter_Uncategorized()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object> { { "uncategorized", true } }).ToList();
            Assert.AreEqual(4, result.Count); // all non-hidden have no categories
        }

        // --- Filter: combined ---

        [Test]
        public void Filter_Combined_InstalledAndPlaytime()
        {
            var result = _svc.Filter(_games, new Dictionary<string, object>
            {
                { "installed", true },
                { "playtimeMin", 200000 }
            }).ToList();
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Factorio", result[0].Name);
        }

        // --- Sort ---

        [Test]
        public void Sort_ByName_Ascending()
        {
            var result = _svc.Sort(_games, "name", false).ToList();
            Assert.AreEqual("Empty Game", result[0].Name);
        }

        [Test]
        public void Sort_ByName_Descending()
        {
            var result = _svc.Sort(_games, "name", true).ToList();
            Assert.AreEqual("Witcher 3", result[0].Name);
        }

        [Test]
        public void Sort_ByPlaytime_Descending()
        {
            var result = _svc.Sort(_games, "playtime", true).ToList();
            Assert.AreEqual("Overwatch 2", result[0].Name);
        }

        [Test]
        public void Sort_ByPlaytime_Ascending()
        {
            var result = _svc.Sort(_games, "playtime", false).ToList();
            Assert.AreEqual("Empty Game", result[0].Name);
        }

        [Test]
        public void Sort_DefaultNull_SortsByName()
        {
            var result = _svc.Sort(_games, null, false).ToList();
            Assert.AreEqual("Empty Game", result[0].Name);
        }

        [Test]
        public void Sort_ByRelease()
        {
            var withDates = _games.Where(g => g.ReleaseDate != null).ToList();
            var result = _svc.Sort(withDates, "release", true).ToList();
            Assert.AreEqual("Hidden Game", result[0].Name); // 2023
        }

        [Test]
        public void Sort_ByAdded()
        {
            var result = _svc.Sort(_games, "added", false).ToList();
            Assert.IsNotNull(result);
            Assert.AreEqual(5, result.Count);
        }

        [Test]
        public void Sort_ByLastPlayed()
        {
            var result = _svc.Sort(_games, "lastplayed", true).ToList();
            Assert.IsNotNull(result);
        }

        // --- GroupBy ---

        [Test]
        public void GroupBy_CompletionStatus()
        {
            var result = _svc.GroupBy(_games, "completionStatus") as IList;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= 1);
        }

        [Test]
        public void GroupBy_Year()
        {
            var result = _svc.GroupBy(_games, "year") as IList;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= 4);
        }

        [Test]
        public void GroupBy_Invalid_ReturnsNull()
        {
            var result = _svc.GroupBy(_games, "invalid");
            Assert.IsNull(result);
        }

        [Test]
        public void GroupBy_Source_WithNullSources()
        {
            // All test games have null Source (read-only), should group as "Unknown"
            var result = _svc.GroupBy(_games, "source") as IList;
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= 1);
        }

        // --- Empty input ---

        [Test]
        public void Filter_EmptyList_ReturnsEmpty()
        {
            var result = _svc.Filter(new List<Game>(), new Dictionary<string, object>()).ToList();
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Sort_EmptyList_ReturnsEmpty()
        {
            var result = _svc.Sort(new List<Game>(), "name", false).ToList();
            Assert.AreEqual(0, result.Count);
        }

        // --- Helper ---

        private Game MakeGame(string name, ulong playtime = 0, bool installed = false,
            bool hidden = false, bool favorite = false, int year = 2020)
        {
            return new Game(name)
            {
                Playtime = playtime,
                IsInstalled = installed,
                Hidden = hidden,
                Favorite = favorite,
                ReleaseDate = new ReleaseDate(year, 1, 1)
            };
        }
    }
}
