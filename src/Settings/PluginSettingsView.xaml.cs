namespace PlayniteBridge.Settings
{
    using System;
    using System.IO;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;
    using Playnite.SDK;

    public partial class PluginSettingsView : UserControl
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        private readonly Func<string> _getToken;
        private readonly Func<bool> _getIsNetworkBound;
        private readonly Func<string> _generateSkillMd;
        private readonly Func<string> _getPluginDataPath;
        private readonly Func<string> _getLocalIp;
        private readonly Action _enableNetworkAccess;
        private readonly Action _rotateToken;
        private readonly IPlayniteAPI _playniteApi;
        private readonly int _port;

        public PluginSettingsView(SettingsViewContext ctx)
        {
            InitializeComponent();
            DataContext = ctx.ViewModel;

            _getToken = ctx.GetToken;
            _getIsNetworkBound = ctx.GetIsNetworkBound;
            _generateSkillMd = ctx.GenerateSkillMd;
            _getPluginDataPath = ctx.GetPluginDataPath;
            _getLocalIp = ctx.GetLocalIp;
            _enableNetworkAccess = ctx.EnableNetworkAccess;
            _rotateToken = ctx.RotateToken;
            _playniteApi = ctx.PlayniteApi;
            _port = ctx.Port;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            bool networkBound = _getIsNetworkBound();

            StatusText.Text = $"Running on port {_port}";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

            NetworkText.Text = networkBound ? "All interfaces (network accessible)" : "Localhost only";
            NetworkText.Foreground = networkBound
                ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

            BtnEnableNetwork.IsEnabled = !networkBound;
            BtnEnableNetwork.Content = networkBound ? "Network Access Enabled" : "Enable Network Access";

            // Token
            TokenBox.Text = _getToken() ?? "(unavailable)";

            // Host for URLs
            string host = networkBound ? _getLocalIp() : "localhost";
            string baseUrl = $"http://{host}:{_port}";

            // Claude Desktop MCP config
            ClaudeConfigBox.Text =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"playnite\": {\n" +
                $"      \"url\": \"{baseUrl}/mcp\",\n" +
                "      \"headers\": {{\n" +
                $"        \"Authorization\": \"Bearer {_getToken()}\"\n" +
                "      }}\n" +
                "    }\n" +
                "  }\n" +
                "}";

            // ChatGPT URL
            ChatGptUrlBox.Text = $"{baseUrl}/api";
        }

        private void FlashButton(Button btn, string flashText)
        {
            var original = btn.Content?.ToString();
            btn.Content = flashText;
            btn.IsEnabled = false;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, ev) =>
            {
                btn.Content = original;
                btn.IsEnabled = true;
                timer.Stop();
            };
            timer.Start();
        }

        private void BtnCopyToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_getToken());
                FlashButton(BtnCopyToken, "Copied!");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to copy token");
            }
        }

        private void BtnRegenerateToken_Click(object sender, RoutedEventArgs e)
        {
            var result = _playniteApi.Dialogs.ShowMessage(
                "Regenerate API token?\n\nThe old token stops working immediately. " +
                "You will need to update it in any AI tools using the API.",
                "Playnite Bridge",
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                _rotateToken();
                RefreshStatus();
                FlashButton(BtnRegenerateToken, "Regenerated!");
            }
        }

        private void BtnEnableNetwork_Click(object sender, RoutedEventArgs e)
        {
            var result = _playniteApi.Dialogs.ShowMessage(
                "Allow API access from other devices on your network?\n\n" +
                "This will:\n" +
                "\u2022 Register HTTP port 19821\n" +
                "\u2022 Add a Windows Firewall rule\n\n" +
                "Windows will ask for administrator permission.",
                "Playnite Bridge",
                MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                _enableNetworkAccess();
                RefreshStatus();
            }
        }

        private void BtnCopySkill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_generateSkillMd());
                FlashButton(BtnCopySkill, "Copied!");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to copy skill");
            }
        }

        private void BtnOpenSkillFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var skillPath = Path.Combine(_getPluginDataPath(), "skill.md");
                File.WriteAllText(skillPath, _generateSkillMd(), Encoding.UTF8);
                System.Diagnostics.Process.Start(skillPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open skill file");
            }
        }

        private void BtnCopyClaudeConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ClaudeConfigBox.Text);
                FlashButton(BtnCopyClaudeConfig, "Copied!");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to copy config");
            }
        }

        private void BtnCopyChatGptUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ChatGptUrlBox.Text);
                FlashButton(BtnCopyChatGptUrl, "Copied!");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to copy URL");
            }
        }
    }
}
