using System;
using System.Windows;
using MoboxFrpGUI.Services;
using MoboxFrpGUI.Models;
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
            if (config != null)
            {
                TxtAccount.Text = config.Account;
                TxtPassword.Password = config.Password;
                ChkRemember.IsChecked = config.RememberMe;
                ChkAutoLogin.IsChecked = config.AutoLogin;

                if (config.AutoLogin && !string.IsNullOrWhiteSpace(config.Account))
                {
                    Dispatcher.BeginInvoke(new Action(async () => {
                        await PerformLogin(config.Account, config.Password, true);
                    }));
                }
            }
        }

        // 登录复选框同步
        private void ChkRemember_Unchecked(object sender, RoutedEventArgs e)
        {
            ChkAutoLogin.IsChecked = false;
        }

        private void ChkAutoLogin_Checked(object sender, RoutedEventArgs e)
        {
            if (ChkRemember != null)
            {
                ChkRemember.IsChecked = true;
            }
        }

        // 自动登录
        private async Task PerformLogin(string account, string password, bool isAuto)
        {
            BtnLogin.IsEnabled = false;
            TxtStatus.Text = "正在自动登录...";

            var result = await _apiService.LoginAsync(account, password);
            if (result != null && result.Success)
            {
                MainWindow main = new MainWindow();
                System.Windows.Application.Current.MainWindow = main;
                main.Show();
                main.CheckSurvivingProcesses();
                this.Close();
            }
            else
            {
                BtnLogin.IsEnabled = true;
                
                bool isNetworkError = IsNetworkError(result);
                if (isNetworkError)
                {
                    TxtStatus.Text = isAuto ? "自动登录失败（网络错误）" : $"登录失败：网络错误";
                    ShowOfflineLoginButton();
                }
                else
                {
                    TxtStatus.Text = isAuto ? "自动登录失败，请手动登录" : $"登录失败：{result?.Message}";
                    HideOfflineLoginButton();
                }
            }
        }

       
        

        // 登录校验
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string account = TxtAccount.Text.Trim();
            string password = TxtPassword.Password;
            bool isAutoLogin = ChkAutoLogin.IsChecked ?? false;
            bool isRemember = ChkRemember.IsChecked ?? false;

            if (isAutoLogin)
            {
                isRemember = true;
            }

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(password))
            {
                TxtStatus.Text = "请输入完整的账号和密码";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
                HideOfflineLoginButton();
                return;
            }

            BtnLogin.IsEnabled = false;
            TxtStatus.Text = "正在连接 MoBoxFrp 服务器...";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Gray;
            HideOfflineLoginButton();

            var result = await _apiService.LoginAsync(account, password);

            if (result != null && result.Success)
            {
                bool saveSuccess = ConfigService.SaveConfig(account, password, isRemember, isAutoLogin);
                if (!saveSuccess)
                {
                    System.Diagnostics.Debug.WriteLine("保存登录信息失败，下次需要手动登录");
                }

                TxtStatus.Text = "登录成功！正在跳转……";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Green;

                await Task.Delay(800);

                MainWindow main = new MainWindow();
                System.Windows.Application.Current.MainWindow = main;
                main.Show();
                main.CheckSurvivingProcesses();
                this.Close();
            }
            else
            {
                BtnLogin.IsEnabled = true;
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;

                bool isNetworkError = IsNetworkError(result);
                if (isNetworkError)
                {
                    TxtStatus.Text = "登录失败：网络错误，请检查网络连接";
                    ShowOfflineLoginButton();
                }
                else
                {
                    string errorDetail = result?.Message ?? "未知错误";
                    TxtStatus.Text = $"登录失败：{errorDetail}";
                    HideOfflineLoginButton();
                }
            }
        }

        private bool IsNetworkError(LoginResponse result)
        {
            if (result == null) return true;

            string msg = result.Message?.ToLower() ?? "";
            return msg.Contains("网络") || msg.Contains("ssl") || msg.Contains("tls") || 
                   msg.Contains("超时") || msg.Contains("refused") || msg.Contains("timed");
        }

        private void ShowOfflineLoginButton()
        {
            BtnOfflineLogin.Visibility = Visibility.Visible;
            BtnOfflineLogin.IsEnabled = true;
        }

        private void HideOfflineLoginButton()
        {
            BtnOfflineLogin.Visibility = Visibility.Collapsed;
            BtnOfflineLogin.IsEnabled = false;
        }

        private void BtnOfflineLogin_Click(object sender, RoutedEventArgs e)
        {
            ApiService.CurrentToken = "";
            MainWindow main = new MainWindow();
            System.Windows.Application.Current.MainWindow = main;
            main.Show();
            main.CheckSurvivingProcesses();
            this.Close();
        }
    }
}