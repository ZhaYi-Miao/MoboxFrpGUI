using System;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace MoboxFrpGUI
{
    public partial class SettingsPage : Page
    {
        private bool _isInitialized = false;

        public SettingsPage()
        {
            InitializeComponent();
            LoadCurrentTheme();
            _isInitialized = true;
        }

        // 读取当前设置的主题
        private void LoadCurrentTheme()
        {
            
            var actualTheme = ThemeManager.Current.ActualApplicationTheme;
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

                if (tag == "Default") // 默认就是选择跟随系统
                {
                    ThemeManager.Current.ApplicationTheme = null;
                    SyncThemeWithSystem();
                }
                else
                {
                    // 用户手动选择了浅色或深色
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
                            // 强制设置对应颜色
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
                // 以后加一个log功能罢 不急
            }
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

        // 检查更新逻辑
        private void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog updateDialog = new ContentDialog
            {
                Title = "检查更新",
                Content = "当前版本 (1.0.0) 已经是最新版本",
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
                           "2. MoboxFrp 官方不提供有关于该软件的任何技术支持\n" ,
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = "我已了解风险",
                DefaultButton = ContentDialogButton.Close
            };
            _ = disclaimerDialog.ShowAsync();
        }
    }
}