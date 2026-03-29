namespace PlayniteBridge.Settings
{
    using System;
    using Playnite.SDK;

    /// <summary>
    /// Plain data bag passed to the settings view.
    /// Keeps the XAML code-behind free of references to internal server/handler types,
    /// which avoids assembly-resolution issues during XAML compilation.
    /// </summary>
    public class SettingsViewContext
    {
        public PluginSettingsViewModel ViewModel { get; set; }
        public Func<string> GetToken { get; set; }
        public Func<bool> GetIsNetworkBound { get; set; }
        public Func<string> GenerateSkillMd { get; set; }
        public Func<string> GetPluginDataPath { get; set; }
        public Func<string> GetLocalIp { get; set; }
        public Action EnableNetworkAccess { get; set; }
        public Action RotateToken { get; set; }
        public IPlayniteAPI PlayniteApi { get; set; }
        public int Port { get; set; }
    }
}
