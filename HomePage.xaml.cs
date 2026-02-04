using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using MoboxFrpGUI.Services;
using MoboxFrpGUI.Models;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Application = System.Windows.Application;
using Page = iNKORE.UI.WPF.Modern.Controls.Page;

namespace MoboxFrpGUI.Pages
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        // 引用现有的 ApiService 实例
        private readonly ApiService _apiService = new ApiService();

        public HomePage()
        {
            InitializeComponent();
            // 页面加载时自动获取详细数据
            LoadHomeData();
        }

        private async void LoadHomeData()
        {
            try
            {
                // 1. 调用接口获取用户信息
                // 注意：确保 UserInfoResponse 类中包含 string 类型的 phone 字段
                var info = await _apiService.PostWithTokenAsync<UserInfoResponse>("UserInfo");

                if (info != null && info.success)
                {
                    // 2. 更新右侧信息卡片
                    UpdateUserUI(info);
                }
                else
                {
                    // 如果获取失败，更新 UI 提示
                    TxtUserGroup.Text = "获取失败";
                    TxtRealNameStatus.Text = "未知";
                }
            }
            catch (Exception ex)
            {
                // 调试输出错误信息
                Debug.WriteLine($"加载主页数据出错: {ex.Message}");
            }
        }

        private void UpdateUserUI(UserInfoResponse info)
        {
            // --- 更新用户组 ---
            TxtUserGroup.Text = info.permission ?? "普通用户";

            // --- 手机绑定状态逻辑 (根据 API 返回的 phone 字段) ---
            if (string.IsNullOrWhiteSpace(info.phone))
            {
                // 情况 A: 手机号为空
                TxtRealNameStatus.Text = "未绑定手机号";
                // 使用红色警示
                TxtRealNameStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 67, 67));
            }
            else
            {
                // 情况 B: 手机号不为空
                // 直接显示 API 返回的脱敏手机号 (如 136****1877)
                TxtRealNameStatus.Text = info.phone;

                // 尝试获取 ModernWPF 的标准成功色（绿色）
                var successBrush = Application.Current.TryFindResource("SystemFillColorSuccessBrush") as Brush;
                TxtRealNameStatus.Foreground = successBrush ?? Brushes.Green;
            }



            // --- 其他数据更新 (如有需要可取消注释并对应 XAML 名称) ---
            TxtGold.Text = info.gold?.ToString() ?? "0";
            TxtSilver.Text = info.silver?.ToString() ?? "0";
        }

        /// <summary>
        /// “打开官网”按钮的点击事件
        /// </summary>
        /// 
        private void BtnOpenDoc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://mobox.zhayi.cc/#/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"无法打开浏览器: {ex.Message}");
            }
        }
        private void BtnOpenWeb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.moboxfrp.top/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"无法打开浏览器: {ex.Message}");
            }
        }
    }
}