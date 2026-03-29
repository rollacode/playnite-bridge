using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PlayniteBridge
{
    public class PlayniteBridgePlugin : GenericPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private const int HttpPort = 19821;
        private HttpListener _httpListener;
        private Thread _serverThread;
        private string _apiToken;
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private static readonly HttpClient _httpClient = new HttpClient();

        // IGDB state
        private string _igdbClientId = "";
        private string _igdbToken = "";
        private DateTime _igdbTokenExpiry = DateTime.MinValue;

        public override Guid Id => Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");

        public PlayniteBridgePlugin(IPlayniteAPI api) : base(api) { }

        #region Plugin Lifecycle

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            LoadOrCreateToken();
            StartHttpServer();
            Logger.Info($"Playnite Bridge API started on port {HttpPort}");

            if (!_isNetworkBound)
            {
                var result = PlayniteApi.Dialogs.ShowMessage(
                    "Playnite Bridge is running on localhost only.\n\n" +
                    "Enable network access so other devices can connect to the API?\n" +
                    "This will register port 19821 and add a firewall rule.\n\n" +
                    "Windows will ask for administrator permission.",
                    "Playnite Bridge — Network Access",
                    System.Windows.MessageBoxButton.YesNo);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    EnableNetworkAccess();
                }
            }
        }

        public override void Dispose()
        {
            StopHttpServer();
            base.Dispose();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Copy AI Skill to Clipboard",
                MenuSection = "@Playnite Bridge",
                Action = _ =>
                {
                    var skill = GenerateSkillMd();
                    System.Windows.Clipboard.SetText(skill);
                    PlayniteApi.Dialogs.ShowMessage(
                        "AI Skill copied to clipboard!\n\n" +
                        "Paste it into your AI chat (ChatGPT, Claude, etc.) to give the AI access to your Playnite library.\n\n" +
                        "The skill contains your API token — don't share it publicly.",
                        "Playnite Bridge");
                }
            };

            yield return new MainMenuItem
            {
                Description = "Open AI Skill File",
                MenuSection = "@Playnite Bridge",
                Action = _ =>
                {
                    var skillPath = Path.Combine(GetPluginUserDataPath(), "skill.md");
                    File.WriteAllText(skillPath, GenerateSkillMd(), Encoding.UTF8);
                    System.Diagnostics.Process.Start(skillPath);
                }
            };

            yield return new MainMenuItem
            {
                Description = "Show API Token",
                MenuSection = "@Playnite Bridge",
                Action = _ => PlayniteApi.Dialogs.ShowMessage(
                    $"API Token:\n\n{_apiToken}\n\nUse 'Copy AI Skill' for the full skill file.",
                    "Playnite Bridge")
            };

            yield return new MainMenuItem
            {
                Description = "Regenerate API Token",
                MenuSection = "@Playnite Bridge",
                Action = _ =>
                {
                    var result = PlayniteApi.Dialogs.ShowMessage(
                        "Regenerate API token?\n\nThe old token stops working immediately. You'll need to update your AI skill.",
                        "Playnite Bridge",
                        System.Windows.MessageBoxButton.YesNo);
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        RotateToken();
                        PlayniteApi.Dialogs.ShowMessage($"New token:\n\n{_apiToken}", "Playnite Bridge");
                    }
                }
            };

            yield return new MainMenuItem
            {
                Description = _isNetworkBound ? "Network Access ✓ (already enabled)" : "Enable Network Access",
                MenuSection = "@Playnite Bridge",
                Action = _ =>
                {
                    if (_isNetworkBound)
                    {
                        PlayniteApi.Dialogs.ShowMessage(
                            "Network access is already enabled.\n\n" +
                            "API is accessible from other devices at:\nhttp://<this-pc-ip>:" + HttpPort,
                            "Playnite Bridge");
                        return;
                    }

                    var result = PlayniteApi.Dialogs.ShowMessage(
                        "Allow API access from other devices on your network?\n\n" +
                        "This will:\n" +
                        "• Register HTTP port 19821 for network access\n" +
                        "• Add a Windows Firewall rule\n\n" +
                        "Windows will ask for administrator permission.",
                        "Playnite Bridge",
                        System.Windows.MessageBoxButton.YesNo);
                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        EnableNetworkAccess();
                    }
                }
            };

            yield return new MainMenuItem
            {
                Description = "Auto-Categorize All Games (by Genre)",
                MenuSection = "@Playnite Bridge",
                Action = _ => AutoCategorizeByGenre()
            };

            yield return new MainMenuItem
            {
                Description = "Auto-Categorize Selected Games",
                MenuSection = "@Playnite Bridge",
                Action = _ => AutoCategorizeSelected()
            };

            yield return new MainMenuItem
            {
                Description = "Show Uncategorized Games",
                MenuSection = "@Playnite Bridge",
                Action = _ => ShowUncategorized()
            };
        }

        #endregion

        #region Authentication

        private void LoadOrCreateToken()
        {
            var dir = GetPluginUserDataPath();
            Directory.CreateDirectory(dir);
            var authPath = Path.Combine(dir, "auth.json");

            if (File.Exists(authPath))
            {
                try
                {
                    var data = _json.Deserialize<Dictionary<string, string>>(File.ReadAllText(authPath));
                    if (data != null && data.ContainsKey("token") && !string.IsNullOrEmpty(data["token"]))
                    {
                        _apiToken = data["token"];
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to load auth token, generating new one");
                }
            }
            RotateToken();
        }

        private void RotateToken()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[24];
                rng.GetBytes(bytes);
                _apiToken = "pb_" + BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }

            var dir = GetPluginUserDataPath();
            Directory.CreateDirectory(dir);
            File.WriteAllText(
                Path.Combine(dir, "auth.json"),
                _json.Serialize(new { token = _apiToken, created = DateTime.UtcNow.ToString("o") }));
        }

        private bool ValidateAuth(HttpListenerRequest req, HttpListenerResponse res)
        {
            var auth = req.Headers["Authorization"];
            if (!string.IsNullOrEmpty(auth) && auth.StartsWith("Bearer ") && auth.Substring(7).Trim() == _apiToken)
                return true;

            res.StatusCode = 401;
            WriteJson(res, new { error = "Unauthorized. Header required: Authorization: Bearer <token>" });
            return false;
        }

        #endregion

        #region HTTP Server

        private bool _isNetworkBound;

        private void StartHttpServer()
        {
            // Try binding to all interfaces first, fall back to localhost if no admin rights
            string[] prefixes = new[]
            {
                $"http://+:{HttpPort}/",
                $"http://*:{HttpPort}/",
                $"http://localhost:{HttpPort}/"
            };

            _isNetworkBound = false;

            foreach (var prefix in prefixes)
            {
                try
                {
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add(prefix);
                    _httpListener.Start();
                    _serverThread = new Thread(HttpServerLoop) { IsBackground = true };
                    _serverThread.Start();
                    _isNetworkBound = prefix.Contains("+") || prefix.Contains("*");
                    Logger.Info($"HTTP server listening on {prefix}");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Failed to bind {prefix}: {ex.Message}");
                    try { _httpListener?.Close(); } catch { }
                }
            }

            Logger.Error("Failed to start HTTP server on any prefix");
        }

        private void EnableNetworkAccess()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netsh http add urlacl url=http://+:{HttpPort}/ user=Everyone && netsh advfirewall firewall add rule name=\"Playnite Bridge\" dir=in action=allow protocol=tcp localport={HttpPort}",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(10000);

                StopHttpServer();
                StartHttpServer();

                PlayniteApi.Dialogs.ShowMessage(
                    _isNetworkBound
                        ? "Network access enabled! API is now accessible from other devices.\n\n" +
                          "Other devices can reach it at:\nhttp://<this-pc-ip>:" + HttpPort
                        : "Failed to bind to network interface. Check the logs.",
                    "Playnite Bridge");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                PlayniteApi.Dialogs.ShowMessage("Cancelled — administrator permission is required.", "Playnite Bridge");
            }
            catch (Exception ex)
            {
                PlayniteApi.Dialogs.ShowMessage($"Error: {ex.Message}", "Playnite Bridge");
            }
        }

        private void StopHttpServer()
        {
            try { _httpListener?.Stop(); _httpListener?.Close(); } catch { }
        }

        private void HttpServerLoop()
        {
            while (_httpListener?.IsListening == true)
            {
                try
                {
                    var ctx = _httpListener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { Logger.Error(ex, "HTTP server error"); }
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.ContentType = "application/json; charset=utf-8";
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.OutputStream.Close();
                return;
            }

            try
            {
                if (!ValidateAuth(req, res)) return;

                var path = req.RawUrl.Split('?')[0].TrimEnd('/');
                var method = req.HttpMethod;
                var result = Route(path, method, req);

                if (result == null)
                {
                    res.StatusCode = 404;
                    result = new { error = "Not found", help = "GET /api for endpoint list" };
                }

                WriteJson(res, result);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"HTTP request error: {req.RawUrl}");
                res.StatusCode = 500;
                WriteJson(res, new { error = ex.Message });
            }
            finally
            {
                try { res.OutputStream.Close(); } catch { }
            }
        }

        private void WriteJson(HttpListenerResponse res, object obj)
        {
            var json = _json.Serialize(obj);
            var buffer = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
        }

        #endregion

        #region Routing

        private object Route(string path, string method, HttpListenerRequest req)
        {
            // API index
            if (path == "/api" && method == "GET") return HandleApiIndex();

            // Auth
            if (path == "/api/auth/rotate" && method == "POST") return HandleRotateToken();
            if (path == "/api/skill.md" && method == "GET") return new { content = GenerateSkillMd() };

            // Games - collection
            if (path == "/api/games" && method == "GET") return HandleGetGames(req);
            if (path == "/api/games/missing-art" && method == "GET") return HandleGetMissingArt();
            if (path == "/api/games" && method == "POST") return HandleCreateGame(req);

            // Games - single resource
            if (path.StartsWith("/api/games/"))
            {
                var rest = path.Substring("/api/games/".Length);
                var segments = rest.Split('/');

                if (segments.Length >= 1 && Guid.TryParse(segments[0], out var gameId))
                {
                    if (segments.Length == 1)
                    {
                        if (method == "GET") return HandleGetGame(gameId);
                        if (method == "PUT") return HandleUpdateGame(gameId, req);
                        if (method == "DELETE") return HandleDeleteGame(gameId);
                    }
                    else if (segments.Length == 2)
                    {
                        var action = segments[1];
                        if (action == "launch" && method == "POST") return HandleLaunchGame(gameId);
                        if (action == "install" && method == "POST") return HandleInstallGame(gameId);
                        if (action == "uninstall" && method == "POST") return HandleUninstallGame(gameId);
                        if (action == "categories" && method == "PUT") return HandleSetGameList(gameId, req, "categories");
                        if (action == "categories" && method == "POST") return HandleAddGameList(gameId, req, "categories");
                        if (action == "tags" && method == "PUT") return HandleSetGameList(gameId, req, "tags");
                        if (action == "tags" && method == "POST") return HandleAddGameList(gameId, req, "tags");
                        if (action == "features" && method == "PUT") return HandleSetGameList(gameId, req, "features");
                        if (action == "features" && method == "POST") return HandleAddGameList(gameId, req, "features");
                        if (action == "genres" && method == "PUT") return HandleSetGameList(gameId, req, "genres");
                        if (action == "genres" && method == "POST") return HandleAddGameList(gameId, req, "genres");
                        if (action == "status" && method == "PUT") return HandleSetCompletionStatus(gameId, req);
                        if (action == "fetch-art" && method == "POST") return HandleFetchArt(gameId);
                        if (action == "delete" && method == "POST") return HandleDeleteGame(gameId); // compat
                    }
                }
            }

            // Database collections — with DELETE support for path like /api/categories/{id}
            if (path.StartsWith("/api/categories/") && method == "DELETE")
                return HandleDeleteCollectionItem(PlayniteApi.Database.Categories, path.Substring("/api/categories/".Length));
            if (path.StartsWith("/api/tags/") && method == "DELETE")
                return HandleDeleteCollectionItem(PlayniteApi.Database.Tags, path.Substring("/api/tags/".Length));
            if (path.StartsWith("/api/genres/") && method == "DELETE")
                return HandleDeleteCollectionItem(PlayniteApi.Database.Genres, path.Substring("/api/genres/".Length));
            if (path.StartsWith("/api/features/") && method == "DELETE")
                return HandleDeleteCollectionItem(PlayniteApi.Database.Features, path.Substring("/api/features/".Length));
            if (path.StartsWith("/api/series/") && method == "DELETE")
                return HandleDeleteCollectionItem(PlayniteApi.Database.Series, path.Substring("/api/series/".Length));

            if (path == "/api/categories" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Categories);
            if (path == "/api/categories" && method == "POST") return HandleCreateItem(PlayniteApi.Database.Categories, req);
            if (path == "/api/genres" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Genres);
            if (path == "/api/genres" && method == "POST") return HandleCreateItem(PlayniteApi.Database.Genres, req);
            if (path == "/api/tags" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Tags);
            if (path == "/api/tags" && method == "POST") return HandleCreateItem(PlayniteApi.Database.Tags, req);
            if (path == "/api/features" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Features);
            if (path == "/api/features" && method == "POST") return HandleCreateItem(PlayniteApi.Database.Features, req);
            if (path == "/api/platforms" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Platforms);
            if (path == "/api/sources" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Sources);
            if (path == "/api/companies" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Companies);
            if (path == "/api/series" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Series);
            if (path == "/api/series" && method == "POST") return HandleCreateItem(PlayniteApi.Database.Series, req);
            if (path == "/api/age-ratings" && method == "GET") return HandleGetCollection(PlayniteApi.Database.AgeRatings);
            if (path == "/api/regions" && method == "GET") return HandleGetCollection(PlayniteApi.Database.Regions);
            if (path == "/api/completion-statuses" && method == "GET") return HandleGetCollection(PlayniteApi.Database.CompletionStatuses);
            if (path == "/api/completion-statuses" && method == "POST") return HandleCreateItem(PlayniteApi.Database.CompletionStatuses, req);
            if (path == "/api/filter-presets" && method == "GET") return HandleGetCollection(PlayniteApi.Database.FilterPresets);
            if (path == "/api/emulators" && method == "GET") return HandleGetEmulators();

            // Automation
            if (path == "/api/auto-categorize" && method == "POST") return HandleAutoCategorize();
            if (path == "/api/fetch-all-art" && method == "POST") return HandleFetchAllArt();
            if (path == "/api/categorize" && method == "POST") return HandleCategorize(req); // compat

            // View
            if (path == "/api/view/state" && method == "GET") return HandleGetViewState();
            if (path == "/api/view/selected" && method == "GET") return HandleGetSelectedGames();
            if (path == "/api/view/select" && method == "POST") return HandleSelectGames(req);
            if (path == "/api/view/filter" && method == "POST") return HandleApplyFilter(req);

            // App
            if (path == "/api/app/info" && method == "GET") return HandleGetAppInfo();
            if (path == "/api/app/addons" && method == "GET") return HandleGetAddons();
            if (path == "/api/stats" && method == "GET") return HandleGetStats();
            if (path == "/api/notifications" && method == "POST") return HandleSendNotification(req);

            return null;
        }

        #endregion

        #region Handlers - API Index

        private object HandleApiIndex()
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
                    "POST   /api/auth/rotate              Rotate API token",
                    "GET    /api/skill.md                 Get AI skill file"
                }
            };
        }

        #endregion

        #region Handlers - Games

        private object HandleGetGames(HttpListenerRequest req)
        {
            IEnumerable<Game> games = PlayniteApi.Database.Games;

            var q = req.QueryString["q"];
            if (!string.IsNullOrEmpty(q))
                games = games.Where(g => g.Name != null && g.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            if (req.QueryString["installed"] == "true")
                games = games.Where(g => g.IsInstalled);

            if (req.QueryString["favorite"] == "true")
                games = games.Where(g => g.Favorite);

            if (req.QueryString["hidden"] != "true")
                games = games.Where(g => !g.Hidden);

            var uncategorized = req.QueryString["uncategorized"];
            if (uncategorized == "true")
                games = games.Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0);

            var source = req.QueryString["source"];
            if (!string.IsNullOrEmpty(source))
                games = games.Where(g => g.Source != null && g.Source.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            var genre = req.QueryString["genre"];
            if (!string.IsNullOrEmpty(genre))
                games = games.Where(g => g.Genres != null && g.Genres.Any(x => x.Name.Equals(genre, StringComparison.OrdinalIgnoreCase)));

            var category = req.QueryString["category"];
            if (!string.IsNullOrEmpty(category))
                games = games.Where(g => g.Categories != null && g.Categories.Any(x => x.Name.Equals(category, StringComparison.OrdinalIgnoreCase)));

            var tag = req.QueryString["tag"];
            if (!string.IsNullOrEmpty(tag))
                games = games.Where(g => g.Tags != null && g.Tags.Any(x => x.Name.Equals(tag, StringComparison.OrdinalIgnoreCase)));

            var feature = req.QueryString["feature"];
            if (!string.IsNullOrEmpty(feature))
                games = games.Where(g => g.Features != null && g.Features.Any(x => x.Name.Equals(feature, StringComparison.OrdinalIgnoreCase)));

            var platform = req.QueryString["platform"];
            if (!string.IsNullOrEmpty(platform))
                games = games.Where(g => g.Platforms != null && g.Platforms.Any(x => x.Name.Equals(platform, StringComparison.OrdinalIgnoreCase)));

            var completionStatus = req.QueryString["completionStatus"];
            if (!string.IsNullOrEmpty(completionStatus))
                games = games.Where(g => g.CompletionStatus != null && g.CompletionStatus.Name.Equals(completionStatus, StringComparison.OrdinalIgnoreCase));

            var ordered = games.OrderBy(g => g.Name);

            int offset = 0, limit = 500;
            if (int.TryParse(req.QueryString["offset"], out var o)) offset = Math.Max(0, o);
            if (int.TryParse(req.QueryString["limit"], out var l)) limit = Math.Min(Math.Max(l, 1), 5000);

            var total = 0;
            try { total = ordered.Count(); } catch { }

            var page = ordered.Skip(offset).Take(limit);

            return new
            {
                total,
                offset,
                limit,
                games = page.Select(g => SerializeGameCompact(g)).ToList()
            };
        }

        private object HandleCreateGame(HttpListenerRequest req)
        {
            var data = ParseBody(req);
            var name = data["name"].ToString();

            var game = new Game(name);

            if (data.ContainsKey("source") && data["source"] != null)
            {
                var sourceName = data["source"].ToString();
                var source = PlayniteApi.Database.Sources
                    .FirstOrDefault(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
                if (source == null)
                {
                    source = new GameSource(sourceName);
                    PlayniteApi.Database.Sources.Add(source);
                }
                game.SourceId = source.Id;
            }

            if (data.ContainsKey("platforms"))
                game.PlatformIds = ResolveIds(PlayniteApi.Database.Platforms, data["platforms"], create: true);
            if (data.ContainsKey("genres"))
                game.GenreIds = ResolveIds(PlayniteApi.Database.Genres, data["genres"]);
            if (data.ContainsKey("categories"))
                game.CategoryIds = ResolveIds(PlayniteApi.Database.Categories, data["categories"]);
            if (data.ContainsKey("tags"))
                game.TagIds = ResolveIds(PlayniteApi.Database.Tags, data["tags"]);
            if (data.ContainsKey("features"))
                game.FeatureIds = ResolveIds(PlayniteApi.Database.Features, data["features"]);
            if (data.ContainsKey("notes"))
                game.Notes = data["notes"]?.ToString();
            if (data.ContainsKey("description"))
                game.Description = data["description"]?.ToString();
            if (data.ContainsKey("hidden"))
                game.Hidden = Convert.ToBoolean(data["hidden"]);
            if (data.ContainsKey("completionStatus"))
            {
                var statusName = data["completionStatus"]?.ToString();
                if (!string.IsNullOrEmpty(statusName))
                {
                    var status = PlayniteApi.Database.CompletionStatuses
                        .FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
                    if (status != null) game.CompletionStatusId = status.Id;
                }
            }

            PlayniteApi.Database.Games.Add(game);
            return new { ok = true, id = game.Id.ToString(), name = game.Name };
        }

        private object HandleGetGame(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            return SerializeGameFull(game);
        }

        private object HandleUpdateGame(Guid gameId, HttpListenerRequest req)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ParseBody(req);

            if (data.ContainsKey("name") && data["name"] != null) game.Name = data["name"].ToString();
            if (data.ContainsKey("sortingName")) game.SortingName = data["sortingName"]?.ToString();
            if (data.ContainsKey("description")) game.Description = data["description"]?.ToString();
            if (data.ContainsKey("notes")) game.Notes = data["notes"]?.ToString();
            if (data.ContainsKey("hidden")) game.Hidden = Convert.ToBoolean(data["hidden"]);
            if (data.ContainsKey("favorite")) game.Favorite = Convert.ToBoolean(data["favorite"]);
            if (data.ContainsKey("version")) game.Version = data["version"]?.ToString();

            if (data.ContainsKey("userScore"))
                game.UserScore = data["userScore"] != null ? (int?)Convert.ToInt32(data["userScore"]) : null;
            if (data.ContainsKey("communityScore"))
                game.CommunityScore = data["communityScore"] != null ? (int?)Convert.ToInt32(data["communityScore"]) : null;
            if (data.ContainsKey("criticScore"))
                game.CriticScore = data["criticScore"] != null ? (int?)Convert.ToInt32(data["criticScore"]) : null;

            if (data.ContainsKey("playtime"))
                game.Playtime = Convert.ToUInt64(data["playtime"]);
            if (data.ContainsKey("playCount"))
                game.PlayCount = Convert.ToUInt64(data["playCount"]);

            // Collection fields — auto-create missing items
            if (data.ContainsKey("categories"))
                game.CategoryIds = ResolveIds(PlayniteApi.Database.Categories, data["categories"]);
            if (data.ContainsKey("tags"))
                game.TagIds = ResolveIds(PlayniteApi.Database.Tags, data["tags"]);
            if (data.ContainsKey("genres"))
                game.GenreIds = ResolveIds(PlayniteApi.Database.Genres, data["genres"]);
            if (data.ContainsKey("features"))
                game.FeatureIds = ResolveIds(PlayniteApi.Database.Features, data["features"]);
            if (data.ContainsKey("developers"))
                game.DeveloperIds = ResolveIds(PlayniteApi.Database.Companies, data["developers"]);
            if (data.ContainsKey("publishers"))
                game.PublisherIds = ResolveIds(PlayniteApi.Database.Companies, data["publishers"]);
            if (data.ContainsKey("series"))
                game.SeriesIds = ResolveIds(PlayniteApi.Database.Series, data["series"]);
            if (data.ContainsKey("platforms"))
                game.PlatformIds = ResolveIds(PlayniteApi.Database.Platforms, data["platforms"], create: false);
            if (data.ContainsKey("ageRatings"))
                game.AgeRatingIds = ResolveIds(PlayniteApi.Database.AgeRatings, data["ageRatings"], create: false);
            if (data.ContainsKey("regions"))
                game.RegionIds = ResolveIds(PlayniteApi.Database.Regions, data["regions"], create: false);

            if (data.ContainsKey("completionStatus"))
            {
                var statusName = data["completionStatus"]?.ToString();
                if (!string.IsNullOrEmpty(statusName))
                {
                    var status = PlayniteApi.Database.CompletionStatuses
                        .FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
                    if (status != null) game.CompletionStatusId = status.Id;
                }
                else
                {
                    game.CompletionStatusId = Guid.Empty;
                }
            }

            if (data.ContainsKey("releaseDate"))
            {
                var rd = data["releaseDate"]?.ToString();
                if (!string.IsNullOrEmpty(rd))
                {
                    try
                    {
                        var parts = rd.Split('-');
                        var year = int.Parse(parts[0]);
                        var month = parts.Length > 1 ? (int?)int.Parse(parts[1]) : null;
                        var day = parts.Length > 2 ? (int?)int.Parse(parts[2]) : null;
                        game.ReleaseDate = new ReleaseDate(year, month ?? 1, day ?? 1);
                    }
                    catch { }
                }
                else
                {
                    game.ReleaseDate = null;
                }
            }

            if (data.ContainsKey("links"))
            {
                var linkList = data["links"] as ArrayList;
                if (linkList != null)
                {
                    game.Links = new ObservableCollection<Link>();
                    foreach (var item in linkList)
                    {
                        var link = item as Dictionary<string, object>;
                        if (link != null)
                            game.Links.Add(new Link(link.GetValueOrNull("name")?.ToString() ?? "", link.GetValueOrNull("url")?.ToString() ?? ""));
                    }
                }
                else
                {
                    game.Links = null;
                }
            }

            PlayniteApi.Database.Games.Update(game);
            return SerializeGameFull(game);
        }

        private object HandleDeleteGame(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            var name = game.Name;
            PlayniteApi.Database.Games.Remove(gameId);
            return new { ok = true, deleted = name };
        }

        private object HandleLaunchGame(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            if (!game.IsInstalled) return new { error = "Game is not installed" };
            InvokeOnUI(() => PlayniteApi.StartGame(gameId));
            return new { ok = true, game = game.Name, action = "launched" };
        }

        private object HandleInstallGame(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            if (game.IsInstalled) return new { ok = true, game = game.Name, action = "already_installed" };
            InvokeOnUI(() => PlayniteApi.InstallGame(gameId));
            return new { ok = true, game = game.Name, action = "install_started" };
        }

        private object HandleUninstallGame(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            if (!game.IsInstalled) return new { ok = true, game = game.Name, action = "not_installed" };
            InvokeOnUI(() => PlayniteApi.UninstallGame(gameId));
            return new { ok = true, game = game.Name, action = "uninstall_started" };
        }

        #endregion

        #region Handlers - Game Sub-resources

        private object HandleSetGameList(Guid gameId, HttpListenerRequest req, string field)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ParseBody(req);
            var names = data.ContainsKey(field) ? data[field] : (data.ContainsKey("names") ? data["names"] : null);

            switch (field)
            {
                case "categories":
                    game.CategoryIds = ResolveIds(PlayniteApi.Database.Categories, names);
                    break;
                case "tags":
                    game.TagIds = ResolveIds(PlayniteApi.Database.Tags, names);
                    break;
                case "features":
                    game.FeatureIds = ResolveIds(PlayniteApi.Database.Features, names);
                    break;
                case "genres":
                    game.GenreIds = ResolveIds(PlayniteApi.Database.Genres, names);
                    break;
            }

            PlayniteApi.Database.Games.Update(game);
            return new { ok = true, game = game.Name, field, set = GetNameList(game, field) };
        }

        private object HandleAddGameList(Guid gameId, HttpListenerRequest req, string field)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ParseBody(req);
            var names = data.ContainsKey(field) ? data[field] : (data.ContainsKey("names") ? data["names"] : null);
            List<Guid> newIds;
            switch (field)
            {
                case "categories": newIds = ResolveIds(PlayniteApi.Database.Categories, names); break;
                case "tags": newIds = ResolveIds(PlayniteApi.Database.Tags, names); break;
                case "features": newIds = ResolveIds(PlayniteApi.Database.Features, names); break;
                case "genres": newIds = ResolveIds(PlayniteApi.Database.Genres, names); break;
                default: return new { error = $"Unknown field: {field}" };
            }

            List<Guid> currentIds;
            switch (field)
            {
                case "categories": currentIds = game.CategoryIds ?? (game.CategoryIds = new List<Guid>()); break;
                case "tags": currentIds = game.TagIds ?? (game.TagIds = new List<Guid>()); break;
                case "features": currentIds = game.FeatureIds ?? (game.FeatureIds = new List<Guid>()); break;
                case "genres": currentIds = game.GenreIds ?? (game.GenreIds = new List<Guid>()); break;
                default: return new { error = $"Unknown field: {field}" };
            }

            foreach (var id in newIds)
            {
                if (!currentIds.Contains(id))
                    currentIds.Add(id);
            }

            PlayniteApi.Database.Games.Update(game);
            return new { ok = true, game = game.Name, field, added = newIds.Count, total = GetNameList(game, field) };
        }

        private object HandleSetCompletionStatus(Guid gameId, HttpListenerRequest req)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ParseBody(req);
            var statusName = data["status"].ToString();

            var status = PlayniteApi.Database.CompletionStatuses
                .FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
            if (status == null) return new { error = $"Status '{statusName}' not found" };

            game.CompletionStatusId = status.Id;
            PlayniteApi.Database.Games.Update(game);
            return new { ok = true, game = game.Name, status = status.Name };
        }

        #endregion

        #region Handlers - Collections (generic)

        private object HandleGetCollection<T>(IItemCollection<T> collection) where T : DatabaseObject
        {
            if (collection == null) return new List<object>();
            return collection.OrderBy(x => x.Name).Select(x => new { id = x.Id.ToString(), name = x.Name }).ToList();
        }

        private object HandleCreateItem<T>(IItemCollection<T> collection, HttpListenerRequest req) where T : DatabaseObject
        {
            var data = ParseBody(req);
            var name = data["name"].ToString();
            var item = GetOrCreate(collection, name);
            return item != null
                ? (object)new { ok = true, id = item.Id.ToString(), name = item.Name }
                : new { error = "Failed to create item" };
        }

        private object HandleDeleteCollectionItem<T>(IItemCollection<T> collection, string idStr) where T : DatabaseObject
        {
            if (!Guid.TryParse(idStr, out var id))
                return Error(400, "Invalid ID");
            var item = collection.Get(id);
            if (item == null) return Error(404, "Item not found");
            var name = item.Name;
            collection.Remove(id);
            return new { ok = true, deleted = name };
        }

        private object HandleGetEmulators()
        {
            var emulators = PlayniteApi.Database.Emulators;
            if (emulators == null) return new List<object>();
            return emulators.OrderBy(e => e.Name).Select(e => new
            {
                id = e.Id.ToString(),
                name = e.Name,
                installDir = e.InstallDir
            }).ToList();
        }

        #endregion

        #region Handlers - View

        private object HandleGetViewState()
        {
            object result = null;
            InvokeOnUI(() =>
            {
                try
                {
                    result = new
                    {
                        selectedCount = PlayniteApi.MainView.SelectedGames?.Count() ?? 0,
                        activeDesktopView = PlayniteApi.MainView.ActiveDesktopView.ToString(),
                        sortOrder = PlayniteApi.MainView.SortOrder.ToString(),
                        grouping = PlayniteApi.MainView.Grouping.ToString(),
                        activeFilterPreset = PlayniteApi.MainView.GetActiveFilterPreset().ToString()
                    };
                }
                catch (Exception ex)
                {
                    result = new { error = "View state unavailable: " + ex.Message };
                }
            });
            return result;
        }

        private object HandleGetSelectedGames()
        {
            object result = null;
            InvokeOnUI(() =>
            {
                var selected = PlayniteApi.MainView.SelectedGames?.ToList();
                result = new
                {
                    count = selected?.Count ?? 0,
                    games = selected?.Select(g => SerializeGameCompact(g)).ToList() ?? new List<object>()
                };
            });
            return result;
        }

        private object HandleSelectGames(HttpListenerRequest req)
        {
            var data = ParseBody(req);
            var ids = ((ArrayList)data["gameIds"]).Cast<string>().Select(Guid.Parse).ToList();
            InvokeOnUI(() => PlayniteApi.MainView.SelectGames(ids));
            return new { ok = true, selected = ids.Count };
        }

        private object HandleApplyFilter(HttpListenerRequest req)
        {
            var data = ParseBody(req);
            var presetId = Guid.Parse(data["presetId"].ToString());
            InvokeOnUI(() => PlayniteApi.MainView.ApplyFilterPreset(presetId));
            return new { ok = true, presetId = presetId.ToString() };
        }

        #endregion

        #region Handlers - App

        private object HandleGetAppInfo()
        {
            return new
            {
                version = PlayniteApi.ApplicationInfo.ApplicationVersion.ToString(),
                mode = PlayniteApi.ApplicationInfo.Mode.ToString(),
                isPortable = PlayniteApi.ApplicationInfo.IsPortable,
                inOfflineMode = PlayniteApi.ApplicationInfo.InOfflineMode,
                apiPort = HttpPort,
                paths = new
                {
                    application = PlayniteApi.Paths.ApplicationPath,
                    configuration = PlayniteApi.Paths.ConfigurationPath,
                    extensionsData = PlayniteApi.Paths.ExtensionsDataPath
                }
            };
        }

        private object HandleGetAddons()
        {
            return new
            {
                installed = PlayniteApi.Addons.Addons ?? new List<string>(),
                disabled = PlayniteApi.Addons.DisabledAddons ?? new List<string>()
            };
        }

        private object HandleGetStats()
        {
            var games = PlayniteApi.Database.Games;
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

        private object HandleSendNotification(HttpListenerRequest req)
        {
            var data = ParseBody(req);
            var text = data["text"].ToString();
            var type = data.ContainsKey("type") && data["type"]?.ToString() == "error"
                ? NotificationType.Error
                : NotificationType.Info;
            var id = Guid.NewGuid().ToString();
            PlayniteApi.Notifications.Add(id, text, type);
            return new { ok = true, id };
        }

        #endregion

        #region Handlers - Auth

        private object HandleRotateToken()
        {
            RotateToken();
            return new { ok = true, token = _apiToken };
        }

        #endregion

        #region Categorization Logic

        private void AutoCategorizeByGenre()
        {
            var uncategorized = PlayniteApi.Database.Games
                .Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0).ToList();

            if (uncategorized.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage("All games are already categorized!", "Playnite Bridge");
                return;
            }

            var result = PlayniteApi.Dialogs.ShowMessage(
                $"Found {uncategorized.Count} uncategorized games. Categorize by primary genre?",
                "Playnite Bridge", System.Windows.MessageBoxButton.YesNo);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            var count = CategorizeGames(uncategorized);
            PlayniteApi.Dialogs.ShowMessage($"Categorized {count} games.", "Playnite Bridge");
        }

        private void AutoCategorizeSelected()
        {
            var selected = PlayniteApi.MainView.SelectedGames?.ToList();
            if (selected == null || selected.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage("No games selected.", "Playnite Bridge");
                return;
            }
            var count = CategorizeGames(selected);
            PlayniteApi.Dialogs.ShowMessage($"Categorized {count} out of {selected.Count} games.", "Playnite Bridge");
        }

        private int CategorizeGames(List<Game> games)
        {
            int count = 0;
            foreach (var game in games)
            {
                if (game.Genres == null || game.Genres.Count == 0) continue;
                var primaryGenre = game.Genres.First().Name;
                var category = GetOrCreate(PlayniteApi.Database.Categories, primaryGenre);
                if (category == null) continue;

                if (game.CategoryIds == null) game.CategoryIds = new List<Guid>();
                if (!game.CategoryIds.Contains(category.Id))
                {
                    game.CategoryIds.Add(category.Id);
                    PlayniteApi.Database.Games.Update(game);
                    count++;
                }
            }
            return count;
        }

        private void ShowUncategorized()
        {
            var uncategorized = PlayniteApi.Database.Games
                .Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0)
                .Select(g => g.Name).OrderBy(n => n).ToList();

            PlayniteApi.Dialogs.ShowMessage(
                uncategorized.Count == 0
                    ? "All games are categorized!"
                    : $"Uncategorized ({uncategorized.Count}):\n\n{string.Join("\n", uncategorized.Take(50))}" +
                      (uncategorized.Count > 50 ? $"\n... and {uncategorized.Count - 50} more" : ""),
                "Playnite Bridge");
        }

        private object HandleAutoCategorize()
        {
            var uncategorized = PlayniteApi.Database.Games
                .Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0).ToList();
            var count = CategorizeGames(uncategorized);
            return new { ok = true, categorized = count, total = uncategorized.Count };
        }

        private object HandleCategorize(HttpListenerRequest req)
        {
            var data = ParseBody(req);
            var gameId = Guid.Parse(data["gameId"].ToString());
            var categoryNames = ((ArrayList)data["categories"]).Cast<string>().ToList();

            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            if (game.CategoryIds == null) game.CategoryIds = new List<Guid>();
            foreach (var catName in categoryNames)
            {
                var cat = GetOrCreate(PlayniteApi.Database.Categories, catName);
                if (cat != null && !game.CategoryIds.Contains(cat.Id))
                    game.CategoryIds.Add(cat.Id);
            }

            PlayniteApi.Database.Games.Update(game);
            return new { ok = true, game = game.Name, categories = categoryNames };
        }

        #endregion

        #region Artwork Logic

        private object HandleGetMissingArt()
        {
            var results = new List<object>();
            foreach (var g in PlayniteApi.Database.Games.OrderBy(x => x.Name))
            {
                var mi = string.IsNullOrEmpty(g.Icon);
                var mc = string.IsNullOrEmpty(g.CoverImage);
                var mb = string.IsNullOrEmpty(g.BackgroundImage);
                if (!mi && !mc && !mb) continue;

                var missing = new List<string>();
                if (mi) missing.Add("icon");
                if (mc) missing.Add("cover");
                if (mb) missing.Add("background");

                results.Add(new
                {
                    id = g.Id.ToString(), name = g.Name,
                    gameId = g.GameId, source = g.Source?.Name ?? "",
                    pluginId = g.PluginId.ToString(), missing
                });
            }
            return new { total = PlayniteApi.Database.Games.Count, missingArt = results.Count, games = results };
        }

        private object HandleFetchArt(Guid gameId)
        {
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            return FetchArtForGame(game);
        }

        private object HandleFetchAllArt()
        {
            var results = new List<object>();
            int fetched = 0, failed = 0, skipped = 0;

            foreach (var game in PlayniteApi.Database.Games.OrderBy(x => x.Name))
            {
                if (!string.IsNullOrEmpty(game.CoverImage) && !string.IsNullOrEmpty(game.BackgroundImage) && !string.IsNullOrEmpty(game.Icon))
                { skipped++; continue; }

                try { results.Add(FetchArtForGame(game)); fetched++; }
                catch (Exception ex) { results.Add(new { game = game.Name, error = ex.Message }); failed++; }
            }
            return new { fetched, failed, skipped, details = results };
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

            if (updated.Count > 0) PlayniteApi.Database.Games.Update(game);

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
                var dbPath = PlayniteApi.Database.AddFile(tempPath, game.Id);
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

            var configPath = Path.Combine(GetPluginUserDataPath(), "igdb.json");
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

        #endregion

        #region Skill Template

        private string GenerateSkillMd()
        {
            string template;
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("skill.md"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                template = reader.ReadToEnd();

            return template
                .Replace("%%HOST%%", _isNetworkBound ? GetLocalIpAddress() : "localhost")
                .Replace("%%PORT%%", HttpPort.ToString())
                .Replace("%%TOKEN%%", _apiToken);
        }

        #endregion

        #region Helpers

        private static string GetLocalIpAddress()
        {
            try
            {
                using (var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.InterNetwork,
                    System.Net.Sockets.SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var ep = socket.LocalEndPoint as System.Net.IPEndPoint;
                    return ep?.Address.ToString() ?? "localhost";
                }
            }
            catch
            {
                return "localhost";
            }
        }

        private string ReadBody(HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private Dictionary<string, object> ParseBody(HttpListenerRequest req)
        {
            return _json.Deserialize<Dictionary<string, object>>(ReadBody(req));
        }

        private void InvokeOnUI(Action action)
        {
            PlayniteApi.MainView.UIDispatcher.Invoke(action);
        }

        private object Error(int code, string message)
        {
            return new { error = message, code };
        }

        private T GetOrCreate<T>(IItemCollection<T> collection, string name) where T : DatabaseObject
        {
            if (collection == null || string.IsNullOrEmpty(name)) return null;
            var existing = collection.FirstOrDefault(x => x.Name != null && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            try
            {
                var item = (T)Activator.CreateInstance(typeof(T), new object[] { name });
                collection.Add(item);
                return item;
            }
            catch
            {
                return null;
            }
        }

        private List<Guid> ResolveIds<T>(IItemCollection<T> collection, object names, bool create = true) where T : DatabaseObject
        {
            var nameList = (names as ArrayList)?.Cast<string>().ToList();
            if (nameList == null || nameList.Count == 0) return new List<Guid>();

            var ids = new List<Guid>();
            foreach (var name in nameList)
            {
                if (string.IsNullOrEmpty(name)) continue;

                if (create)
                {
                    var item = GetOrCreate(collection, name);
                    if (item != null) ids.Add(item.Id);
                }
                else
                {
                    var item = collection.FirstOrDefault(x => x.Name != null && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (item != null) ids.Add(item.Id);
                }
            }
            return ids;
        }

        private object GetCollection(string field)
        {
            switch (field)
            {
                case "categories": return PlayniteApi.Database.Categories;
                case "tags": return PlayniteApi.Database.Tags;
                case "features": return PlayniteApi.Database.Features;
                case "genres": return PlayniteApi.Database.Genres;
                default: return null;
            }
        }

        private IItemCollection<T> GetTypedCollection<T>(string field) where T : DatabaseObject
        {
            return GetCollection(field) as IItemCollection<T>;
        }

        private List<string> GetNameList(Game game, string field)
        {
            switch (field)
            {
                case "categories": return game.Categories?.Select(x => x.Name).ToList() ?? new List<string>();
                case "tags": return game.Tags?.Select(x => x.Name).ToList() ?? new List<string>();
                case "features": return game.Features?.Select(x => x.Name).ToList() ?? new List<string>();
                case "genres": return game.Genres?.Select(x => x.Name).ToList() ?? new List<string>();
                default: return new List<string>();
            }
        }

        private object SerializeGameCompact(Game g)
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

        private object SerializeGameFull(Game g)
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

        #endregion
    }

    internal static class DictExtensions
    {
        public static TValue GetValueOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TValue : class
        {
            return dict.ContainsKey(key) ? dict[key] : null;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            return dict.ContainsKey(key) ? dict[key] : defaultValue;
        }
    }
}
