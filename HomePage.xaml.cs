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
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Net.Http;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;

namespace MoboxFrpGUI.Pages
{
    public partial class HomePage : Page
    {
        private DispatcherTimer _carouselTimer;
        private AdData _currentAdData;
        private bool _isShowingAd = false;
        private readonly string _baseDomain = "https://www.moboxfrp.top";
        private readonly ApiService _apiService = new ApiService();
        public HomePage()
        {
            InitializeComponent();
            LoadHomeData();
            InitCarousel();
        }


        private async void LoadHomeData()
        {
            try
            {
                var info = await _apiService.PostWithTokenAsync<UserInfoResponse>("UserInfo");

                if (info != null && info.success)
                {
                    UpdateUserUI(info);
                }
                else
                {
                    TxtUserGroup.Text = "获取失败";
                    TxtRealNameStatus.Text = "未知";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载主页数据出错: {ex.Message}");
            }
        }

        private void UpdateUserUI(UserInfoResponse info)
        {
            TxtUserGroup.Text = info.permission ?? "普通用户";

            if (string.IsNullOrWhiteSpace(info.phone))
            {
                TxtRealNameStatus.Text = "未绑定手机号";
                TxtRealNameStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 67, 67));
            }
            else
            {
                TxtRealNameStatus.Text = info.phone;
                var successBrush = Application.Current.TryFindResource("SystemFillColorSuccessBrush") as Brush;
                TxtRealNameStatus.Foreground = successBrush ?? Brushes.Green;
            }



            TxtGold.Text = info.gold?.ToString() ?? "0";
            TxtSilver.Text = info.silver?.ToString() ?? "0";
        }

        private void InitCarousel()
        {
            _carouselTimer = new DispatcherTimer();
            _carouselTimer.Interval = TimeSpan.FromSeconds(8); 
            _carouselTimer.Tick += async (s, e) => await ToggleContent();
            _carouselTimer.Start();
        }

        

        // 广告/公告切换
        private async Task SwitchImage(Image targetImage, string url)
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(url));
                targetImage.Source = bitmap;

                // 等待图片加载完成再做动画
                DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(800));
                targetImage.BeginAnimation(Image.OpacityProperty, fadeIn);
            }
            catch 
            { 
                // 图片加载总归不会报错了吧（？
            }
        }

        private void FadeOutImage(Image targetImage)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(800));
            targetImage.BeginAnimation(Image.OpacityProperty, fadeOut);
        }

        // 刚才咱们跑通的 API 请求逻辑
        // 修改后的获取广告方法
        private async Task<AdData> GetAdFromApi()
        {
            var result = await _apiService.PostWithTokenAsync<AdResponse>("Ad/Get");

            if (result != null && result.Success && result.Data != null)
            {
                return result.Data;
            }
            return null;
        }

        private void AdCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isShowingAd && _currentAdData != null)
            {
                string jumpUrl = "https://www.moboxfrp.top" + _currentAdData.Url_jump;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(jumpUrl) { UseShellExecute = true });
            }
        }

        private async Task ToggleContent()
        {
            if (!_isShowingAd)
            {
                var ad = await GetAdFromApi();
                if (ad != null)
                {
                    _currentAdData = ad;
                    await SwitchToAdUI(ad);
                    _isShowingAd = true;
                }
            }
            else
            {
                SwitchToBingUI();
                _isShowingAd = false;
            }
        }
        private async Task SwitchToAdUI(AdData ad)
        {
            TxtTag.Text = "广告";
            TxtNoticeTitle.Text = " ";
            TxtNotice.Text = " ";
            await SwitchImage(ImgAd, ad.Url_pic);
        }

        // 切换回必应壁纸
        private void SwitchToBingUI()
        {
            TxtTag.Text = "公告栏";
            TxtNoticeTitle.Text = "Mobox FRP 启动器";
            TxtNotice.Text = "体验全新的内网穿透管理，更快速、更稳定。";

            FadeOutImage(ImgAd);
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            _carouselTimer.Stop();
            await ToggleContent();
            _carouselTimer.Start();
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            _carouselTimer.Stop();
            await ToggleContent();
            _carouselTimer.Start();
        }

        private void BtnOpenDoc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://mobox.zhayi.cc/",
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