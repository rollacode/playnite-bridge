namespace PlayniteBridge.Settings
{
    using System.Collections.Generic;
    using Playnite.SDK;
    using Playnite.SDK.Data;

    public class PluginSettings : ObservableObject
    {
        // Settings are saved/loaded automatically by Playnite.
        // Currently no persistent settings — the panel is used for
        // status display and quick actions (token copy, AI skill, etc.).
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
