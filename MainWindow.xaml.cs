using System.Windows;
using MoboxFrpGUI.Services;
using MoboxFrpGUI.Pages;
using iNKORE.UI.WPF.Modern.Controls;
using System.ComponentModel;
using System.Diagnostics;
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

            LoadUserInfoAsync();
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
        private async void LoadUserInfoAsync()
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
            if (_isExiting)
            {
                base.OnClosing(e);
                return;
            }

            if (IsForceClosing)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;

            var dialog = new Modern.ContentDialog
            {
                Title = "退出确认",
                Content = "您想要彻底关闭 MoboxFrp 吗？\n选择“最小化”将保持隧道在后台运行。",
                PrimaryButtonText = "彻底退出",
                SecondaryButtonText = "最小化到托盘",
                CloseButtonText = "取消",
                DefaultButton = Modern.ContentDialogButton.Secondary
            };

            var result = await dialog.ShowAsync();

            if (result == Modern.ContentDialogResult.Primary)
            {
                _isExiting = true;
                ShutdownAndCleanup();
            }
            else if (result == Modern.ContentDialogResult.Secondary)
            {
                this.Hide();
                _notifyIcon.ShowBalloonTip(3000, "MoboxFrp", "已最小化到系统托盘", Forms.ToolTipIcon.Info);
            }
        }

        // 退出时杀死所有frpc进程
        private void ShutdownAndCleanup()
        {
            this.Hide();
            if (App.GlobalTunnelList != null)
            {
                var tunnels = App.GlobalTunnelList.ToList();
                foreach (var tunnel in tunnels)
                {
                    tunnel?.Stop();
                }
            }
            // 编诗环节（
            // KillAllFrpProcesses();

            _notifyIcon?.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
         /*
        // 根据进程名字补刀
        private void KillAllFrpProcesses()
        {
            try
            {
                var frpProcesses = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("frpc", StringComparison.OrdinalIgnoreCase));

                foreach (var p in frpProcesses)
                {
                     p.Kill(true);
                     p.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"frpc退出可能异常: {ex.Message}");
            }
        }*/
    }
}