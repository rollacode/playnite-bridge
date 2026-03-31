namespace PlayniteBridge.Settings
{
    using System;
    using System.Collections.Generic;
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

        // Sync
        private readonly Func<string> _getSyncStatus;
        private readonly Func<string> _getLastSyncTime;
        private readonly Func<List<PlayniteBridge.Services.DiscoveredBackend>> _discoverBackends;
        private readonly Func<string, string> _requestConnection;
        private readonly Func<string, bool> _approveWithCode;
        private readonly Func<bool> _pollApproval;
        private readonly Action _triggerSync;
        private readonly Action _triggerFullResync;
        private readonly Action _disconnect;
        private DispatcherTimer _pollTimer;

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

            _getSyncStatus = ctx.GetSyncStatus;
            _getLastSyncTime = ctx.GetLastSyncTime;
            _discoverBackends = ctx.DiscoverBackends;
            _requestConnection = ctx.RequestConnection;
            _approveWithCode = ctx.ApproveWithCode;
            _pollApproval = ctx.PollApproval;
            _triggerSync = ctx.TriggerSync;
            _triggerFullResync = ctx.TriggerFullResync;
            _disconnect = ctx.Disconnect;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
            RefreshSyncStatus();
            RefreshFSEStatus();
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

        // --- FSE ---

        private void BtnInstallFSE_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Find MSIX and cert in plugin directory
                var pluginDir = System.IO.Path.GetDirectoryName(
                    typeof(PluginSettingsView).Assembly.Location);
                var fseDir = System.IO.Path.Combine(pluginDir, "FSE");
                var msix = System.IO.Path.Combine(fseDir, "PlayniteFSE.msix");
                var cer = System.IO.Path.Combine(fseDir, "PlayniteFSE.cer");

                if (!System.IO.File.Exists(msix))
                {
                    _playniteApi.Dialogs.ShowMessage(
                        "FSE package not found. Make sure PlayniteFSE.msix is in the plugin's FSE folder.",
                        "Playnite Bridge");
                    return;
                }

                var script = $"Import-Certificate -FilePath '{cer}' -CertStoreLocation 'Cert:\\LocalMachine\\TrustedPeople'; " +
                             $"Add-AppxPackage -Path '{msix}'; " +
                             "if ($?) { Write-Host 'SUCCESS' } else { Write-Host 'FAILED' }; " +
                             "Start-Sleep 2";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-Command \"Start-Process powershell -Verb RunAs -ArgumentList '-Command {script.Replace("'", "''")}'\"",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, ev) =>
                {
                    RefreshFSEStatus();
                    timer.Stop();
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                _playniteApi.Dialogs.ShowMessage($"Error: {ex.Message}", "Playnite Bridge");
            }
        }

        private void BtnUninstallFSE_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"Get-AppxPackage -Name Rollacode.PlayniteBridgeFSE | Remove-AppxPackage\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi)?.WaitForExit(10000);
                FlashButton(BtnUninstallFSE, "Removed!");
                RefreshFSEStatus();
            }
            catch (Exception ex)
            {
                _playniteApi.Dialogs.ShowMessage($"Error: {ex.Message}", "Playnite Bridge");
            }
        }

        private void RefreshFSEStatus()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command \"if (Get-AppxPackage -Name Rollacode.PlayniteBridgeFSE) { Write-Output 'yes' } else { Write-Output 'no' }\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(5000);

                if (output == "yes")
                {
                    FSEStatus.Text = "Installed";
                    FSEStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                }
                else
                {
                    FSEStatus.Text = "Not installed";
                    FSEStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
                }
            }
            catch { FSEStatus.Text = ""; }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
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

        // --- Sync ---

        private void RefreshSyncStatus()
        {
            var status = _getSyncStatus?.Invoke() ?? "Not configured";
            SyncStatusText.Text = status;

            var isConnected = status == "Connected";
            var isPending = status == "Pending approval";
            SyncStatusText.Foreground = isConnected
                ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
                : isPending
                    ? new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07))
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));

            var lastSync = _getLastSyncTime?.Invoke();
            SyncLastTimeText.Text = string.IsNullOrEmpty(lastSync) ? "Never" : lastSync;

            // Show correct panel
            PanelDiscovery.Visibility = (!isConnected && !isPending) ? Visibility.Visible : Visibility.Collapsed;
            PanelPending.Visibility = isPending ? Visibility.Visible : Visibility.Collapsed;
            PanelConnected.Visibility = isConnected ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnSyncNow_Click(object sender, RoutedEventArgs e)
        {
            _triggerSync?.Invoke();
            FlashButton(BtnSyncNow, "Syncing...");

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, ev) =>
            {
                RefreshSyncStatus();
                timer.Stop();
            };
            timer.Start();
        }

        private void BtnDetectBackend_Click(object sender, RoutedEventArgs e)
        {
            BtnDetectBackend.IsEnabled = false;
            ScanSpinner.Visibility = Visibility.Visible;
            DiscoveredList.Items.Clear();

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var backends = _discoverBackends?.Invoke() ?? new List<Services.DiscoveredBackend>();
                Dispatcher.Invoke(() =>
                {
                    BtnDetectBackend.IsEnabled = true;
                    ScanSpinner.Visibility = Visibility.Collapsed;

                    if (backends.Count == 0)
                    {
                        DiscoveredList.Items.Add(new TextBlock
                        {
                            Text = "No backends found on network",
                            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
                            Margin = new Thickness(0, 0, 0, 4)
                        });
                    }
                    else
                    {
                        foreach (var b in backends)
                        {
                            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                            var via = b.ViaTailscale ? "Tailscale" : "LAN";
                            var label = $"{b.Hostname} ({via}) — {b.Url}";
                            panel.Children.Add(new TextBlock
                            {
                                Text = label,
                                VerticalAlignment = VerticalAlignment.Center,
                                MinWidth = 300,
                                Foreground = (Brush)FindResource("TextBrush")
                            });
                            var btn = new Button { Content = "Connect", Padding = new Thickness(12, 5, 12, 5) };
                            var url = b.Url;
                            btn.Click += (s2, ev2) => StartConnection(url);
                            panel.Children.Add(btn);
                            DiscoveredList.Items.Add(panel);
                        }
                    }
                });
            });
        }

        private void BtnConnectUrl_Click(object sender, RoutedEventArgs e)
        {
            var url = SyncUrlBox.Text?.Trim();
            if (!string.IsNullOrEmpty(url)) StartConnection(url);
        }

        private void StartConnection(string url)
        {
            var clientId = _requestConnection?.Invoke(url);
            if (string.IsNullOrEmpty(clientId))
            {
                _playniteApi.Dialogs.ShowMessage("Failed to contact backend.", "Playnite Bridge");
                return;
            }

            // Switch to pending state
            PanelDiscovery.Visibility = Visibility.Collapsed;
            PanelPending.Visibility = Visibility.Visible;
            RefreshSyncStatus();

            // Start polling for approval
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _pollTimer.Tick += (s, ev) =>
            {
                var approved = _pollApproval?.Invoke() ?? false;
                if (approved)
                {
                    _pollTimer.Stop();
                    OnConnected();
                }
            };
            _pollTimer.Start();
        }

        private void BtnApproveCode_Click(object sender, RoutedEventArgs e)
        {
            var code = SyncCodeBox.Text?.Trim();
            if (string.IsNullOrEmpty(code)) return;

            var success = _approveWithCode?.Invoke(code) ?? false;
            if (success)
            {
                _pollTimer?.Stop();
                OnConnected();
            }
            else
            {
                _playniteApi.Dialogs.ShowMessage("Invalid code. Check the backend dashboard for the PSR code.", "Playnite Bridge");
            }
        }

        private void BtnCancelPending_Click(object sender, RoutedEventArgs e)
        {
            _pollTimer?.Stop();
            PanelPending.Visibility = Visibility.Collapsed;
            PanelDiscovery.Visibility = Visibility.Visible;
            RefreshSyncStatus();
        }

        private void OnConnected()
        {
            PanelPending.Visibility = Visibility.Collapsed;
            PanelConnected.Visibility = Visibility.Visible;
            SyncStatusText.Text = "Connected — syncing...";
            SyncStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));

            // Trigger first sync immediately
            _triggerSync?.Invoke();

            // Refresh status after a few seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, ev) =>
            {
                RefreshSyncStatus();
                timer.Stop();
            };
            timer.Start();
        }

        private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var result = _playniteApi.Dialogs.ShowMessage(
                "Disconnect from sync backend?\n\nYour local library won't be affected.",
                "Playnite Bridge", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                _disconnect?.Invoke();
                _pollTimer?.Stop();
                RefreshSyncStatus();
            }
        }

        private void BtnFullResync_Click(object sender, RoutedEventArgs e)
        {
            var result = _playniteApi.Dialogs.ShowMessage(
                "Full resync will re-push all games and re-pull from the server.\n\nContinue?",
                "Playnite Bridge", MessageBoxButton.YesNo);

            if (result == MessageBoxResult.Yes)
            {
                _triggerFullResync?.Invoke();
                FlashButton(BtnFullResync, "Resyncing...");
            }
        }
    }
}
