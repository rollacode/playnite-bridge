namespace PlayniteBridge.Handlers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Web.Script.Serialization;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;

    internal class AutomationHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private readonly IPlayniteAPI _api;
        private readonly CollectionResolver _resolver;
        private readonly Func<string> _getPluginDataPath;

        // IGDB state
        private string _igdbClientId = "";
        private string _igdbToken = "";
        private DateTime _igdbTokenExpiry = DateTime.MinValue;

        public AutomationHandler(IPlayniteAPI api, CollectionResolver resolver, Func<string> getPluginDataPath)
        {
            _api = api;
            _resolver = resolver;
            _getPluginDataPath = getPluginDataPath;
        }

        public object AutoCategorize()
        {
            var uncategorized = _api.Database.Games
                .Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0).ToList();
            var count = CategorizeGames(uncategorized);
            return new { ok = true, categorized = count, total = uncategorized.Count };
        }

        public object Categorize(RequestContext ctx)
        {
            var data = ctx.Body;
            var gameId = Guid.Parse(data["gameId"].ToString());
            var categoryNames = ((ArrayList)data["categories"]).Cast<string>().ToList();

            var game = _api.Database.Games.Get(gameId);
            if (game == null) return new { error = "Game not found", code = 404 };

            if (game.CategoryIds == null) game.CategoryIds = new List<Guid>();
            foreach (var catName in categoryNames)
            {
                var cat = _resolver.GetOrCreate(_api.Database.Categories, catName);
                if (cat != null && !game.CategoryIds.Contains(cat.Id))
                    game.CategoryIds.Add(cat.Id);
            }

            _api.Database.Games.Update(game);
            return new { ok = true, game = game.Name, categories = categoryNames };
        }

        public object FetchArt(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return new { error = "Game not found", code = 404 };
            return FetchArtForGame(game);
        }

        public object FetchAllArt()
        {
            var results = new List<object>();
            int fetched = 0, failed = 0, skipped = 0;

            foreach (var game in _api.Database.Games.OrderBy(x => x.Name))
            {
                if (!string.IsNullOrEmpty(game.CoverImage) && !string.IsNullOrEmpty(game.BackgroundImage) && !string.IsNullOrEmpty(game.Icon))
                { skipped++; continue; }

                try { results.Add(FetchArtForGame(game)); fetched++; }
                catch (Exception ex) { results.Add(new { game = game.Name, error = ex.Message }); failed++; }
            }
            return new { fetched, failed, skipped, details = results };
        }

        // Called from plugin for menu items
        public int CategorizeGames(List<Game> games)
        {
            int count = 0;
            foreach (var game in games)
            {
                if (game.Genres == null || game.Genres.Count == 0) continue;
                var primaryGenre = game.Genres.First().Name;
                var category = _resolver.GetOrCreate(_api.Database.Categories, primaryGenre);
                if (category == null) continue;

                if (game.CategoryIds == null) game.CategoryIds = new List<Guid>();
                if (!game.CategoryIds.Contains(category.Id))
                {
                    game.CategoryIds.Add(category.Id);
                    _api.Database.Games.Update(game);
                    count++;
                }
            }
            return count;
        }

        private object FetchArtForGame(Game game)
        {
            var updated = new List<string>();
            var errors = new List<string>();
            bool missingCover = string.IsNullOrEmpty(game.CoverImage);
            bool missingBg = string.IsNullOrEmpty(game.BackgroundImage);
            bool missingIcon = string.IsNullOrEmpty(game.Icon);

            if (!missingCover && !missingBg && !missingIcon)
                return new { game = game.Name, status = "all art present" };

            // Steam CDN
            var steamPluginId = "cb91dfc9-b977-43bf-8e70-55f46e410fab";
            if (game.PluginId.ToString() == steamPluginId && !string.IsNullOrEmpty(game.GameId))
            {
                var sid = game.GameId;
                if (missingCover && (TrySetImage(game, $"https://cdn.akamai.steamstatic.com/steam/apps/{sid}/library_600x900_2x.jpg", "cover")
                    || TrySetImage(game, $"https://cdn.akamai.steamstatic.com/steam/apps/{sid}/header.jpg", "cover")))
                { updated.Add("cover"); missingCover = false; }

                if (missingBg && (TrySetImage(game, $"https://cdn.akamai.steamstatic.com/steam/apps/{sid}/library_hero.jpg", "background")
                    || TrySetImage(game, $"https://cdn.akamai.steamstatic.com/steam/apps/{sid}/page_bg_generated_v6b.jpg", "background")))
                { updated.Add("background"); missingBg = false; }

                if (missingIcon && TrySetImage(game, $"https://cdn.akamai.steamstatic.com/steam/apps/{sid}/capsule_231x87.jpg", "icon"))
                { updated.Add("icon"); missingIcon = false; }
            }

            // IGDB fallback
            if (missingCover || missingBg || missingIcon)
            {
                EnsureIgdbToken();
                if (!string.IsNullOrEmpty(_igdbToken))
                {
                    try
                    {
                        var searchName = game.Name.Replace("\"", "\\\"");
                        var results = IgdbQuery("games",
                            $"search \"{searchName}\"; fields name,cover.image_id,screenshots.image_id,artworks.image_id; limit 1;");

                        if (results.Count > 0)
                        {
                            var igdb = results[0];

                            if (missingCover && igdb.ContainsKey("cover"))
                            {
                                var cover = igdb["cover"] as Dictionary<string, object>;
                                var imgId = cover?.GetValueOrNull("image_id")?.ToString();
                                if (imgId != null && TrySetImage(game, $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imgId}.jpg", "cover"))
                                { updated.Add("cover (IGDB)"); missingCover = false; }
                            }

                            if (missingBg)
                            {
                                string bgId = ExtractFirstImageId(igdb, "artworks") ?? ExtractFirstImageId(igdb, "screenshots");
                                if (bgId != null && TrySetImage(game, $"https://images.igdb.com/igdb/image/upload/t_1080p/{bgId}.jpg", "background"))
                                { updated.Add("background (IGDB)"); missingBg = false; }
                            }

                            if (missingIcon && igdb.ContainsKey("cover"))
                            {
                                var cover = igdb["cover"] as Dictionary<string, object>;
                                var imgId = cover?.GetValueOrNull("image_id")?.ToString();
                                if (imgId != null && TrySetImage(game, $"https://images.igdb.com/igdb/image/upload/t_thumb/{imgId}.jpg", "icon"))
                                { updated.Add("icon (IGDB)"); missingIcon = false; }
                            }
                        }
                        else errors.Add("IGDB: no match");
                    }
                    catch (Exception ex) { errors.Add($"IGDB: {ex.Message}"); }
                }
                else errors.Add("IGDB not configured");
            }

            if (updated.Count > 0) _api.Database.Games.Update(game);

            var still = new List<string>();
            if (missingIcon) still.Add("icon");
            if (missingCover) still.Add("cover");
            if (missingBg) still.Add("background");

            return new { game = game.Name, source = game.Source?.Name ?? "unknown", updated, stillMissing = still, errors };
        }

        private bool TrySetImage(Game game, string url, string type)
        {
            try
            {
                var response = _httpClient.GetAsync(url).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) return false;
                var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (bytes.Length < 1000) return false;

                var ext = url.EndsWith(".png") ? ".png" : ".jpg";
                var tempPath = Path.Combine(Path.GetTempPath(), $"pb_{game.Id}_{type}{ext}");
                File.WriteAllBytes(tempPath, bytes);
                var dbPath = _api.Database.AddFile(tempPath, game.Id);
                try { File.Delete(tempPath); } catch { }
                if (string.IsNullOrEmpty(dbPath)) return false;

                switch (type)
                {
                    case "cover": game.CoverImage = dbPath; break;
                    case "background": game.BackgroundImage = dbPath; break;
                    case "icon": game.Icon = dbPath; break;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed {type} for {game.Name}: {ex.Message}");
                return false;
            }
        }

        private void EnsureIgdbToken()
        {
            if (!string.IsNullOrEmpty(_igdbToken) && DateTime.UtcNow < _igdbTokenExpiry) return;
            if (!string.IsNullOrEmpty(_igdbClientId)) return;

            var configPath = Path.Combine(_getPluginDataPath(), "igdb.json");
            if (!File.Exists(configPath)) return;

            try
            {
                var cfg = _json.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
                _igdbClientId = cfg.GetValueOrDefault("client_id", "");
                var secret = cfg.GetValueOrDefault("client_secret", "");

                if (string.IsNullOrEmpty(_igdbClientId) || string.IsNullOrEmpty(secret)) return;

                var tokenUrl = $"https://id.twitch.tv/oauth2/token?client_id={_igdbClientId}&client_secret={secret}&grant_type=client_credentials";
                var resp = _httpClient.PostAsync(tokenUrl, null).GetAwaiter().GetResult();
                var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var tokenData = _json.Deserialize<Dictionary<string, object>>(json);
                _igdbToken = tokenData["access_token"].ToString();
                _igdbTokenExpiry = DateTime.UtcNow.AddSeconds(Convert.ToInt32(tokenData["expires_in"]) - 60);
            }
            catch (Exception ex)
            {
                Logger.Warn($"IGDB token error: {ex.Message}");
            }
        }

        private List<Dictionary<string, object>> IgdbQuery(string endpoint, string query)
        {
            EnsureIgdbToken();
            if (string.IsNullOrEmpty(_igdbToken)) return new List<Dictionary<string, object>>();

            var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.igdb.com/v4/{endpoint}");
            req.Headers.Add("Client-ID", _igdbClientId);
            req.Headers.Add("Authorization", $"Bearer {_igdbToken}");
            req.Content = new StringContent(query, Encoding.UTF8, "text/plain");

            var resp = _httpClient.SendAsync(req).GetAwaiter().GetResult();
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return _json.Deserialize<List<Dictionary<string, object>>>(json) ?? new List<Dictionary<string, object>>();
        }

        private string ExtractFirstImageId(Dictionary<string, object> igdb, string field)
        {
            if (!igdb.ContainsKey(field)) return null;
            var list = igdb[field] as ArrayList;
            if (list == null || list.Count == 0) return null;
            var first = list[0] as Dictionary<string, object>;
            return first?.GetValueOrNull("image_id")?.ToString();
        }
    }
}
