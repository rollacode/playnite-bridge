using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PlayniteBridge.Handlers;
using PlayniteBridge.Helpers;
using PlayniteBridge.Server;
using PlayniteBridge.Services;

namespace PlayniteBridge
{
    public class PlayniteBridgePlugin : GenericPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        internal const int HttpPort = 19821;

        private HttpApiServer _server;
        private AuthHandler _auth;
        private AutomationHandler _automation;

        public override Guid Id => Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");

        public PlayniteBridgePlugin(IPlayniteAPI api) : base(api) { }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Services
            var resolver = new CollectionResolver();
            var serializer = new GameSerializationService();
            var queryService = new GameQueryService();
            var evalService = new EvalService();
            var jsonHelper = new JsonHelper();
            var pluginService = new PluginIntegrationService(PlayniteApi.Paths.ExtensionsDataPath);

            // Auth
            _auth = new AuthHandler(
                () => GetPluginUserDataPath(),
                () => _server?.IsNetworkBound ?? false,
                () => GenerateSkillMd());
            _auth.LoadOrCreateToken();

            // Handlers
            var gamesHandler = new GamesHandler(PlayniteApi, resolver, serializer);
            var queryHandler = new QueryHandler(PlayniteApi, queryService, serializer);
            var collectionHandler = new CollectionHandler(PlayniteApi, resolver);
            var viewHandler = new ViewHandler(PlayniteApi, serializer);
            var appHandler = new AppHandler(PlayniteApi, HttpPort);
            _automation = new AutomationHandler(PlayniteApi, resolver, () => GetPluginUserDataPath());
            var pluginDataHandler = new PluginDataHandler(PlayniteApi, pluginService);
            var evalHandler = new EvalHandler(PlayniteApi, this, evalService, jsonHelper, serializer);

            // Router + Server
            var router = new Router(PlayniteApi, gamesHandler, queryHandler, collectionHandler,
                viewHandler, appHandler, _automation, _auth, pluginDataHandler, evalHandler);
            _server = new HttpApiServer(HttpPort, _auth, router);
            _server.Start();

            Logger.Info($"Playnite Bridge API started on port {HttpPort}");

