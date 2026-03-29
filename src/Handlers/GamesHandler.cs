namespace PlayniteBridge.Handlers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    internal class GamesHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly CollectionResolver _resolver;
        private readonly GameSerializationService _serializer;

        public GamesHandler(IPlayniteAPI api, CollectionResolver resolver, GameSerializationService serializer)
        {
            _api = api;
            _resolver = resolver;
            _serializer = serializer;
        }

        public object GetGames(RequestContext ctx)
        {
            IEnumerable<Game> games = _api.Database.Games;

            var q = ctx.QueryString?["q"];
            if (!string.IsNullOrEmpty(q))
                games = games.Where(g => g.Name != null && g.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            if (ctx.QueryString?["installed"] == "true")
                games = games.Where(g => g.IsInstalled);

            if (ctx.QueryString?["favorite"] == "true")
                games = games.Where(g => g.Favorite);

            if (ctx.QueryString?["hidden"] != "true")
                games = games.Where(g => !g.Hidden);

            var uncategorized = ctx.QueryString?["uncategorized"];
            if (uncategorized == "true")
                games = games.Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0);

            var source = ctx.QueryString?["source"];
            if (!string.IsNullOrEmpty(source))
                games = games.Where(g => g.Source != null && g.Source.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            var genre = ctx.QueryString?["genre"];
            if (!string.IsNullOrEmpty(genre))
                games = games.Where(g => g.Genres != null && g.Genres.Any(x => x.Name.Equals(genre, StringComparison.OrdinalIgnoreCase)));

            var category = ctx.QueryString?["category"];
            if (!string.IsNullOrEmpty(category))
                games = games.Where(g => g.Categories != null && g.Categories.Any(x => x.Name.Equals(category, StringComparison.OrdinalIgnoreCase)));

            var tag = ctx.QueryString?["tag"];
            if (!string.IsNullOrEmpty(tag))
                games = games.Where(g => g.Tags != null && g.Tags.Any(x => x.Name.Equals(tag, StringComparison.OrdinalIgnoreCase)));

            var feature = ctx.QueryString?["feature"];
            if (!string.IsNullOrEmpty(feature))
                games = games.Where(g => g.Features != null && g.Features.Any(x => x.Name.Equals(feature, StringComparison.OrdinalIgnoreCase)));

            var platform = ctx.QueryString?["platform"];
            if (!string.IsNullOrEmpty(platform))
                games = games.Where(g => g.Platforms != null && g.Platforms.Any(x => x.Name.Equals(platform, StringComparison.OrdinalIgnoreCase)));

            var completionStatus = ctx.QueryString?["completionStatus"];
            if (!string.IsNullOrEmpty(completionStatus))
                games = games.Where(g => g.CompletionStatus != null && g.CompletionStatus.Name.Equals(completionStatus, StringComparison.OrdinalIgnoreCase));

            var ordered = games.OrderBy(g => g.Name);

            int offset = 0, limit = 500;
            if (int.TryParse(ctx.QueryString?["offset"], out var o)) offset = Math.Max(0, o);
            if (int.TryParse(ctx.QueryString?["limit"], out var l)) limit = Math.Min(Math.Max(l, 1), 5000);

            var total = 0;
            try { total = ordered.Count(); } catch { }

            var page = ordered.Skip(offset).Take(limit);

            return new
            {
                total,
                offset,
                limit,
                games = page.Select(g => _serializer.SerializeCompact(g)).ToList()
            };
        }

        public object GetGame(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            return _serializer.SerializeFull(game);
        }

        public object CreateGame(RequestContext ctx)
        {
            var data = ctx.Body;
            var name = data["name"].ToString();

            var game = new Game(name);

            if (data.ContainsKey("source") && data["source"] != null)
            {
                var sourceName = data["source"].ToString();
                var source = _api.Database.Sources
                    .FirstOrDefault(s => s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
                if (source == null)
                {
                    source = new GameSource(sourceName);
                    _api.Database.Sources.Add(source);
                }
                game.SourceId = source.Id;
            }

            if (data.ContainsKey("platforms"))
                game.PlatformIds = _resolver.ResolveIds(_api.Database.Platforms, data["platforms"], create: true);
            if (data.ContainsKey("genres"))
                game.GenreIds = _resolver.ResolveIds(_api.Database.Genres, data["genres"]);
            if (data.ContainsKey("categories"))
                game.CategoryIds = _resolver.ResolveIds(_api.Database.Categories, data["categories"]);
            if (data.ContainsKey("tags"))
                game.TagIds = _resolver.ResolveIds(_api.Database.Tags, data["tags"]);
            if (data.ContainsKey("features"))
                game.FeatureIds = _resolver.ResolveIds(_api.Database.Features, data["features"]);
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
                    var status = _api.Database.CompletionStatuses
                        .FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
                    if (status != null) game.CompletionStatusId = status.Id;
                }
            }

            _api.Database.Games.Add(game);
            return new { ok = true, id = game.Id.ToString(), name = game.Name };
        }

        public object UpdateGame(Guid gameId, RequestContext ctx)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ctx.Body;

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

            if (data.ContainsKey("categories"))
                game.CategoryIds = _resolver.ResolveIds(_api.Database.Categories, data["categories"]);
            if (data.ContainsKey("tags"))
                game.TagIds = _resolver.ResolveIds(_api.Database.Tags, data["tags"]);
            if (data.ContainsKey("genres"))
                game.GenreIds = _resolver.ResolveIds(_api.Database.Genres, data["genres"]);
            if (data.ContainsKey("features"))
                game.FeatureIds = _resolver.ResolveIds(_api.Database.Features, data["features"]);
            if (data.ContainsKey("developers"))
                game.DeveloperIds = _resolver.ResolveIds(_api.Database.Companies, data["developers"]);
            if (data.ContainsKey("publishers"))
                game.PublisherIds = _resolver.ResolveIds(_api.Database.Companies, data["publishers"]);
            if (data.ContainsKey("series"))
                game.SeriesIds = _resolver.ResolveIds(_api.Database.Series, data["series"]);
            if (data.ContainsKey("platforms"))
                game.PlatformIds = _resolver.ResolveIds(_api.Database.Platforms, data["platforms"], create: false);
            if (data.ContainsKey("ageRatings"))
                game.AgeRatingIds = _resolver.ResolveIds(_api.Database.AgeRatings, data["ageRatings"], create: false);
            if (data.ContainsKey("regions"))
                game.RegionIds = _resolver.ResolveIds(_api.Database.Regions, data["regions"], create: false);

            if (data.ContainsKey("completionStatus"))
            {
                var statusName = data["completionStatus"]?.ToString();
                if (!string.IsNullOrEmpty(statusName))
                {
                    var status = _api.Database.CompletionStatuses
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

            _api.Database.Games.Update(game);
            return _serializer.SerializeFull(game);
        }

        public object DeleteGame(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            var name = game.Name;
            _api.Database.Games.Remove(gameId);
            return new { ok = true, deleted = name };
        }

        public object LaunchGame(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            if (!game.IsInstalled) return new { error = "Game is not installed" };
            _api.MainView.UIDispatcher.Invoke(() => _api.StartGame(gameId));
            return new { ok = true, game = game.Name, action = "launched" };
        }

        public object InstallGame(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            if (game.IsInstalled) return new { ok = true, game = game.Name, action = "already_installed" };
            _api.MainView.UIDispatcher.Invoke(() => _api.InstallGame(gameId));
            return new { ok = true, game = game.Name, action = "install_started" };
        }

        public object UninstallGame(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");
            if (!game.IsInstalled) return new { ok = true, game = game.Name, action = "not_installed" };
            _api.MainView.UIDispatcher.Invoke(() => _api.UninstallGame(gameId));
            return new { ok = true, game = game.Name, action = "uninstall_started" };
        }

        public object SetGameList(Guid gameId, RequestContext ctx, string field)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ctx.Body;
            var names = data.ContainsKey(field) ? data[field] : (data.ContainsKey("names") ? data["names"] : null);

            switch (field)
            {
                case "categories":
                    game.CategoryIds = _resolver.ResolveIds(_api.Database.Categories, names);
                    break;
                case "tags":
                    game.TagIds = _resolver.ResolveIds(_api.Database.Tags, names);
                    break;
                case "features":
                    game.FeatureIds = _resolver.ResolveIds(_api.Database.Features, names);
                    break;
                case "genres":
                    game.GenreIds = _resolver.ResolveIds(_api.Database.Genres, names);
                    break;
            }

            _api.Database.Games.Update(game);
            return new { ok = true, game = game.Name, field, set = _resolver.GetNameList(game, field) };
        }

        public object AddGameList(Guid gameId, RequestContext ctx, string field)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ctx.Body;
            var names = data.ContainsKey(field) ? data[field] : (data.ContainsKey("names") ? data["names"] : null);
            List<Guid> newIds;
            switch (field)
            {
                case "categories": newIds = _resolver.ResolveIds(_api.Database.Categories, names); break;
                case "tags": newIds = _resolver.ResolveIds(_api.Database.Tags, names); break;
                case "features": newIds = _resolver.ResolveIds(_api.Database.Features, names); break;
                case "genres": newIds = _resolver.ResolveIds(_api.Database.Genres, names); break;
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

            _api.Database.Games.Update(game);
            return new { ok = true, game = game.Name, field, added = newIds.Count, total = _resolver.GetNameList(game, field) };
        }

        public object SetCompletionStatus(Guid gameId, RequestContext ctx)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return Error(404, "Game not found");

            var data = ctx.Body;
            var statusName = data["status"].ToString();

            var status = _api.Database.CompletionStatuses
                .FirstOrDefault(s => s.Name.Equals(statusName, StringComparison.OrdinalIgnoreCase));
            if (status == null) return new { error = $"Status '{statusName}' not found" };

            game.CompletionStatusId = status.Id;
            _api.Database.Games.Update(game);
            return new { ok = true, game = game.Name, status = status.Name };
        }

        public object GetMissingArt()
        {
            var results = new List<object>();
            foreach (var g in _api.Database.Games.OrderBy(x => x.Name))
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
            return new { total = _api.Database.Games.Count, missingArt = results.Count, games = results };
        }

        private static object Error(int code, string message)
        {
            return new { error = message, code };
        }
    }
}
