using MoboxFrpGUI.Models;
using System.Collections.ObjectModel;
using System.Windows;
using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;

namespace MoboxFrpGUI
{
    public partial class App : System.Windows.Application
    {
        public static ObservableCollection<TunnelItem> GlobalTunnelList { get; } = new ObservableCollection<TunnelItem>();

        public App()
        {
            InitializeComponent();
            ApplySystemTheme();
        }

        private void ApplySystemTheme()
        {
            try
            {
                ThemeManager.Current.ApplicationTheme = null;
                
                // 抽查一下注册表的系统主题设置是啥
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var registryValue = key.GetValue("AppsUseLightTheme");
                        if (registryValue is int lightThemeValue)
                        {
                            // 0是深色 1是浅色
                            ThemeManager.Current.ApplicationTheme = (lightThemeValue == 0)
                                ? ApplicationTheme.Dark
                                : ApplicationTheme.Light;
                        }
                    }
                }
            }
            catch
            {
                // 有问题就用深色嘛 好看的喵
                ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
            }
        }
    }
}