using System;
using System.Windows;
using MoboxFrpGUI.Services;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;

namespace MoboxFrpGUI
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService = new ApiService();

        public LoginWindow()
        {
            InitializeComponent();
            ExtractResources();
            LoadSavedConfig();
        }
        public void ExtractResources()
        {
            string resourceName = "MoboxFrpGUI.Resources.frpc.exe";
            string targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            string targetFile = Path.Combine(targetFolder, "frpc.exe");

            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            if (!File.Exists(targetFile))
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;

                    using (FileStream fileStream = new FileStream(targetFile, FileMode.Create))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }

        
        private void LoadSavedConfig()
        {
            var config = ConfigService.LoadConfig();
            if (config != null && config.RememberMe)
            {
                TxtAccount.Text = config.Account;
                TxtPassword.Password = config.Password;
                ChkRemember.IsChecked = true;
            }
        }

        // 登录校验
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string account = TxtAccount.Text.Trim();
            string password = TxtPassword.Password;
            bool isRemember = ChkRemember.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            {
                TxtStatus.Text = "请输入完整的账号和密码";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtStatus.Text = "正在连接 MoBoxFrp 服务器...";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Gray;

            var result = await _apiService.LoginAsync(account, password);

            if (result != null && result.Success)
            {
                if (isRemember)
                {
                    ConfigService.SaveConfig(account, password, true);
                }
                else
                {
                    ConfigService.SaveConfig(string.Empty, string.Empty, false);
                }

                TxtStatus.Text = "登录成功！正在跳转……";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Green;

                await Task.Delay(800);

                MainWindow main = new MainWindow();
                main.Show();
                this.Close();
            }
            else
            {
                BtnLogin.IsEnabled = true;
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;

                string errorDetail = result?.Message ?? "网络异常或服务器无响应";
                TxtStatus.Text = $"登录失败：{errorDetail}";
            }
        }
    }
}