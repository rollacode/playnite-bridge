using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using PlayniteBridge.Handlers;
using PlayniteBridge.Helpers;
using PlayniteBridge.Server;
using PlayniteBridge.Services;
using PlayniteBridge.Settings;

namespace PlayniteBridge
{
    public class PlayniteBridgePlugin : GenericPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        internal const int HttpPort = 19821;

        private HttpApiServer _server;
        private AuthHandler _auth;
        private AutomationHandler _automation;
        private PluginSettingsViewModel _settingsViewModel;

        // Sync
        private SyncEngine _syncEngine;
        private SyncClient _syncClient;
        private SyncState _syncState;
        private GameSerializationService _serializer;
        private CollectionResolver _resolver;
        private Timer _syncTimer;

        public override Guid Id => Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");

        public PlayniteBridgePlugin(IPlayniteAPI api) : base(api)
        {
            _settingsViewModel = new PluginSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Services
            _resolver = new CollectionResolver();
            _serializer = new GameSerializationService();
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
            var gamesHandler = new GamesHandler(PlayniteApi, _resolver, _serializer);
            var queryHandler = new QueryHandler(PlayniteApi, queryService, _serializer);
            var collectionHandler = new CollectionHandler(PlayniteApi, _resolver);
            var viewHandler = new ViewHandler(PlayniteApi, _serializer);
            var appHandler = new AppHandler(PlayniteApi, HttpPort);
            _automation = new AutomationHandler(PlayniteApi, _resolver, () => GetPluginUserDataPath());
            var pluginDataHandler = new PluginDataHandler(PlayniteApi, pluginService);
            var evalHandler = new EvalHandler(PlayniteApi, this, evalService, jsonHelper, _serializer);

            // Router + Server
            var router = new Router(PlayniteApi, gamesHandler, queryHandler, collectionHandler,
                viewHandler, appHandler, _automation, _auth, pluginDataHandler, evalHandler);
            _server = new HttpApiServer(HttpPort, _auth, router);
            _server.Start();

            Logger.Info($"Playnite Bridge API started on port {HttpPort}");

            if (!_server.IsNetworkBound && !_settingsViewModel.Settings.NetworkPromptDismissed)
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
                else
                {
                    _settingsViewModel.Settings.NetworkPromptDismissed = true;
                    _settingsViewModel.EndEdit();
                }
            }

