namespace PlayniteBridge.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;

    internal class SyncEngine
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        private const int PushBatchSize = 200;

        private readonly IPlayniteAPI _api;
        private readonly SyncClient _client;
        private readonly SyncState _state;
        private readonly GameSerializationService _serializer;
        private readonly CollectionResolver _resolver;
        private readonly Func<string> _getPluginDataPath;
        private readonly object _syncLock = new object();
        private volatile bool _isSyncing;

        public bool IsSyncing => _isSyncing;
        public string LastError => _state?.LastSyncError;
        public string LastSyncTime => _state?.LastSyncTime;

        public SyncEngine(IPlayniteAPI api, SyncClient client, SyncState state,
            GameSerializationService serializer, CollectionResolver resolver,
            Func<string> getPluginDataPath)
        {
            _api = api;
            _client = client;
            _state = state;
            _serializer = serializer;
            _resolver = resolver;
            _getPluginDataPath = getPluginDataPath;
        }

        /// <summary>Check commands, then push diff, then pull diff.</summary>
        public SyncResult RunSync()
        {
            if (!_state.IsRegistered) return Fail("Not registered with sync backend");
            if (!Monitor.TryEnter(_syncLock)) return Fail("Sync already in progress");

            _isSyncing = true;
            try
            {
                return DoSync(fullResync: false);
            }
            finally
            {
                _isSyncing = false;
                Monitor.Exit(_syncLock);
            }
        }

        /// <summary>Reset cursors and do full push+pull.</summary>
        public SyncResult RunFullResync()
        {
            if (!_state.IsRegistered) return Fail("Not registered with sync backend");
            if (!Monitor.TryEnter(_syncLock)) return Fail("Sync already in progress");

            _isSyncing = true;
            try
            {
                _state.LastPushCursor = null;
                _state.LastPullCursor = null;
                _state.Save();
                return DoSync(fullResync: true);
            }
            finally
            {
                _isSyncing = false;
                Monitor.Exit(_syncLock);
            }
        }

        /// <summary>Quick push of a single game after it stops.</summary>
        public SyncResult PushSingleGame(Game game)
        {
            if (!_state.IsRegistered) return Fail("Not registered");
            if (!HasCanonicalKey(game)) return new SyncResult { Success = true, GamesSkipped = 1 };

            try
            {
                var serialized = _serializer.SerializeFull(game);
                var response = _client.Push("games", new List<object> { serialized });
                if (response == null) return Fail("Push failed");
                return new SyncResult { Success = true, GamesPushed = response.accepted, GamesSkipped = response.skipped };
            }
            catch (Exception ex)
            {
                return Fail($"PushSingleGame error: {ex.Message}");
            }
        }

        /// <summary>Request connection to a backend (creates pending client).</summary>
        public string RequestConnection(string backendUrl)
        {
            var tempClient = new SyncClient(backendUrl, "");
            var machineName = Environment.MachineName;
            var version = _api.ApplicationInfo?.ApplicationVersion?.ToString() ?? "unknown";

            var response = tempClient.RequestConnection(machineName, version);
            if (response == null || string.IsNullOrEmpty(response.clientId))
            {
                Logger.Warn("Connection request failed");
                return null;
            }

            _state.BackendUrl = backendUrl;
            _state.ClientId = response.clientId;
            _state.ApiKey = null; // not yet approved
            _state.Save();

            Logger.Info($"Connection requested: {response.clientId} at {backendUrl}");
            return response.clientId;
        }

        /// <summary>Approve connection with PSR code.</summary>
        public bool ApproveWithCode(string registrationCode)
        {
            if (string.IsNullOrEmpty(_state.ClientId) || string.IsNullOrEmpty(_state.BackendUrl))
                return false;

            var tempClient = new SyncClient(_state.BackendUrl, "");
            var result = tempClient.ApproveWithCode(_state.ClientId, registrationCode);
            return FinishApproval(result);
        }

        /// <summary>Poll backend for approval status. Returns true if approved.</summary>
        public bool PollApproval()
        {
            if (string.IsNullOrEmpty(_state.ClientId) || string.IsNullOrEmpty(_state.BackendUrl))
                return false;

            var tempClient = new SyncClient(_state.BackendUrl, "");
            var result = tempClient.PollStatus(_state.ClientId);
            if (result == null || result.status != "active") return false;
            return FinishApproval(result);
        }

        private bool FinishApproval(SyncClientStatus result)
        {
            if (result == null || result.status != "active" || string.IsNullOrEmpty(result.apiKey))
            {
                Logger.Warn("Approval failed or not yet approved");
                return false;
            }

            _state.ApiKey = result.apiKey;
            _state.Save();

            _client.UpdateCredentials(_state.BackendUrl, result.apiKey);
            Logger.Info($"Connection approved for client {_state.ClientId}");
            return true;
        }

        private SyncResult DoSync(bool fullResync)
        {
            var result = new SyncResult { Success = true };

            try
            {
                // 1. Check for server commands
                var commands = _client.GetCommands();
                foreach (var cmd in commands)
                {
                    if (cmd.command == "full_resync")
                    {
                        Logger.Info("Server requested full resync");
                        _state.LastPushCursor = null;
                        _state.LastPullCursor = null;
                        fullResync = true;
                    }
                    _client.AckCommand(cmd.id);
                }

                // 2. Push changed games
                var pushResult = PushChangedGames(fullResync);
                result.GamesPushed = pushResult.GamesPushed;
                result.GamesSkipped = pushResult.GamesSkipped;

                // 3. Pull changes from other clients
                var pullResult = PullChanges();
                result.GamesPulled = pullResult.GamesPulled;
                result.GamesUpdated = pullResult.GamesUpdated;
                result.GamesCreated = pullResult.GamesCreated;

                _state.LastSyncTime = DateTime.UtcNow.ToString("o");
                _state.LastSyncError = null;
                _state.Save();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _state.LastSyncError = ex.Message;
                _state.Save();
                Logger.Error($"Sync failed: {ex.Message}");
            }

            return result;
        }

        private SyncResult PushChangedGames(bool full)
        {
            var result = new SyncResult { Success = true };
            DateTime? since = null;

            if (!full && !string.IsNullOrEmpty(_state.LastPushCursor))
            {
                DateTimeOffset parsed;
                if (DateTimeOffset.TryParse(_state.LastPushCursor, out parsed))
                    since = parsed.UtcDateTime;
            }

            // Collect games with canonical keys, changed since cursor
            var allGames = _api.Database.Games
                .Where(g => since == null || g.Modified == null || g.Modified.Value.ToUniversalTime() > since)
                .ToList();

            var games = allGames.Where(g => HasCanonicalKey(g)).ToList();
            var skippedCount = allGames.Count - games.Count;

            if (games.Count == 0)
            {
                if (skippedCount > 0)
                    Logger.Info($"No syncable games ({skippedCount} skipped — no source or gameId)");
                else
                    Logger.Info("No games to push");
                result.GamesSkipped = skippedCount;
                return result;
            }

            Logger.Info($"Pushing {games.Count} games, {skippedCount} skipped (full={full})");

            // Batch and push
            for (int i = 0; i < games.Count; i += PushBatchSize)
            {
                var batch = games.Skip(i).Take(PushBatchSize)
                    .Select(g => _serializer.SerializeFull(g))
                    .ToList();

                var response = _client.Push("games", batch);
                if (response == null)
                {
                    result.Success = false;
                    result.Error = "Push batch failed";
                    break;
                }

                result.GamesPushed += response.accepted;
                result.GamesSkipped += response.skipped;

                _state.LastPushCursor = response.newCursor;
                _state.Save();
            }

            return result;
        }

        private SyncResult PullChanges()
        {
            var result = new SyncResult { Success = true };
            var since = _state.LastPullCursor ?? "1970-01-01T00:00:00Z";
            bool hasMore = true;

            while (hasMore)
            {
                var response = _client.Pull("games", since);
                if (response == null)
                {
                    result.Success = false;
                    result.Error = "Pull failed";
                    break;
                }

                if (response.items != null && response.items.Count > 0)
                {
                    var applied = ApplyPulledGames(response.items);
                    result.GamesUpdated += applied.Item1;
                    result.GamesCreated += applied.Item2;
                    result.GamesPulled += response.items.Count;
                }

                since = response.cursor;
                hasMore = response.hasMore;
            }

            _state.LastPullCursor = since;
            _state.Save();
            return result;
        }

        /// <summary>Apply pulled games to local DB. Returns (updated, created).</summary>
        private Tuple<int, int> ApplyPulledGames(List<Dictionary<string, object>> items)
        {
            int updated = 0, created = 0;

            _api.MainView.UIDispatcher.Invoke(() =>
            {
                foreach (var data in items)
                {
                    try
                    {
                        var local = FindLocalGame(data);
                        if (local != null)
                        {
                            if (ApplyFields(local, data))
                            {
                                _api.Database.Games.Update(local);
                                updated++;
                            }
                        }
                        else
                        {
                            var newGame = CreateFromServerData(data);
                            if (newGame != null)
                            {
                                _api.Database.Games.Add(newGame);
                                created++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to apply game: {ex.Message}");
                    }
                }
            });

            return Tuple.Create(updated, created);
        }

        private Game FindLocalGame(Dictionary<string, object> data)
        {
            // Match by gameId + source (canonical key)
            var gameId = GetString(data, "gameId");
            var source = GetString(data, "source");

            if (!string.IsNullOrEmpty(gameId) && !string.IsNullOrEmpty(source))
            {
                var match = _api.Database.Games.FirstOrDefault(g =>
                    g.GameId == gameId && g.Source != null && g.Source.Name == source);
                if (match != null) return match;
            }

            // Fallback: try by server ID (might be our own Playnite GUID)
            var id = GetString(data, "id");
            if (!string.IsNullOrEmpty(id))
            {
                Guid guid;
                if (Guid.TryParse(id, out guid))
                {
                    var match = _api.Database.Games.Get(guid);
                    if (match != null) return match;
                }
            }

            return null;
        }

        private bool ApplyFields(Game game, Dictionary<string, object> data)
        {
            bool changed = false;

            // Playtime: only update if server has more (MAX)
            var pt = GetLong(data, "playtime");
            if (pt > (long)game.Playtime) { game.Playtime = (ulong)pt; changed = true; }

            var pc = GetLong(data, "playCount");
            if (pc > (long)game.PlayCount) { game.PlayCount = (ulong)pc; changed = true; }

            // LWW fields: apply if different (server already resolved conflicts)
            var completionStatus = GetString(data, "completionStatus");
            if (!string.IsNullOrEmpty(completionStatus) && (game.CompletionStatus?.Name ?? "") != completionStatus)
            {
                var cs = _api.Database.CompletionStatuses.FirstOrDefault(s => s.Name == completionStatus);
                if (cs != null) { game.CompletionStatusId = cs.Id; changed = true; }
            }

            // Categories (UNION from server)
            var categories = GetStringList(data, "categories");
            if (categories.Count > 0)
            {
                var categoryIds = _resolver.ResolveIds(_api.Database.Categories, categories);
                if (game.CategoryIds == null) game.CategoryIds = new List<Guid>();
                foreach (var cid in categoryIds)
                {
                    if (!game.CategoryIds.Contains(cid)) { game.CategoryIds.Add(cid); changed = true; }
                }
            }

            // Tags
            var tags = GetStringList(data, "tags");
            if (tags.Count > 0)
            {
                var tagIds = _resolver.ResolveIds(_api.Database.Tags, tags);
                if (game.TagIds == null) game.TagIds = new List<Guid>();
                foreach (var tid in tagIds)
                {
                    if (!game.TagIds.Contains(tid)) { game.TagIds.Add(tid); changed = true; }
                }
            }

            // Genres
            var genres = GetStringList(data, "genres");
            if (genres.Count > 0)
            {
                var genreIds = _resolver.ResolveIds(_api.Database.Genres, genres);
                if (game.GenreIds == null) game.GenreIds = new List<Guid>();
                foreach (var gid in genreIds)
                {
                    if (!game.GenreIds.Contains(gid)) { game.GenreIds.Add(gid); changed = true; }
                }
            }

            // Favorite / Hidden
            if (data.ContainsKey("favorite"))
            {
                var fav = Convert.ToBoolean(data["favorite"]);
                if (game.Favorite != fav) { game.Favorite = fav; changed = true; }
            }

            if (data.ContainsKey("hidden"))
            {
                var hid = Convert.ToBoolean(data["hidden"]);
                if (game.Hidden != hid) { game.Hidden = hid; changed = true; }
            }

            return changed;
        }

        private Game CreateFromServerData(Dictionary<string, object> data)
        {
            var name = GetString(data, "name");
            if (string.IsNullOrEmpty(name)) return null;

            var game = new Game(name);

            // Source
            var source = GetString(data, "source");
            if (!string.IsNullOrEmpty(source))
            {
                var src = _api.Database.Sources.FirstOrDefault(s => s.Name == source);
                if (src != null) game.SourceId = src.Id;
            }

            // GameId
            game.GameId = GetString(data, "gameId");

            // Apply all shared fields
            ApplyFields(game, data);

            return game;
        }

        internal static bool HasCanonicalKey(Game game)
        {
            return game.Source != null
                && !string.IsNullOrEmpty(game.Source.Name)
                && !string.IsNullOrEmpty(game.GameId);
        }

        private static string GetString(Dictionary<string, object> data, string key)
        {
            object val;
            return data.TryGetValue(key, out val) && val != null ? val.ToString() : null;
        }

        private static long GetLong(Dictionary<string, object> data, string key)
        {
            object val;
            if (data.TryGetValue(key, out val) && val != null)
                return Convert.ToInt64(val);
            return 0;
        }

        private static List<string> GetStringList(Dictionary<string, object> data, string key)
        {
            object val;
            if (data.TryGetValue(key, out val) && val is ArrayList list)
                return list.Cast<object>().Select(x => x.ToString()).ToList();
            return new List<string>();
        }

        private static SyncResult Fail(string error)
        {
            return new SyncResult { Success = false, Error = error };
        }
    }
}
