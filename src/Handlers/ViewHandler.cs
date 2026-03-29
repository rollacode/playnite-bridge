namespace PlayniteBridge.Handlers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Playnite.SDK;
    using PlayniteBridge.Server;
    using PlayniteBridge.Services;

    internal class ViewHandler
    {
        private readonly IPlayniteAPI _api;
        private readonly GameSerializationService _serializer;

        public ViewHandler(IPlayniteAPI api, GameSerializationService serializer)
        {
            _api = api;
            _serializer = serializer;
        }

        public object GetViewState()
        {
            object result = null;
            _api.MainView.UIDispatcher.Invoke(() =>
            {
                try
                {
                    result = new
                    {
                        selectedCount = _api.MainView.SelectedGames?.Count() ?? 0,
                        activeDesktopView = _api.MainView.ActiveDesktopView.ToString(),
                        sortOrder = _api.MainView.SortOrder.ToString(),
                        grouping = _api.MainView.Grouping.ToString(),
                        activeFilterPreset = _api.MainView.GetActiveFilterPreset().ToString()
                    };
                }
                catch (Exception ex)
                {
                    result = new { error = "View state unavailable: " + ex.Message };
                }
            });
            return result;
        }

        public object GetSelectedGames()
        {
            object result = null;
            _api.MainView.UIDispatcher.Invoke(() =>
            {
                var selected = _api.MainView.SelectedGames?.ToList();
                result = new
                {
                    count = selected?.Count ?? 0,
                    games = selected?.Select(g => _serializer.SerializeCompact(g)).ToList() ?? new List<object>()
                };
            });
            return result;
        }

        public object SelectGames(RequestContext ctx)
        {
            var data = ctx.Body;
            var ids = ((ArrayList)data["gameIds"]).Cast<string>().Select(Guid.Parse).ToList();
            _api.MainView.UIDispatcher.Invoke(() => _api.MainView.SelectGames(ids));
            return new { ok = true, selected = ids.Count };
        }

        public object ApplyFilter(RequestContext ctx)
        {
            var data = ctx.Body;
            var presetId = Guid.Parse(data["presetId"].ToString());
            _api.MainView.UIDispatcher.Invoke(() => _api.MainView.ApplyFilterPreset(presetId));
            return new { ok = true, presetId = presetId.ToString() };
        }
    }
}
