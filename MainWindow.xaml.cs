using System.Windows;
using MoboxFrpGUI.Services;
using MoboxFrpGUI.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Modern = iNKORE.UI.WPF.Modern.Controls;
using Forms = System.Windows.Forms;

namespace MoboxFrpGUI
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();
        private Forms.NotifyIcon _notifyIcon;
        public bool IsForceClosing { get; set; } = false;


        public MainWindow()
        {
            InitializeComponent();
            InitNotifyIcon();

            NavView.SelectionChanged += NavView_SelectionChanged;
            if (NavView.MenuItems.Count > 0)
                NavView.SelectedItem = NavView.MenuItems[0];

            InitializeUserInfo();
        }

        // 系统托盘
        private void InitNotifyIcon()
        {
            _notifyIcon = new Forms.NotifyIcon();
            _notifyIcon.Text = "MoboxFrp";

            try
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
            }
            catch
            {
                // 如果获取不到就用默认系统图标凑合
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon.Visible = true;
            _notifyIcon.MouseDoubleClick += (s, e) => ShowMainWindow();

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("显示主界面", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add("彻底退出", null, (s, e) =>
            {
                _isExiting = true;
                ShutdownAndCleanup();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }


        // 侧边栏用户信息（仅在开启时更新
        private async Task LoadUserInfoAsync()
        {
            try
            {
                var info = await _apiService.PostWithTokenAsync<UserInfoResponse>("UserInfo");
                if (info != null && info.success)
                {
                    TxtUserName.Text = info.username;
                    TxtBalance.Text = $"金币: {info.gold} | 银币: {info.silver}";
                    if (UserPane.Icon is PersonPicture avatar)
                    {
                        avatar.DisplayName = info.username;
                    }
                }
                else
                {
                    TxtUserName.Text = "获取失败喵";
                }
            }
            catch (Exception ex)
            {
                TxtUserName.Text = "网络错误";
                Debug.WriteLine($"加载用户信息失败: {ex.Message}");
            }
        }

        // 在构造函数中调用异步方法
        private async void InitializeUserInfo()
        {
            await LoadUserInfoAsync();
        }

        // 侧边栏跳转逻辑
        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(new SettingsPage());
            }
            else if (args.SelectedItem is NavigationViewItem item)
            {
                if (item.Tag == null) return;
                string tag = item.Tag.ToString();

                switch (tag)
                {
                    case "Home":
                        ContentFrame.Navigate(new HomePage());
                        break;
                    case "Tunnels":
                        ContentFrame.Navigate(new TunnelsPage());
                        break;
                    case "UserCodes":
                        ContentFrame.Navigate(new UserCodesPage());
                        break;
                    case "Logs":
                        ContentFrame.Navigate(new LogsPage());
                        break;
                    case "Settings":
                        ContentFrame.Navigate(new SettingsPage());
                        break;
                }
                if (NavView.DisplayMode != NavigationViewDisplayMode.Expanded)
                {
                    NavView.IsPaneOpen = false;
                }
            }
        }

        private bool _isExiting = false;


        // 退出程序拦截
        protected override async void OnClosing(CancelEventArgs e)
        {
            // 如果已经在退出流程中，直接退出
            if (_isExiting)
            {
                base.OnClosing(e);
                return;
            }

            // 如果强制关闭标志已设置，直接退出
            if (IsForceClosing)
            {
                _isExiting = true;
                ShutdownAndCleanup();
                base.OnClosing(e);
                return;
            }

            // 取消关闭事件，等待用户确认
            e.Cancel = true;

            try
            {
                var dialog = new Modern.ContentDialog
                {
                    Title = "退出确认",
                    Content = "您想要彻底关闭 MoboxFrp 吗？\n选择\"最小化\"将保持隧道在后台运行。",
                    PrimaryButtonText = "彻底退出",
                    SecondaryButtonText = "最小化到托盘",
                    CloseButtonText = "取消",
                    DefaultButton = Modern.ContentDialogButton.Secondary
                };

                var result = await dialog.ShowAsync();

                if (result == Modern.ContentDialogResult.Primary)
                {
                    _isExiting = true;
                    e.Cancel = false; // 允许关闭
                    ShutdownAndCleanup();
                }
                else if (result == Modern.ContentDialogResult.Secondary)
                {
                    this.Hide();
                    _notifyIcon.ShowBalloonTip(3000, "MoboxFrp", "已最小化到系统托盘", Forms.ToolTipIcon.Info);
                }
                // Cancel按钮不做任何操作，窗口保持打开状态
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭确认对话框失败: {ex.Message}");
                // 如果对话框失败，允许关闭
                _isExiting = true;
                e.Cancel = false;
                ShutdownAndCleanup();
            }
        }

        // 退出时杀死所有frpc进程
        private void ShutdownAndCleanup()
        {
            this.Hide();

            // 停止所有隧道
            if (App.GlobalTunnelList != null)
            {
                var tunnels = App.GlobalTunnelList.ToList();
                foreach (var tunnel in tunnels)
                {
                    try
                    {
                        tunnel?.Stop();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"停止隧道 {tunnel?.Name} 失败: {ex.Message}");
                    }
                }
            }

            // 清理可能残留的frpc进程（安全检查）
            KillAllFrpProcesses();

            // 清理托盘图标
            try
            {
                _notifyIcon?.Dispose();
            }
            catch { }

            System.Windows.Application.Current.Shutdown();
        }

        // 根据进程名字清理残留的frpc进程
        private void KillAllFrpProcesses()
        {
            try
            {
                // 只清理当前用户启动的frpc进程，避免影响其他用户的进程
                var frpProcesses = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("frpc", StringComparison.OrdinalIgnoreCase));

                foreach (var p in frpProcesses)
                {
                    try
                    {
                        // 尝试优雅关闭
                        if (!p.WaitForExit(2000))
                        {
                            p.Kill(true);
                        }
                        p.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"清理进程 {p.Id} 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"frpc进程清理异常: {ex.Message}");
            }
        }
    }
}