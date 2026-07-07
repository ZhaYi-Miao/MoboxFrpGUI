using System;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;
using MoboxFrpGUI.Services;
using System.Diagnostics;
using WpfClipboard = System.Windows.Clipboard;

namespace MoboxFrpGUI
{
    public partial class SettingsPage : Page
    {
        private bool _isInitialized = false;

        public SettingsPage()
        {
            InitializeComponent();
            LoadCurrentTheme();
            LoadSettingsState();
            LoadLastException();
            _isInitialized = true;
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
            }
            else
            {
                ToggleAutoLogin.IsOn = false;
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

                UpdateWindowBackdrop();
            }
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

            ConfigService.SaveConfig(config.Account, config.Password, config.RememberMe, config.AutoLogin);
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            ConfigService.SaveConfig("", "", false, false);

            var mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.IsForceClosing = true;
            }

            LoginWindow login = new LoginWindow();
            login.Show();

            mainWindow?.Close();
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
        private void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog updateDialog = new ContentDialog
            {
                Title = "检查更新",
                Content = "当前版本 (1.1.1) 已经是最新版本\n实际上没有写任何检查更新的代码（\n去 GitHub 仓库下载吧喵",
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close
            };
            _ = updateDialog.ShowAsync();
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
    }
}
