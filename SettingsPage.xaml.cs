using System;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using MoboxFrpGUI.Services;
using System.Diagnostics;
using WpfClipboard = System.Windows.Clipboard;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MoboxFrpGUI
{
    public partial class SettingsPage : Page
    {
        private bool _isInitialized = false;

        public SettingsPage()
        {
            InitializeComponent();
            LoadCurrentVersion();
            LoadCurrentTheme();
            LoadSettingsState();
            LoadLastException();
            _isInitialized = true;
        }

        private void LoadCurrentVersion()
        {
            string version = UpdateService.GetCurrentVersion();
            VersionText.Text = $"版本 {version}";
        }

        private void LoadLastException()
        {
            RefreshErrorDetail();
        }

        public void RefreshErrorDetail()
        {
            if (!string.IsNullOrEmpty(App.LastExceptionReport))
            {
                ErrorDetailTextBox.Text = App.LastExceptionReport;
                ErrorDetailPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorDetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        // 同步设置里面所有的开关状态
        private void LoadSettingsState()
        {
            var config = ConfigService.LoadConfig();
            if (config != null)
            {
                ToggleAutoLogin.IsOn = config.AutoLogin;
                ToggleAutoCheckUpdate.IsOn = config.AutoCheckUpdate;
                
                // 加载主题设置
                if (!string.IsNullOrEmpty(config.Theme))
                {
                    switch (config.Theme)
                    {
                        case "Light":
                            ThemeComboBox.SelectedIndex = 1;
                            break;
                        case "Dark":
                            ThemeComboBox.SelectedIndex = 2;
                            break;
                        default:
                            ThemeComboBox.SelectedIndex = 0;
                            break;
                    }
                }
            }
            else
            {
                ToggleAutoLogin.IsOn = false;
                ToggleAutoCheckUpdate.IsOn = true;
                ThemeComboBox.SelectedIndex = 0;
            }
        }

        // 读取当前设置的主题
        private void LoadCurrentTheme()
        {
            var settingTheme = ThemeManager.Current.ApplicationTheme;

            if (settingTheme == null)
            {
                ThemeComboBox.SelectedIndex = 0;
            }
            else
            {
                ThemeComboBox.SelectedIndex = settingTheme == ApplicationTheme.Light ? 1 : 2;
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();
                string themeValue = tag ?? "Default";

                if (tag == "Default")
                {
                    ThemeManager.Current.ApplicationTheme = null;
                    SyncThemeWithSystem();
                }
                else
                {
                    ThemeManager.Current.ApplicationTheme = tag == "Light"
                        ? ApplicationTheme.Light
                        : ApplicationTheme.Dark;
                }

                // 保存主题设置
                SaveThemeSetting(themeValue);
                UpdateWindowBackdrop();
            }
        }

        private void SaveThemeSetting(string theme)
        {
            var config = ConfigService.LoadConfig() ?? new UserConfig();
            config.Theme = theme;
            ConfigService.SaveConfig(config);
        }

        private void SyncThemeWithSystem()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var registryValue = key.GetValue("AppsUseLightTheme");
                        if (registryValue is int lightThemeValue)
                        {
                            ThemeManager.Current.ApplicationTheme = (lightThemeValue == 0)
                                ? ApplicationTheme.Dark
                                : ApplicationTheme.Light;
                            ThemeManager.Current.ApplicationTheme = null;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private void ToggleAutoLogin_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            var config = ConfigService.LoadConfig() ?? new UserConfig();
            config.AutoLogin = ToggleAutoLogin.IsOn;

            if (config.AutoLogin) config.RememberMe = true;

            ConfigService.SaveConfig(config);
        }

        private void ToggleAutoCheckUpdate_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            var config = ConfigService.LoadConfig() ?? new UserConfig();
            config.AutoCheckUpdate = ToggleAutoCheckUpdate.IsOn;
            ConfigService.SaveConfig(config);
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // 清除保存的登录信息
            var config = ConfigService.LoadConfig() ?? new UserConfig();
            config.Account = "";
            config.Password = "";
            config.RememberMe = false;
            config.AutoLogin = false;
            ConfigService.SaveConfig(config);

            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null) return;

            // 隐藏主窗口（不关闭）
            mainWindow.Hide();

            // 显示登录窗口
            var login = new LoginWindow();
            login.Owner = mainWindow;
            login.Closed += (s, args) =>
            {
                // 登录成功后刷新主窗口信息
                if (login.LoginSucceed)
                {
                    mainWindow.RefreshAfterRelogin();
                }
                // 重新显示主窗口
                mainWindow.Show();
                mainWindow.Activate();
            };
            login.Show();
        }

        private void UpdateWindowBackdrop()
        {
            var current = ThemeManager.Current.ApplicationTheme;
            if (current == null)
            {
                ThemeManager.Current.ApplicationTheme = ThemeManager.Current.ActualApplicationTheme;
                ThemeManager.Current.ApplicationTheme = null;
            }
        }

        // 检查更新
        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;
            CheckUpdateBtn.Content = "检查中...";

            try
            {
                var updateInfo = await UpdateService.CheckForUpdateAsync();
                string currentVersion = UpdateService.GetCurrentVersion();

                if (updateInfo == null)
                {
                    await ShowUpdateDialog("检查更新", $"当前版本: {currentVersion}\n\n无法连接到 GitHub，请检查网络连接。");
                    return;
                }

                if (!UpdateService.HasNewVersion(currentVersion, updateInfo.Version))
                {
                    await ShowUpdateDialog("检查更新", $"当前版本: {currentVersion}\n已经是最新版本！");
                    return;
                }

                // 有新版本，显示更新信息
                await ShowNewVersionDialog(currentVersion, updateInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[更新] 检查异常: {ex.Message}");
                await ShowUpdateDialog("检查更新", "检查更新时发生错误，请稍后重试。");
            }
            finally
            {
                CheckUpdateBtn.IsEnabled = true;
                CheckUpdateBtn.Content = "检查更新";
            }
        }

        private async Task ShowUpdateDialog(string title, string content)
        {
            await new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close
            }.ShowAsync();
        }

        private async Task ShowNewVersionDialog(string currentVersion, UpdateInfo updateInfo)
        {
            bool isSingleFile = UpdateService.IsSingleFilePublish();

            var stackPanel = new StackPanel { Width = 400 };

            // 版本信息
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"发现新版本: v{updateInfo.Version}",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = System.Windows.Media.Brushes.DodgerBlue,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stackPanel.Children.Add(new TextBlock
            {
                Text = $"当前版本: v{currentVersion}",
                Opacity = 0.6,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // 运行模式提示
            string modeText = isSingleFile ? "单文件模式（支持自动更新）" : "压缩包模式（需手动更新）";
            var modeBrush = isSingleFile ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Orange;
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"运行模式: {modeText}",
                FontSize = 12,
                Foreground = modeBrush,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // 更新日志
            if (!string.IsNullOrEmpty(updateInfo.Body))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = "更新内容:",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 6)
                });

                // 限制更新日志长度
                string body = updateInfo.Body;
                if (body.Length > 800)
                    body = body.Substring(0, 800) + "\n...";

                var bodyBorder = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(15, 0, 0, 0)),
                    Padding = new Thickness(12),
                    CornerRadius = new CornerRadius(6),
                    MaxHeight = 200
                };

                var bodyText = new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = 0.8
                };

                var bodyScroll = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 180,
                    Content = bodyText
                };

                bodyBorder.Child = bodyScroll;
                stackPanel.Children.Add(bodyBorder);
            }

            // 下载/更新按钮区域（仅单文件模式显示）
            var actionStack = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };

            if (isSingleFile && updateInfo.IsSingleFileAsset)
            {
                var downloadBtn = new WpfButton
                {
                    Content = "立即下载并更新",
                    Style = (Style)FindResource("AccentButtonStyle"),
                    Height = 36,
                    HorizontalAlignment = WpfHorizontalAlignment.Stretch,
                    Tag = updateInfo
                };
                downloadBtn.Click += DownloadUpdate_Click;
                actionStack.Children.Add(downloadBtn);
            }
            else
            {
                actionStack.Children.Add(new TextBlock
                {
                    Text = "当前为压缩包版本，请手动下载更新：\n" +
                           "1. 在浏览器中下载最新版本压缩包\n" +
                           "2. 解压到当前程序目录覆盖所有文件\n" +
                           "3. MoBoxFrp 文件夹中的隧道配置不会被覆盖",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Opacity = 0.7,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var openBtn = new WpfButton
                {
                    Content = "前往 GitHub 下载",
                    Height = 36,
                    HorizontalAlignment = WpfHorizontalAlignment.Stretch,
                    Tag = updateInfo
                };
                openBtn.Click += (s, args) => UpdateService.OpenReleasePage(updateInfo.HtmlUrl);
                actionStack.Children.Add(openBtn);
            }

            stackPanel.Children.Add(actionStack);

            var dialog = new ContentDialog
            {
                Title = "发现新版本",
                Content = stackPanel,
                CloseButtonText = "稍后再说",
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        }

        private async void DownloadUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is UpdateInfo updateInfo)
            {
                btn.IsEnabled = false;
                btn.Content = "下载中...";

                var progressBorder = new Border
                {
                    Child = new ProgressRing { IsActive = true, Width = 24, Height = 24 },
                    Margin = new Thickness(0, 8, 0, 0),
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                };

                var progress = new Progress<(long downloaded, long total)>(p =>
                {
                    if (p.total > 0)
                    {
                        double percent = (double)p.downloaded / p.total * 100;
                        btn.Content = $"下载中... {percent:F0}%";
                    }
                });

                bool success = await UpdateService.DownloadAndPrepareUpdateAsync(
                    updateInfo.DownloadUrl, progress);

                if (success)
                {
                    var result = await new ContentDialog
                    {
                        Title = "下载完成",
                        Content = "新版本已下载完成，程序将重启以完成更新。\n隧道配置和登录信息将自动保留。",
                        PrimaryButtonText = "立即重启更新",
                        CloseButtonText = "稍后更新",
                        DefaultButton = ContentDialogButton.Primary
                    }.ShowAsync();

                    if (result == ContentDialogResult.Primary)
                    {
                        UpdateService.ApplyUpdateAndRestart();
                    }
                }
                else
                {
                    await ShowUpdateDialog("下载失败", "下载更新文件失败，请检查网络连接后重试。\n你也可以前往 GitHub 手动下载。");
                    btn.IsEnabled = true;
                    btn.Content = "立即下载并更新";
                }
            }
        }

        // 免责声明弹窗
        private void ShowDisclaimer_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog disclaimerDialog = new ContentDialog
            {
                Title = "免责声明",
                Content = new TextBlock
                {
                    Text = "1. 本程序由社区成员（ZhaYi）自发贡献，不属于 MoboxFrp 官方开发的产品\n" +
                           "2. MoboxFrp 官方不提供有关于该软件的任何技术支持\n",
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "我已了解风险",
                DefaultButton = ContentDialogButton.Close
            };
            _ = disclaimerDialog.ShowAsync();
        }

        // 打开GitHub仓库
        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ZhaYi-Miao/MoboxFrpGUI",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenLogDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logDir = LogService.LogDirectory;
                if (!string.IsNullOrEmpty(logDir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logDir,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void CopyErrorDetail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfClipboard.SetText(ErrorDetailTextBox.Text);
                NotificationService.Show("已复制到剪贴板", NotificationType.Info, 2000);
            }
            catch { }
        }

        private void ClearErrorDetail_Click(object sender, RoutedEventArgs e)
        {
            App.LastExceptionReport = "";
            ErrorDetailTextBox.Text = "";
            ErrorDetailPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 静默检查更新（程序启动时调用），如有新版本弹出通知
        /// </summary>
        public static async void SilentCheckUpdateAsync()
        {
            try
            {
                var config = ConfigService.LoadConfig();
                if (config == null || !config.AutoCheckUpdate) return;

                var updateInfo = await UpdateService.CheckForUpdateAsync();
                if (updateInfo == null) return;

                string currentVersion = UpdateService.GetCurrentVersion();
                if (!UpdateService.HasNewVersion(currentVersion, updateInfo.Version)) return;

                // 有新版本，弹出通知
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    NotificationService.ShowPersistent(
                        $"发现新版本 v{updateInfo.Version}，点击查看详情",
                        NotificationType.Info,
                        () =>
                        {
                            // 导航到设置页
                            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.NavigateToSettings();
                            }
                        });
                });
            }
            catch { }
        }
    }
}
