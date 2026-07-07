using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using iNKORE.UI.WPF.Modern.Controls;
using Modern = iNKORE.UI.WPF.Modern.Controls;
using MoboxFrpGUI.Models;
using System.Threading.Tasks;

namespace MoboxFrpGUI
{
    public partial class TunnelsPage : Modern.Page
    {
        public TunnelsPage()
        {
            InitializeComponent();
            TunnelsItemsControl.ItemsSource = App.GlobalTunnelList;

            Loaded += (s, e) => {
                LoadLocalTunnels();
            };
        }

        private void LoadLocalTunnels()
        {
            try
            {
                string tunnelRootDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoBoxFrp", "Tunnels");
                if (!Directory.Exists(tunnelRootDir)) Directory.CreateDirectory(tunnelRootDir);

                var tunnelFolders = Directory.GetDirectories(tunnelRootDir);

                foreach (var dir in tunnelFolders)
                {
                    string name = Path.GetFileName(dir);
                    string configPath = Path.Combine(dir, "config.toml");

                    if (File.Exists(configPath))
                    {
                        bool exists = App.GlobalTunnelList.Any(t => t.Name == name);
                        if (!exists)
                        {
                            var item = new TunnelItem
                            {
                                Name = name,
                                ConfigPath = configPath,
                                IsRunning = false
                            };
                            item.ParseConfig();
                            App.GlobalTunnelList.Add(item);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("加载隧道失败: " + ex.Message);
            }
        }

        // 启动
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TunnelItem tunnel)
            {
                tunnel.ParseConfig();
                tunnel.Start();
            }
        }

        // 停止
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TunnelItem tunnel)
            {
                tunnel.Stop();
            }
        }

        // 用记事本编辑toml文件
        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TunnelItem item)
            {
                try
                {
                    if (File.Exists(item.ConfigPath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "notepad.exe",
                            Arguments = item.ConfigPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Modern.MessageBox.Show($"无法打开记事本: {ex.Message}");
                }
            }
        }

        // 删除隧道 引入了4位的pin码做一个二次确认
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TunnelItem item)
            {
                Random rnd = new Random();
                string pinCode = rnd.Next(1000, 9999).ToString();

                var inputTextBox = new System.Windows.Controls.TextBox
                {
                    Margin = new Thickness(0, 10, 0, 0),
                    HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold
                };

                var errorTip = new TextBlock
                {
                    Text = "验证码错误，请重新输入",
                    Foreground = System.Windows.Media.Brushes.OrangeRed,
                    FontSize = 12,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0),
                    Visibility = Visibility.Collapsed
                };

                inputTextBox.TextChanged += (s, args) =>
                {
                    if (errorTip.Visibility == Visibility.Visible)
                        errorTip.Visibility = Visibility.Collapsed;
                };

                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"确定要彻底删除隧道 [{item.Name}] 吗？",
                    TextWrapping = TextWrapping.Wrap
                });

                var pinDisplay = new TextBlock
                {
                    Text = pinCode,
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 15, 0, 15),
                    Foreground = System.Windows.Media.Brushes.Red
                };

                stackPanel.Children.Add(pinDisplay);
                stackPanel.Children.Add(inputTextBox);
                stackPanel.Children.Add(errorTip);

                var dialog = new Modern.ContentDialog
                {
                    Title = "删除隧道",
                    Content = stackPanel,
                    PrimaryButtonText = "确认删除",
                    CloseButtonText = "取消",
                    DefaultButton = Modern.ContentDialogButton.Primary
                };

                dialog.PrimaryButtonClick += (s, args) =>
                {
                    if (inputTextBox.Text.Trim() != pinCode)
                    {
                        args.Cancel = true;
                        errorTip.Visibility = Visibility.Visible;
                        inputTextBox.Focus();
                        inputTextBox.SelectAll();
                    }
                };

                dialog.Opened += (s, args) =>
                {
                    inputTextBox.Dispatcher.BeginInvoke(new Action(() => inputTextBox.Focus()),
                        System.Windows.Threading.DispatcherPriority.Input);
                };

                if (await dialog.ShowAsync() == Modern.ContentDialogResult.Primary)
                {
                    try
                    {
                        if (item.IsRunning)
                        {
                            item.Stop();
                            await Task.Delay(800);
                        }

                        string tunnelDir = Path.GetDirectoryName(item.ConfigPath);
                        if (Directory.Exists(tunnelDir))
                        {
                            Directory.Delete(tunnelDir, true);
                        }

                        bool removed = App.GlobalTunnelList.Remove(item);
                        if (!removed)
                        {
                            var found = App.GlobalTunnelList.FirstOrDefault(t => t.Name == item.Name);
                            if (found != null)
                            {
                                App.GlobalTunnelList.Remove(found);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Modern.MessageBox.Show($"删除失败: {ex.Message}");
                    }
                }
            }
        }
    }
}