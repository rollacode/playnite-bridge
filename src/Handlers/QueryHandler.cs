namespace PlayniteBridge.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Playnite.SDK;
    using Playnite.SDK.Models;
    using PlayniteBridge.Helpers;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    internal class QueryHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly GameQueryService _queryService;
        private readonly GameSerializationService _serializer;

        public QueryHandler(IPlayniteAPI api, GameQueryService queryService, GameSerializationService serializer)
        {
            _api = api;
            _queryService = queryService;
            _serializer = serializer;
        }

        public object QueryGames(RequestContext ctx)
        {
            var body = ctx.Body;
            IEnumerable<Game> games = _api.Database.Games;

            // Apply filters via service
            games = _queryService.Filter(games, body);

            // Sorting
            var sort = (body.GetValueOrNull("sort") as string ?? "name");
            var desc = body.ContainsKey("descending") && body["descending"] is bool d && d;
            var ordered = _queryService.Sort(games, sort, desc);

            // GroupBy support
            var groupBy = body.GetValueOrNull("groupBy") as string;
            if (!string.IsNullOrEmpty(groupBy))
            {
                var grouped = _queryService.GroupBy(ordered, groupBy);
                if (grouped == null)
                    return new { error = "Invalid groupBy. Use: genre, developer, publisher, source, platform, year, completionStatus", code = 400 };
                return grouped;
            }

            // Pagination
            int offset = 0, limit = 500;
            if (body.ContainsKey("offset")) offset = Math.Max(0, Convert.ToInt32(body["offset"]));
            if (body.ContainsKey("limit")) limit = Math.Min(Math.Max(Convert.ToInt32(body["limit"]), 1), 5000);

            var total = 0;
            try { total = ordered.Count(); } catch { }
            var page = ordered.Skip(offset).Take(limit);

            return new { total, offset, limit, games = page.Select(g => _serializer.SerializeCompact(g)).ToList() };
        }
    }
}
