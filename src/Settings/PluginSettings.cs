namespace PlayniteBridge.Settings
{
    using System.Collections.Generic;
    using Playnite.SDK;
    using Playnite.SDK.Data;

    public class PluginSettings : ObservableObject
    {
        private bool _syncEnabled;
        private string _syncBackendUrl = "";
        private int _syncIntervalMinutes = 15;

        public bool SyncEnabled
        {
            get => _syncEnabled;
            set => SetValue(ref _syncEnabled, value);
        }

        public string SyncBackendUrl
        {
            get => _syncBackendUrl;
            set => SetValue(ref _syncBackendUrl, value);
        }

        public int SyncIntervalMinutes
        {
            get => _syncIntervalMinutes;
            set => SetValue(ref _syncIntervalMinutes, value);
        }
    }

    public class PluginSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteBridgePlugin _plugin;
        private PluginSettings _settings;
        private PluginSettings _editingClone;

        public PluginSettings Settings
        {
            get => _settings;
            set => SetValue(ref _settings, value);
        }

        public PluginSettingsViewModel(PlayniteBridgePlugin plugin)
        {
            _plugin = plugin;
            var saved = plugin.LoadPluginSettings<PluginSettings>();
            Settings = saved ?? new PluginSettings();
        }

        public void BeginEdit()
        {
            _editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = _editingClone;
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
