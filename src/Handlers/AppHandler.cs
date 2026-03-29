namespace PlayniteBridge.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Server;

    internal class AppHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly int _httpPort;

        public AppHandler(IPlayniteAPI api, int httpPort)
        {
            _api = api;
            _httpPort = httpPort;
        }

        public object GetAppInfo()
        {
            return new
            {
                version = _api.ApplicationInfo.ApplicationVersion.ToString(),
                mode = _api.ApplicationInfo.Mode.ToString(),
                isPortable = _api.ApplicationInfo.IsPortable,
                inOfflineMode = _api.ApplicationInfo.InOfflineMode,
                apiPort = _httpPort,
                paths = new
                {
                    application = _api.Paths.ApplicationPath,
                    configuration = _api.Paths.ConfigurationPath,
                    extensionsData = _api.Paths.ExtensionsDataPath
                }
            };
        }

        public object GetAddons()
        {
            return new
            {
                installed = _api.Addons.Addons ?? new List<string>(),
                disabled = _api.Addons.DisabledAddons ?? new List<string>()
            };
        }

        public object GetStats()
        {
            var games = _api.Database.Games;
            if (games == null || games.Count == 0)
                return new { totalGames = 0 };

            var gameList = games.ToList();
            return new
            {
                totalGames = gameList.Count,
                installed = gameList.Count(g => g.IsInstalled),
                notInstalled = gameList.Count(g => !g.IsInstalled),
                hidden = gameList.Count(g => g.Hidden),
                favorite = gameList.Count(g => g.Favorite),
                categorized = gameList.Count(g => g.CategoryIds != null && g.CategoryIds.Count > 0),
                uncategorized = gameList.Count(g => g.CategoryIds == null || g.CategoryIds.Count == 0),
                totalPlaytimeHours = Math.Round(gameList.Sum(g => (double)g.Playtime) / 3600, 1),
                bySource = gameList
                    .GroupBy(g => g.Source?.Name ?? "Unknown")
                    .OrderByDescending(g => g.Count())
                    .Select(g => new { source = g.Key, count = g.Count() }).ToList(),
                byCompletionStatus = gameList
                    .GroupBy(g => g.CompletionStatus?.Name ?? "Not Set")
                    .OrderByDescending(g => g.Count())
                    .Select(g => new { status = g.Key, count = g.Count() }).ToList(),
                topGenres = gameList
                    .SelectMany(g => g.Genres?.Select(x => x.Name) ?? Enumerable.Empty<string>())
                    .GroupBy(x => x)
                    .OrderByDescending(g => g.Count())
                    .Take(15)
                    .Select(g => new { genre = g.Key, count = g.Count() }).ToList(),
                recentlyPlayed = gameList
                    .Where(g => g.LastActivity.HasValue)
                    .OrderByDescending(g => g.LastActivity)
                    .Take(10)
                    .Select(g => new { id = g.Id.ToString(), name = g.Name, lastPlayed = g.LastActivity?.ToString("o") }).ToList()
            };
        }

        public object SendNotification(RequestContext ctx)
        {
            var data = ctx.Body;
            var text = data["text"].ToString();
            var type = data.ContainsKey("type") && data["type"]?.ToString() == "error"
                ? NotificationType.Error
                : NotificationType.Info;
            var id = Guid.NewGuid().ToString();
            _api.Notifications.Add(id, text, type);
            return new { ok = true, id };
        }

        public object GetPlugins()
        {
            var plugins = _api.Addons.Plugins.Select(p => new
            {
                id = p.Id.ToString(),
                type = p.GetType().Name,
                assembly = p.GetType().Assembly.GetName().Name
            }).ToList();

            var addons = _api.Addons.Addons ?? new List<string>();
            var disabled = _api.Addons.DisabledAddons ?? new List<string>();

            return new
            {
                loaded = plugins,
                installed = addons,
                disabled
            };
        }

        public object GetApiIndex()
        {
            return new
            {
                name = "Playnite Bridge API",
                version = "2.0.0",
                endpoints = new[]
                {
                    "GET    /api                          API index",
                    "GET    /api/games                    List/search games",
                    "GET    /api/games/{id}               Full game details",
                    "PUT    /api/games/{id}               Update game fields",
                    "DELETE /api/games/{id}               Delete game",
                    "POST   /api/games/{id}/launch        Launch game",
                    "POST   /api/games/{id}/install       Install game",
                    "POST   /api/games/{id}/uninstall     Uninstall game",
                    "PUT    /api/games/{id}/categories    Set categories",
                    "POST   /api/games/{id}/categories    Add categories",
                    "PUT    /api/games/{id}/tags          Set tags",
                    "POST   /api/games/{id}/tags          Add tags",
                    "PUT    /api/games/{id}/features      Set features",
                    "POST   /api/games/{id}/features      Add features",
                    "PUT    /api/games/{id}/genres        Set genres",
                    "POST   /api/games/{id}/genres        Add genres",
                    "PUT    /api/games/{id}/status        Set completion status",
                    "POST   /api/games/{id}/fetch-art     Fetch missing artwork",
                    "GET    /api/games/missing-art        Games missing artwork",
                    "POST   /api/games/query              Advanced query with filters & groupBy",
                    "GET    /api/games/{id}/achievements  Achievements (SuccessStory)",
                    "GET    /api/games/{id}/activity      Play sessions (GameActivity)",
                    "GET    /api/categories               All categories",
                    "POST   /api/categories               Create category",
                    "GET    /api/genres                   All genres",
                    "POST   /api/genres                   Create genre",
                    "GET    /api/tags                     All tags",
                    "POST   /api/tags                     Create tag",
                    "GET    /api/features                 All features",
                    "POST   /api/features                 Create feature",
                    "GET    /api/platforms                All platforms",
                    "GET    /api/sources                  All library sources",
                    "GET    /api/companies                All developers/publishers",
                    "GET    /api/series                   All series",
                    "POST   /api/series                   Create series",
                    "GET    /api/age-ratings              All age ratings",
                    "GET    /api/regions                  All regions",
                    "GET    /api/completion-statuses      All completion statuses",
                    "POST   /api/completion-statuses      Create completion status",
                    "GET    /api/filter-presets           Saved filter presets",
                    "GET    /api/emulators                All emulators",
                    "POST   /api/auto-categorize          Auto-categorize by genre",
                    "POST   /api/fetch-all-art            Fetch artwork for all",
                    "GET    /api/view/state               Current UI state",
                    "GET    /api/view/selected            Selected games",
                    "POST   /api/view/select              Select games in UI",
                    "POST   /api/view/filter              Apply filter preset",
                    "GET    /api/app/info                 App version & paths",
                    "GET    /api/app/addons               Installed addons",
                    "GET    /api/stats                    Library statistics",
                    "POST   /api/notifications            Show notification",
                    "GET    /api/plugins                  Loaded/installed plugins",
                    "POST   /api/auth/rotate              Rotate API token",
                    "GET    /api/skill.md                 Get AI skill file",
                    "POST   /api/eval                     Execute C# code"
                }
            };
        }
    }
}
