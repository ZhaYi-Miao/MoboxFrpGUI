using MoboxFrpGUI.Models;
using MoboxFrpGUI.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;

namespace MoboxFrpGUI
{
    public partial class App : System.Windows.Application
    {
        public static ObservableCollection<TunnelItem> GlobalTunnelList { get; } = new ObservableCollection<TunnelItem>();
        public static string LastExceptionReport { get; set; } = "";
        public static string? PendingToastTunnelName { get; set; }

        public App()
        {
            InitializeComponent();
            RegisterGlobalExceptionHandlers();
            ApplySystemTheme();
            LogService.CleanOldLogs(7);
            LogService.Info("应用程序启动");
            ToastService.Initialize();
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                LogService.Error("UI线程未处理异常", e.Exception);
                LastExceptionReport = LogService.GetFullErrorReport(e.Exception);
                e.Handled = true;

                Dispatcher.Invoke(() =>
                {
                    NotificationService.ShowPersistent(
                        $"UI错误: {e.Exception.Message}",
                        NotificationType.Error,
                        () => NavigateToErrorDetail());
                });
            }
            catch
            {
                e.Handled = false;
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                SaveCrashLogs();
                LogService.Fatal("非UI线程未处理异常", ex);

                if (ex != null)
                {
                    LastExceptionReport = LogService.GetFullErrorReport(ex);
                    Dispatcher.Invoke(() => ShowCrashReport(ex));
                }
            }
            catch
            {
            }
        }

        private static void SaveCrashLogs()
        {
            try
            {
                if (GlobalTunnelList != null)
                {
                    foreach (var tunnel in GlobalTunnelList)
                    {
                        try
                        {
                            if (tunnel.IsRunning)
                            {
                                tunnel.SavePersistentLog(false);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void LoadAndDetectAllTunnels()
        {
            try
            {
                string tunnelRootDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoBoxFrp", "Tunnels");
                System.Diagnostics.Debug.WriteLine($"[App] 加载隧道目录: {tunnelRootDir}");

                if (!Directory.Exists(tunnelRootDir))
                {
                    Directory.CreateDirectory(tunnelRootDir);
                    return;
                }

                var tunnelFolders = Directory.GetDirectories(tunnelRootDir);

                foreach (var dir in tunnelFolders)
                {
                    string name = Path.GetFileName(dir);
                    string configPath = Path.Combine(dir, "config.toml");

                    if (!File.Exists(configPath)) continue;
                    if (GlobalTunnelList.Any(t => t.Name == name)) continue;

                    var item = new TunnelItem
                    {
                        Name = name,
                        ConfigPath = configPath,
                        IsRunning = false
                    };
                    item.ParseConfig();

                    bool hasCrashLog = item.LoadPersistentLog();
                    bool processAttached = item.CheckAndAttachExistingProcess();

                    if (hasCrashLog && !processAttached)
                    {
                        item.Status = "异常退出";
                        item.TunnelStatus = MoboxFrpGUI.Models.TunnelStatus.Error;
                    }

                    GlobalTunnelList.Add(item);
                    System.Diagnostics.Debug.WriteLine($"[App] 已加载隧道: {name}, 进程={processAttached}, 崩溃日志={hasCrashLog}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 加载隧道失败: {ex.Message}");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                LogService.Error("任务未观察异常", e.Exception);
                e.SetObserved();

                var innerEx = e.Exception.InnerException ?? e.Exception;
                LastExceptionReport = LogService.GetFullErrorReport(innerEx);

                Dispatcher.Invoke(() =>
                {
                    NotificationService.ShowPersistent(
                        $"后台任务错误: {innerEx.Message}",
                        NotificationType.Warning,
                        () => NavigateToErrorDetail());
                });
            }
            catch
            {
                try { e.SetObserved(); } catch { }
            }
        }

        private void NavigateToErrorDetail()
        {
            try
            {
                if (Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.NavigateToSettings();
                }
            }
            catch { }
        }

        private void ShowCrashReport(Exception ex)
        {
            try
            {
                var crashWindow = new CrashReportWindow(ex);
                crashWindow.ShowDialog();

                if (!crashWindow.ShouldExit)
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                Current.Shutdown();
            }
            catch
            {
                Environment.Exit(1);
            }
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