namespace PlayniteBridge.Handlers
{
    using System;
    using Playnite.SDK;
    using PlayniteBridge.Services;

    internal class PluginDataHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly PluginIntegrationService _pluginService;

        public PluginDataHandler(IPlayniteAPI api, PluginIntegrationService pluginService)
        {
            _api = api;
            _pluginService = pluginService;
        }

        public object GetAchievements(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return new { error = "Game not found", code = 404 };
            return _pluginService.GetAchievements(gameId, game.Name);
        }

        public object GetActivity(Guid gameId)
        {
            var game = _api.Database.Games.Get(gameId);
            if (game == null) return new { error = "Game not found", code = 404 };
            return _pluginService.GetActivity(gameId, game.Name);
        }
    }
}
