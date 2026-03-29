namespace PlayniteBridge.Server
{
    using System;
    using Playnite.SDK;
    using PlayniteBridge.Handlers;

    internal class Router
    {
        private readonly IPlayniteAPI _api;
        private readonly GamesHandler _games;
        private readonly QueryHandler _query;
        private readonly CollectionHandler _collections;
        private readonly ViewHandler _view;
        private readonly AppHandler _app;
        private readonly AutomationHandler _automation;
        private readonly AuthHandler _auth;
        private readonly PluginDataHandler _pluginData;
        private readonly EvalHandler _eval;

        public Router(
            IPlayniteAPI api,
            GamesHandler games,
            QueryHandler query,
            CollectionHandler collections,
            ViewHandler view,
            AppHandler app,
            AutomationHandler automation,
            AuthHandler auth,
            PluginDataHandler pluginData,
            EvalHandler eval)
        {
            _api = api;
            _games = games;
            _query = query;
            _collections = collections;
            _view = view;
            _app = app;
            _automation = automation;
            _auth = auth;
            _pluginData = pluginData;
            _eval = eval;
        }

        public object Route(RequestContext ctx)
        {
            var path = ctx.Path;
            var method = ctx.Method;

            // API index
            if (path == "/api" && method == "GET") return _app.GetApiIndex();

            // Auth
            if (path == "/api/auth/rotate" && method == "POST") return _auth.HandleRotateToken();
            if (path == "/api/skill.md" && method == "GET") return _auth.HandleGetSkillMd();

            // Games - collection
            if (path == "/api/games" && method == "GET") return _games.GetGames(ctx);
            if (path == "/api/games/missing-art" && method == "GET") return _games.GetMissingArt();
            if (path == "/api/games/query" && method == "POST") return _query.QueryGames(ctx);
            if (path == "/api/games" && method == "POST") return _games.CreateGame(ctx);

            // Games - single resource
            if (path.StartsWith("/api/games/"))
            {
                var rest = path.Substring("/api/games/".Length);
                var segments = rest.Split('/');

                if (segments.Length >= 1 && Guid.TryParse(segments[0], out var gameId))
                {
                    if (segments.Length == 1)
                    {
                        if (method == "GET") return _games.GetGame(gameId);
                        if (method == "PUT") return _games.UpdateGame(gameId, ctx);
                        if (method == "DELETE") return _games.DeleteGame(gameId);
                    }
                    else if (segments.Length == 2)
                    {
                        var action = segments[1];
                        if (action == "launch" && method == "POST") return _games.LaunchGame(gameId);
                        if (action == "install" && method == "POST") return _games.InstallGame(gameId);
                        if (action == "uninstall" && method == "POST") return _games.UninstallGame(gameId);
                        if (action == "categories" && method == "PUT") return _games.SetGameList(gameId, ctx, "categories");
                        if (action == "categories" && method == "POST") return _games.AddGameList(gameId, ctx, "categories");
                        if (action == "tags" && method == "PUT") return _games.SetGameList(gameId, ctx, "tags");
                        if (action == "tags" && method == "POST") return _games.AddGameList(gameId, ctx, "tags");
                        if (action == "features" && method == "PUT") return _games.SetGameList(gameId, ctx, "features");
                        if (action == "features" && method == "POST") return _games.AddGameList(gameId, ctx, "features");
                        if (action == "genres" && method == "PUT") return _games.SetGameList(gameId, ctx, "genres");
                        if (action == "genres" && method == "POST") return _games.AddGameList(gameId, ctx, "genres");
                        if (action == "status" && method == "PUT") return _games.SetCompletionStatus(gameId, ctx);
                        if (action == "fetch-art" && method == "POST") return _automation.FetchArt(gameId);
                        if (action == "achievements" && method == "GET") return _pluginData.GetAchievements(gameId);
                        if (action == "activity" && method == "GET") return _pluginData.GetActivity(gameId);
                        if (action == "cover" && method == "GET") return _games.GetCover(gameId, ctx.QueryString?["type"] ?? "cover");
                        if (action == "delete" && method == "POST") return _games.DeleteGame(gameId); // compat
                    }
                }
            }

            // Database collections — DELETE by id
            if (path.StartsWith("/api/categories/") && method == "DELETE")
                return _collections.DeleteCollectionItem(_api.Database.Categories, path.Substring("/api/categories/".Length));
            if (path.StartsWith("/api/tags/") && method == "DELETE")
                return _collections.DeleteCollectionItem(_api.Database.Tags, path.Substring("/api/tags/".Length));
            if (path.StartsWith("/api/genres/") && method == "DELETE")
                return _collections.DeleteCollectionItem(_api.Database.Genres, path.Substring("/api/genres/".Length));
            if (path.StartsWith("/api/features/") && method == "DELETE")
                return _collections.DeleteCollectionItem(_api.Database.Features, path.Substring("/api/features/".Length));
            if (path.StartsWith("/api/series/") && method == "DELETE")
                return _collections.DeleteCollectionItem(_api.Database.Series, path.Substring("/api/series/".Length));

            // Database collections — GET/POST
            if (path == "/api/categories" && method == "GET") return _collections.GetCollection(_api.Database.Categories);
            if (path == "/api/categories" && method == "POST") return _collections.CreateItem(_api.Database.Categories, ctx);
            if (path == "/api/genres" && method == "GET") return _collections.GetCollection(_api.Database.Genres);
            if (path == "/api/genres" && method == "POST") return _collections.CreateItem(_api.Database.Genres, ctx);
            if (path == "/api/tags" && method == "GET") return _collections.GetCollection(_api.Database.Tags);
            if (path == "/api/tags" && method == "POST") return _collections.CreateItem(_api.Database.Tags, ctx);
            if (path == "/api/features" && method == "GET") return _collections.GetCollection(_api.Database.Features);
            if (path == "/api/features" && method == "POST") return _collections.CreateItem(_api.Database.Features, ctx);
            if (path == "/api/platforms" && method == "GET") return _collections.GetCollection(_api.Database.Platforms);
            if (path == "/api/sources" && method == "GET") return _collections.GetCollection(_api.Database.Sources);
            if (path == "/api/companies" && method == "GET") return _collections.GetCollection(_api.Database.Companies);
            if (path == "/api/series" && method == "GET") return _collections.GetCollection(_api.Database.Series);
            if (path == "/api/series" && method == "POST") return _collections.CreateItem(_api.Database.Series, ctx);
            if (path == "/api/age-ratings" && method == "GET") return _collections.GetCollection(_api.Database.AgeRatings);
            if (path == "/api/regions" && method == "GET") return _collections.GetCollection(_api.Database.Regions);
            if (path == "/api/completion-statuses" && method == "GET") return _collections.GetCollection(_api.Database.CompletionStatuses);
            if (path == "/api/completion-statuses" && method == "POST") return _collections.CreateItem(_api.Database.CompletionStatuses, ctx);
            if (path == "/api/filter-presets" && method == "GET") return _collections.GetCollection(_api.Database.FilterPresets);
            if (path == "/api/emulators" && method == "GET") return _collections.GetEmulators();

            // Automation
            if (path == "/api/auto-categorize" && method == "POST") return _automation.AutoCategorize();
            if (path == "/api/fetch-all-art" && method == "POST") return _automation.FetchAllArt();
            if (path == "/api/categorize" && method == "POST") return _automation.Categorize(ctx); // compat

            // View
            if (path == "/api/view/state" && method == "GET") return _view.GetViewState();
            if (path == "/api/view/selected" && method == "GET") return _view.GetSelectedGames();
            if (path == "/api/view/select" && method == "POST") return _view.SelectGames(ctx);
            if (path == "/api/view/filter" && method == "POST") return _view.ApplyFilter(ctx);

            // App
            if (path == "/api/app/info" && method == "GET") return _app.GetAppInfo();
            if (path == "/api/app/addons" && method == "GET") return _app.GetAddons();
            if (path == "/api/stats" && method == "GET") return _app.GetStats();
            if (path == "/api/notifications" && method == "POST") return _app.SendNotification(ctx);

            // Plugins
            if (path == "/api/plugins" && method == "GET") return _app.GetPlugins();

            // Eval
            if (path == "/api/eval" && method == "POST") return _eval.HandleEval(ctx);

            return null;
        }
    }
}
