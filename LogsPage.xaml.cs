using System.Linq;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using Modern = iNKORE.UI.WPF.Modern.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace MoboxFrpGUI
{
    public partial class LogsPage : Modern.Page
    {
        public LogsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => {
                UpdateEmptyHint();
            };
        }

        public void UpdateEmptyHint()
        {
            // 只要有隧道在运行或者运行过，就把提示文字隐藏掉
            if (App.GlobalTunnelList == null) return;

            bool shouldHideHint = App.GlobalTunnelList.Any(t => t.IsRunning || t.HasLog);
            EmptyHint.Visibility = shouldHideHint ? Visibility.Collapsed : Visibility.Visible;
        }

        // 清空日志，但保留日志框
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is MoboxFrpGUI.Models.TunnelItem tunnel)
            {
                tunnel.ClearLog();
                UpdateEmptyHint();
            }
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }
    }
}