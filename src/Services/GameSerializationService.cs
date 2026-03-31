namespace PlayniteBridge.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using Playnite.SDK;
    using Playnite.SDK.Models;

    internal class GameSerializationService
    {
        public object SerializeCompact(Game g)
        {
            return new
            {
                id = g.Id.ToString(),
                name = g.Name,
                source = g.Source?.Name,
                genres = g.Genres?.Select(x => x.Name).ToList() ?? new List<string>(),
                categories = g.Categories?.Select(x => x.Name).ToList() ?? new List<string>(),
                tags = g.Tags?.Select(x => x.Name).ToList() ?? new List<string>(),
                features = g.Features?.Select(x => x.Name).ToList() ?? new List<string>(),
                platforms = g.Platforms?.Select(x => x.Name).ToList() ?? new List<string>(),
                completionStatus = g.CompletionStatus?.Name,
                isInstalled = g.IsInstalled,
                favorite = g.Favorite,
                hidden = g.Hidden,
                playtime = (long)g.Playtime,
                playCount = (long)g.PlayCount,
                lastActivity = g.LastActivity?.ToString("o"),
                userScore = g.UserScore
            };
        }

        public object SerializeFull(Game g)
        {
            return new
            {
                id = g.Id.ToString(),
                name = g.Name,
                sortingName = g.SortingName,
                description = g.Description,
                notes = g.Notes,
                genres = g.Genres?.Select(x => x.Name).ToList() ?? new List<string>(),
                categories = g.Categories?.Select(x => x.Name).ToList() ?? new List<string>(),
                tags = g.Tags?.Select(x => x.Name).ToList() ?? new List<string>(),
                features = g.Features?.Select(x => x.Name).ToList() ?? new List<string>(),
                platforms = g.Platforms?.Select(x => x.Name).ToList() ?? new List<string>(),
                developers = g.Developers?.Select(x => x.Name).ToList() ?? new List<string>(),
                publishers = g.Publishers?.Select(x => x.Name).ToList() ?? new List<string>(),
                series = g.Series?.Select(x => x.Name).ToList() ?? new List<string>(),
                ageRatings = g.AgeRatings?.Select(x => x.Name).ToList() ?? new List<string>(),
                regions = g.Regions?.Select(x => x.Name).ToList() ?? new List<string>(),
                source = g.Source?.Name,
                gameId = g.GameId,
                pluginId = g.PluginId.ToString(),
                completionStatus = g.CompletionStatus?.Name,
                releaseDate = g.ReleaseDate?.Date.ToString("yyyy-MM-dd"),
                isInstalled = g.IsInstalled,
                installDirectory = g.InstallDirectory,
                installSize = g.InstallSize,
                hidden = g.Hidden,
                favorite = g.Favorite,
                playtime = (long)g.Playtime,
                playCount = (long)g.PlayCount,
                lastActivity = g.LastActivity?.ToString("o"),
                added = g.Added?.ToString("o"),
                modified = g.Modified?.ToString("o"),
                communityScore = g.CommunityScore,
                criticScore = g.CriticScore,
                userScore = g.UserScore,
                links = g.Links?.Select(l => new { name = l.Name, url = l.Url }).ToList(),
                hasIcon = !string.IsNullOrEmpty(g.Icon),
                hasCover = !string.IsNullOrEmpty(g.CoverImage),
                hasBackground = !string.IsNullOrEmpty(g.BackgroundImage),
                version = g.Version
            };
        }

        /// <summary>Serialize for sync — includes image hashes.</summary>
        public object SerializeForSync(Game g, IGameDatabaseAPI db)
        {
            return new
            {
                id = g.Id.ToString(),
                name = g.Name,
                sortingName = g.SortingName,
                description = g.Description,
                notes = g.Notes,
                genres = g.Genres?.Select(x => x.Name).ToList() ?? new List<string>(),
                categories = g.Categories?.Select(x => x.Name).ToList() ?? new List<string>(),
                tags = g.Tags?.Select(x => x.Name).ToList() ?? new List<string>(),
                features = g.Features?.Select(x => x.Name).ToList() ?? new List<string>(),
                platforms = g.Platforms?.Select(x => x.Name).ToList() ?? new List<string>(),
                developers = g.Developers?.Select(x => x.Name).ToList() ?? new List<string>(),
                publishers = g.Publishers?.Select(x => x.Name).ToList() ?? new List<string>(),
                series = g.Series?.Select(x => x.Name).ToList() ?? new List<string>(),
                source = g.Source?.Name,
                gameId = g.GameId,
                pluginId = g.PluginId.ToString(),
                completionStatus = g.CompletionStatus?.Name,
                releaseDate = g.ReleaseDate?.Date.ToString("yyyy-MM-dd"),
                isInstalled = g.IsInstalled,
                hidden = g.Hidden,
                favorite = g.Favorite,
                playtime = (long)g.Playtime,
                playCount = (long)g.PlayCount,
                lastActivity = g.LastActivity?.ToString("o"),
                modified = g.Modified?.ToString("o"),
                communityScore = g.CommunityScore,
                criticScore = g.CriticScore,
                userScore = g.UserScore,
                links = g.Links?.Select(l => new { name = l.Name, url = l.Url }).ToList(),
                version = g.Version,
                // Image hashes
                coverHash = ComputeFileHash(db, g.CoverImage),
                iconHash = ComputeFileHash(db, g.Icon),
                backgroundHash = ComputeFileHash(db, g.BackgroundImage),
            };
        }

        public static string ComputeFileHash(IGameDatabaseAPI db, string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath) || dbPath.StartsWith("http")) return null;
            try
            {
                var fullPath = db.GetFullFilePath(dbPath);
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;
                using (var sha = SHA256.Create())
                using (var stream = File.OpenRead(fullPath))
                {
                    var hash = sha.ComputeHash(stream);
                    return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch { return null; }
        }

        public static byte[] ReadImageFile(IGameDatabaseAPI db, string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath) || dbPath.StartsWith("http")) return null;
            try
            {
                var fullPath = db.GetFullFilePath(dbPath);
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;
                return File.ReadAllBytes(fullPath);
            }
            catch { return null; }
        }
    }
}
