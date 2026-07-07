using System;
using System.Windows;
using MoboxFrpGUI.Services;
using WpfClipboard = System.Windows.Clipboard;

namespace MoboxFrpGUI
{
    public partial class CrashReportWindow : Window
    {
        private readonly Exception _exception;
        private readonly string _errorReport;

        public bool ShouldExit { get; private set; } = true;

        public CrashReportWindow(Exception ex)
        {
            InitializeComponent();
            _exception = ex;
            _errorReport = LogService.GetFullErrorReport(ex);
            ErrorTextBox.Text = _errorReport;
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfClipboard.SetText(_errorReport);
                CopyBtn.Content = "已复制！";
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, args) =>
                {
                    CopyBtn.Content = "复制错误信息";
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                CopyBtn.Content = "复制失败";
            }
        }

        private void ContinueBtn_Click(object sender, RoutedEventArgs e)
        {
            ShouldExit = false;
            this.Close();
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            ShouldExit = true;
            this.Close();
        }
    }
}
