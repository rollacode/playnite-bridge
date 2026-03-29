namespace PlayniteBridge.Services
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;

    internal class GameQueryService
    {
        public IEnumerable<Game> Filter(IEnumerable<Game> games, Dictionary<string, object> filters)
        {
            var q = filters.GetValueOrNull("q") as string;
            if (!string.IsNullOrEmpty(q))
                games = games.Where(g => g.Name != null && g.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            if (filters.ContainsKey("installed") && filters["installed"] is bool inst)
                games = inst ? games.Where(g => g.IsInstalled) : games.Where(g => !g.IsInstalled);
            if (filters.ContainsKey("favorite") && filters["favorite"] is bool fav)
                games = fav ? games.Where(g => g.Favorite) : games.Where(g => !g.Favorite);
            if (filters.ContainsKey("hidden") && filters["hidden"] is bool hid)
                games = hid ? games.Where(g => g.Hidden) : games.Where(g => !g.Hidden);
            else
                games = games.Where(g => !g.Hidden);

            if (filters.ContainsKey("playtimeMin"))
            {
                var min = Convert.ToUInt64(filters["playtimeMin"]);
                games = games.Where(g => g.Playtime >= min);
            }
            if (filters.ContainsKey("playtimeMax"))
            {
                var max = Convert.ToUInt64(filters["playtimeMax"]);
                games = games.Where(g => g.Playtime <= max);
            }

            games = ApplyListFilter(games, filters, "genres", g => g.Genres);
            games = ApplyListFilter(games, filters, "categories", g => g.Categories);
            games = ApplyListFilter(games, filters, "tags", g => g.Tags);
            games = ApplyListFilter(games, filters, "features", g => g.Features);
            games = ApplyListFilter(games, filters, "developers", g => g.Developers);
            games = ApplyListFilter(games, filters, "publishers", g => g.Publishers);
            games = ApplyListFilter(games, filters, "platforms", g => g.Platforms);

            var source = filters.GetValueOrNull("source") as string;
            if (!string.IsNullOrEmpty(source))
                games = games.Where(g => g.Source != null && g.Source.Name.Equals(source, StringComparison.OrdinalIgnoreCase));

            var status = filters.GetValueOrNull("completionStatus") as string;
            if (!string.IsNullOrEmpty(status))
                games = games.Where(g => g.CompletionStatus != null && g.CompletionStatus.Name.Equals(status, StringComparison.OrdinalIgnoreCase));

            if (filters.ContainsKey("releaseYearMin"))
                games = games.Where(g => g.ReleaseDate != null && g.ReleaseDate.Value.Year >= Convert.ToInt32(filters["releaseYearMin"]));
            if (filters.ContainsKey("releaseYearMax"))
                games = games.Where(g => g.ReleaseDate != null && g.ReleaseDate.Value.Year <= Convert.ToInt32(filters["releaseYearMax"]));

            if (filters.ContainsKey("uncategorized") && filters["uncategorized"] is bool uc && uc)
                games = games.Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0);
            if (filters.ContainsKey("untagged") && filters["untagged"] is bool ut && ut)
                games = games.Where(g => g.TagIds == null || g.TagIds.Count == 0);

            return games;
        }

        public IOrderedEnumerable<Game> Sort(IEnumerable<Game> games, string sort, bool descending)
        {
            sort = (sort ?? "name").ToLower();
            switch (sort)
            {
                case "playtime": return descending ? games.OrderByDescending(g => g.Playtime) : games.OrderBy(g => g.Playtime);
                case "added": return descending ? games.OrderByDescending(g => g.Added) : games.OrderBy(g => g.Added);
                case "release": return descending ? games.OrderByDescending(g => g.ReleaseDate) : games.OrderBy(g => g.ReleaseDate);
                case "lastplayed": return descending ? games.OrderByDescending(g => g.LastActivity) : games.OrderBy(g => g.LastActivity);
                default: return descending ? games.OrderByDescending(g => g.Name) : games.OrderBy(g => g.Name);
            }
        }

        public object GroupBy(IEnumerable<Game> games, string groupBy)
        {
            switch (groupBy.ToLower())
            {
                case "genre":
                    return GroupByMulti(games, g => g.Genres);
                case "developer":
                    return GroupByMulti(games, g => g.Developers);
                case "publisher":
                    return GroupByMulti(games, g => g.Publishers);
                case "platform":
                    return GroupByMulti(games, g => g.Platforms);
                case "source":
                    return games.GroupBy(g => g.Source != null ? g.Source.Name : "Unknown")
                        .Select(grp => new { group = grp.Key, count = grp.Count(), totalHours = grp.Sum(g => (long)(g.Playtime / 3600)) })
                        .OrderByDescending(x => x.totalHours).ToList();
                case "year":
                    return games.Where(g => g.ReleaseDate != null).GroupBy(g => g.ReleaseDate.Value.Year)
                        .Select(grp => new { group = grp.Key.ToString(), count = grp.Count(), totalHours = grp.Sum(g => (long)(g.Playtime / 3600)) })
                        .OrderByDescending(x => x.group).ToList();
                case "completionstatus":
                    return games.GroupBy(g => g.CompletionStatus != null ? g.CompletionStatus.Name : "Not Set")
                        .Select(grp => new { group = grp.Key, count = grp.Count(), totalHours = grp.Sum(g => (long)(g.Playtime / 3600)) })
                        .OrderByDescending(x => x.totalHours).ToList();
                default:
                    return null;
            }
        }

        private object GroupByMulti(IEnumerable<Game> games, Func<Game, IEnumerable<DatabaseObject>> accessor)
        {
            return games.Where(g => accessor(g) != null)
                .SelectMany(g => accessor(g).Select(x => new { key = x.Name, game = g }))
                .GroupBy(x => x.key)
                .Select(grp => new { group = grp.Key, count = grp.Count(), totalHours = grp.Sum(x => (long)(x.game.Playtime / 3600)) })
                .OrderByDescending(x => x.totalHours).ToList();
        }

        private IEnumerable<Game> ApplyListFilter(IEnumerable<Game> games, Dictionary<string, object> filters, string key, Func<Game, IEnumerable<DatabaseObject>> accessor)
        {
            var val = filters.GetValueOrNull(key);
            if (val == null) return games;
            var names = new List<string>();
            if (val is string s) names.Add(s);
            else if (val is ArrayList al) foreach (var item in al) names.Add(item.ToString());
            if (names.Count == 0) return games;
            return games.Where(g =>
            {
                var coll = accessor(g);
                if (coll == null) return false;
                return names.All(n => coll.Any(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)));
            });
        }
    }
}