            // Initialize sync
            InitSync();
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (!_settingsViewModel.Settings.SyncEnabled || _syncEngine == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var result = _syncEngine.PushSingleGame(args.Game);
                    if (result.Success && result.GamesPushed > 0)
                        Logger.Info($"Synced game after stop: {args.Game.Name}");
                }
                catch (Exception ex) { Logger.Warn($"Post-game sync failed: {ex.Message}"); }
            });
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (!_settingsViewModel.Settings.SyncEnabled || _syncEngine == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(5000); // let library update settle
                try
                {
                    var result = _syncEngine.RunSync();
                    if (result.Success)
                        Logger.Info($"Library-update sync: pushed={result.GamesPushed}, pulled={result.GamesPulled}");
                }
                catch (Exception ex) { Logger.Warn($"Library-update sync failed: {ex.Message}"); }
            });
        }

        public override void Dispose()
        {
            _syncTimer?.Dispose();
            _server?.Stop();
            base.Dispose();
        }

        public override ISettings GetSettings(bool firstRunSettings) => _settingsViewModel;

        public override System.Windows.Controls.UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PluginSettingsView(new SettingsViewContext
            {
                ViewModel = _settingsViewModel,
                GetToken = () => _auth?.ApiToken,
                GetIsNetworkBound = () => _server?.IsNetworkBound ?? false,
                GenerateSkillMd = () => GenerateSkillMd(),
                GetPluginDataPath = () => GetPluginUserDataPath(),
                GetLocalIp = () => NetworkHelper.GetLocalIpAddress(),
                EnableNetworkAccess = () => EnableNetworkAccess(),
                RotateToken = () => _auth?.RotateToken(),
                PlayniteApi = PlayniteApi,
                Port = HttpPort,
                // Sync
                GetSyncStatus = () => GetSyncStatusText(),
                GetLastSyncTime = () => _syncState?.LastSyncTime,
                DiscoverBackends = () => BackendDiscovery.DiscoverAll(),
                RequestConnection = (url) => _syncEngine?.RequestConnection(url),
                ApproveWithCode = (code) => ApproveAndStart(code),
                PollApproval = () => PollAndStart(),
                TriggerSync = () => TriggerSyncNow(),
                TriggerFullResync = () => TriggerFullResync(),
            });
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Copy AI Skill to Clipboard",
                MenuSection = "@Playnite Bridge",
                Action = _ =>
                {
                    System.Windows.Clipboard.SetText(GenerateSkillMd());
                    PlayniteApi.Dialogs.ShowMessage(
                        "AI Skill copied to clipboard!\n\nPaste it into your AI chat to give the AI access to your Playnite library.\n\nThe skill contains your API token \u2014 don't share it publicly.",
                        "Playnite Bridge");
                }
            };
            yield return new MainMenuItem
            {
                Description = "Sync Now",
                MenuSection = "@Playnite Bridge",
                Action = _ => TriggerSyncNow()
            };
        }

        #region Sync

        private void InitSync()
        {
            _syncState = SyncState.Load(GetPluginUserDataPath());
            _syncClient = new SyncClient(
                _settingsViewModel.Settings.SyncBackendUrl,
                _syncState.ApiKey ?? "");
            _syncEngine = new SyncEngine(PlayniteApi, _syncClient, _syncState,
                _serializer, _resolver, () => GetPluginUserDataPath());

            // If already registered and enabled, start sync timer
            if (_settingsViewModel.Settings.SyncEnabled && _syncState.IsRegistered)
            {
                StartSyncTimer();
            }
        }

        private void StartSyncTimer()
        {
            _syncTimer?.Dispose();
            var interval = TimeSpan.FromMinutes(
                Math.Max(1, _settingsViewModel.Settings.SyncIntervalMinutes));
            _syncTimer = new Timer(SyncTimerCallback, null, TimeSpan.FromSeconds(10), interval);
            Logger.Info($"Sync timer started (interval: {interval.TotalMinutes}m)");
        }

        private void SyncTimerCallback(object state)
        {
            if (_syncEngine == null || _syncEngine.IsSyncing) return;
            if (_api_has_running_game()) return;

            try
            {
                var result = _syncEngine.RunSync();
                if (result.Success && (result.GamesPushed > 0 || result.GamesPulled > 0))
                    Logger.Info($"Periodic sync: pushed={result.GamesPushed}, pulled={result.GamesPulled}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Periodic sync failed: {ex.Message}");
            }
        }

        private bool _api_has_running_game()
        {
            try { return PlayniteApi.Database.Games.Any(g => g.IsRunning); }
            catch { return false; }
        }

        private string GetSyncStatusText()
        {
            if (_syncEngine?.IsSyncing == true) return "Syncing...";
            if (_syncState == null) return "Not configured";
            if (!string.IsNullOrEmpty(_syncState.LastSyncError)) return $"Error: {_syncState.LastSyncError}";
            if (_syncState.IsRegistered) return "Connected";
            if (!string.IsNullOrEmpty(_syncState.ClientId)) return "Pending approval";
            return "Not connected";
        }

        private bool ApproveAndStart(string code)
        {
            if (_syncEngine == null) return false;
            var success = _syncEngine.ApproveWithCode(code);
            if (success)
            {
                _settingsViewModel.Settings.SyncEnabled = true;
                StartSyncTimer();
            }
            return success;
        }

        private bool PollAndStart()
        {
            if (_syncEngine == null) return false;
            var approved = _syncEngine.PollApproval();
            if (approved)
            {
                _settingsViewModel.Settings.SyncEnabled = true;
                StartSyncTimer();
            }
            return approved;
        }

        private void TriggerSyncNow()
        {
            if (_syncEngine == null || !_syncState.IsRegistered) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = _syncEngine.RunSync();
                PlayniteApi.Notifications.Add(new NotificationMessage("sync",
                    result.Success
                        ? $"Sync complete: {result.GamesPushed} pushed, {result.GamesPulled} pulled"
                        : $"Sync failed: {result.Error}",
                    result.Success ? NotificationType.Info : NotificationType.Error));
            });
        }

        private void TriggerFullResync()
        {
            if (_syncEngine == null || !_syncState.IsRegistered) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var result = _syncEngine.RunFullResync();
                PlayniteApi.Notifications.Add(new NotificationMessage("sync",
                    result.Success
                        ? $"Full resync: {result.GamesPushed} pushed, {result.GamesPulled} pulled"
                        : $"Resync failed: {result.Error}",
                    result.Success ? NotificationType.Info : NotificationType.Error));
            });
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
                    Arguments = $"/c netsh http add urlacl url=http://+:{HttpPort}/ sddl=D:(A;;GX;;;S-1-1-0) && netsh advfirewall firewall add rule name=\"Playnite Bridge\" dir=in action=allow protocol=tcp localport={HttpPort}",
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