            if (!_server.IsNetworkBound)
            {
                var result = PlayniteApi.Dialogs.ShowMessage(
                    "Playnite Bridge is running on localhost only.\n\n" +
                    "Enable network access so other devices can connect to the API?\n" +
                    "This will register port 19821 and add a firewall rule.\n\n" +
                    "Windows will ask for administrator permission.",
                    "Playnite Bridge \u2014 Network Access",
                    System.Windows.MessageBoxButton.YesNo);

                if (result == System.Windows.MessageBoxResult.Yes)
                    EnableNetworkAccess();
            }
        }

        public override void Dispose()
        {
            _server?.Stop();
            base.Dispose();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return MenuItem("Copy AI Skill to Clipboard", _ =>
            {
                System.Windows.Clipboard.SetText(GenerateSkillMd());
                PlayniteApi.Dialogs.ShowMessage(
                    "AI Skill copied to clipboard!\n\nPaste it into your AI chat to give the AI access to your Playnite library.\n\nThe skill contains your API token \u2014 don't share it publicly.",
                    "Playnite Bridge");
            });

            yield return MenuItem("Open AI Skill File", _ =>
            {
                var skillPath = Path.Combine(GetPluginUserDataPath(), "skill.md");
                File.WriteAllText(skillPath, GenerateSkillMd(), Encoding.UTF8);
                System.Diagnostics.Process.Start(skillPath);
            });

            yield return MenuItem("Show API Token", _ =>
                PlayniteApi.Dialogs.ShowMessage($"API Token:\n\n{_auth.ApiToken}\n\nUse 'Copy AI Skill' for the full skill file.", "Playnite Bridge"));

            yield return MenuItem("Regenerate API Token", _ =>
            {
                var r = PlayniteApi.Dialogs.ShowMessage("Regenerate API token?\n\nThe old token stops working immediately.", "Playnite Bridge", System.Windows.MessageBoxButton.YesNo);
                if (r == System.Windows.MessageBoxResult.Yes)
                {
                    _auth.RotateToken();
                    PlayniteApi.Dialogs.ShowMessage($"New token:\n\n{_auth.ApiToken}", "Playnite Bridge");
                }
            });

            yield return MenuItem(_server?.IsNetworkBound == true ? "Network Access \u2713 (already enabled)" : "Enable Network Access", _ =>
            {
                if (_server?.IsNetworkBound == true)
                {
                    PlayniteApi.Dialogs.ShowMessage("Network access is already enabled.\n\nAPI is accessible from other devices at:\nhttp://<this-pc-ip>:" + HttpPort, "Playnite Bridge");
                    return;
                }
                var r = PlayniteApi.Dialogs.ShowMessage("Allow API access from other devices on your network?\n\nThis will:\n\u2022 Register HTTP port 19821\n\u2022 Add a Windows Firewall rule\n\nWindows will ask for administrator permission.", "Playnite Bridge", System.Windows.MessageBoxButton.YesNo);
                if (r == System.Windows.MessageBoxResult.Yes)
                    EnableNetworkAccess();
            });

            yield return MenuItem("Auto-Categorize All Games (by Genre)", _ => AutoCategorizeByGenre());
            yield return MenuItem("Auto-Categorize Selected Games", _ => AutoCategorizeSelected());
            yield return MenuItem("Show Uncategorized Games", _ => ShowUncategorized());
        }

        #region Menu Helpers

        private MainMenuItem MenuItem(string desc, Action<MainMenuItemActionArgs> action)
        {
            return new MainMenuItem { Description = desc, MenuSection = "@Playnite Bridge", Action = action };
        }

        private void AutoCategorizeByGenre()
        {
            var uncategorized = PlayniteApi.Database.Games.Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0).ToList();
            if (uncategorized.Count == 0) { PlayniteApi.Dialogs.ShowMessage("All games are already categorized!", "Playnite Bridge"); return; }
            var r = PlayniteApi.Dialogs.ShowMessage($"Found {uncategorized.Count} uncategorized games. Categorize by primary genre?", "Playnite Bridge", System.Windows.MessageBoxButton.YesNo);
            if (r != System.Windows.MessageBoxResult.Yes) return;
            var count = _automation.CategorizeGames(uncategorized);
            PlayniteApi.Dialogs.ShowMessage($"Categorized {count} games.", "Playnite Bridge");
        }

        private void AutoCategorizeSelected()
        {
            var selected = PlayniteApi.MainView.SelectedGames?.ToList();
            if (selected == null || selected.Count == 0) { PlayniteApi.Dialogs.ShowMessage("No games selected.", "Playnite Bridge"); return; }
            var count = _automation.CategorizeGames(selected);
            PlayniteApi.Dialogs.ShowMessage($"Categorized {count} out of {selected.Count} games.", "Playnite Bridge");
        }

        private void ShowUncategorized()
        {
            var uncategorized = PlayniteApi.Database.Games.Where(g => g.CategoryIds == null || g.CategoryIds.Count == 0).Select(g => g.Name).OrderBy(n => n).ToList();
            PlayniteApi.Dialogs.ShowMessage(
                uncategorized.Count == 0 ? "All games are categorized!"
                : $"Uncategorized ({uncategorized.Count}):\n\n{string.Join("\n", uncategorized.Take(50))}" + (uncategorized.Count > 50 ? $"\n... and {uncategorized.Count - 50} more" : ""),
                "Playnite Bridge");
        }

        #endregion

        #region Network

        private void EnableNetworkAccess()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netsh http add urlacl url=http://+:{HttpPort}/ user=Everyone && netsh advfirewall firewall add rule name=\"Playnite Bridge\" dir=in action=allow protocol=tcp localport={HttpPort}",
                    Verb = "runas", UseShellExecute = true, CreateNoWindow = false
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(10000);
                _server.Restart();
                PlayniteApi.Dialogs.ShowMessage(
                    _server.IsNetworkBound ? "Network access enabled! API is now accessible from other devices.\n\nOther devices can reach it at:\nhttp://<this-pc-ip>:" + HttpPort : "Failed to bind to network interface. Check the logs.",
                    "Playnite Bridge");
            }
            catch (System.ComponentModel.Win32Exception)
            {
                PlayniteApi.Dialogs.ShowMessage("Cancelled \u2014 administrator permission is required.", "Playnite Bridge");
            }
            catch (Exception ex)
            {
                PlayniteApi.Dialogs.ShowMessage($"Error: {ex.Message}", "Playnite Bridge");
            }
        }

        #endregion

        #region Skill Template

        private string GenerateSkillMd()
        {
            string template;
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("skill.md"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                template = reader.ReadToEnd();

            return template
                .Replace("%%HOST%%", _server?.IsNetworkBound == true ? NetworkHelper.GetLocalIpAddress() : "localhost")
                .Replace("%%PORT%%", HttpPort.ToString())
                .Replace("%%TOKEN%%", _auth.ApiToken);
        }

        #endregion
    }
}
