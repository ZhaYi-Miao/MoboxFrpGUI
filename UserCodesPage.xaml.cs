using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern.Controls;
using MoboxFrpGUI.Models;
using MoboxFrpGUI.Services;
using Modern = iNKORE.UI.WPF.Modern.Controls;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using RadioButton = System.Windows.Controls.RadioButton;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using ComboBox = System.Windows.Controls.ComboBox;

namespace MoboxFrpGUI
{
    public partial class UserCodesPage : Modern.Page
    {
        private readonly ApiService _api = new ApiService();
        public ObservableCollection<UserCodeItem> CodeList { get; set; } = new ObservableCollection<UserCodeItem>();

        public UserCodesPage()
        {
            InitializeComponent();
            CodesDataGrid.ItemsSource = CodeList;
            Loaded += async (s, e) => await LoadCodesAsync();
        }

        private async Task LoadCodesAsync()
        {
            LoadingProgressBar.Visibility = Visibility.Visible;
            try
            {
                var res = await _api.PostWithTokenAsync<UserCodeListResponse>("UserCode/List");
                if (res != null && res.success)
                {
                    CodeList.Clear();
                    if (res.codes != null)
                    {
                        foreach (var c in res.codes)
                        {
                            CodeList.Add(c);
                        }
                    }
                }
                else
                {
                    ContentDialogSafe("错误", res?.message ?? "获取数据失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载穿透码列表出错: {ex.Message}");
            }
            finally
            {
                LoadingProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void ContentDialogSafe(string title, string content)
        {
            try
            {
                await new ContentDialog
                {
                    Title = title,
                    Content = content,
                    CloseButtonText = "确定",
                    DefaultButton = ContentDialogButton.Close
                }.ShowAsync();
            }
            catch { }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadCodesAsync();

        // 复制功能
        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserCodeItem item)
            {
                try
                {
                    System.Windows.Clipboard.SetText(item.token);
                    var oldContent = btn.Content;
                    btn.Content = "已复制";
                    await Task.Delay(1000);
                    btn.Content = oldContent;
                }
                catch { }
            }
        }

        private async void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserCodeItem item)
            {
                var nodeRes = await _api.PostWithTokenAsync<NodeListResponse>("Node/List");
                if (nodeRes == null || !nodeRes.success)
                {
                    Modern.MessageBox.Show("获取节点单价失败，请检查网络");
                    return;
                }

                var currentNode = nodeRes.nodes.FirstOrDefault(n => n.nodeID == item.node || n.name == item.node);
                if (currentNode == null)
                {
                    Modern.MessageBox.Show("找不到对应的节点信息，无法计算价格");
                    return;
                }

                if (!double.TryParse(currentNode.price?.ToString(), out double unitPrice))
                {
                    unitPrice = 0.0;
                }

                double remainingDays = 0;
                try
                {
                    if (long.TryParse(item.timeOutdate, out long ms))
                    {
                        DateTime expireDate = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                        var span = expireDate - DateTime.Now;
                        remainingDays = Math.Max(0, Math.Ceiling(span.TotalDays));
                    }
                }
                catch 
                { 
                    // 这里应该不会报错吧（？
                }

                var rootGrid = new Grid();
                var contentStack = new StackPanel { Width = 300, Visibility = Visibility.Visible };

                contentStack.Children.Add(new TextBlock
                {
                    Text = $"升配穿透码: {item.codeID}",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                contentStack.Children.Add(new TextBlock { Text = "增加的带宽量 (Mbps):", Opacity = 0.7 });
                var bandSlider = new Slider
                {
                    Minimum = 1,
                    Maximum = 100,
                    Value = 1,
                    IsSnapToTickEnabled = true,
                    TickFrequency = 1,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                contentStack.Children.Add(bandSlider);

                // 价格预览
                var priceBorder = new Border
                {
                    Background = (Brush)new BrushConverter().ConvertFrom("#0A000000"),
                    Padding = new Thickness(15),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 15, 0, 0)
                };
                var priceInfoTxt = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
                priceBorder.Child = priceInfoTxt;
                contentStack.Children.Add(priceBorder);

                var loadingStack = new StackPanel
                {
                    Visibility = Visibility.Collapsed,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                loadingStack.Children.Add(new Modern.ProgressRing { IsActive = true, Width = 40, Height = 40 });
                loadingStack.Children.Add(new TextBlock
                {
                    Text = "正在提交升配请求...",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center, // 修复 CS0176
                    Margin = new Thickness(0, 10, 0, 0),
                    Opacity = 0.6
                });

                rootGrid.Children.Add(contentStack);
                rootGrid.Children.Add(loadingStack);

                string coinName = currentNode.coin == "gold" ? "金币" : "银币";
                Action updatePrice = () => {
                    int addBand = (int)bandSlider.Value;
                    double totalPrice = unitPrice * addBand * remainingDays;

                    priceInfoTxt.Inlines.Clear();
                    priceInfoTxt.Inlines.Add(new System.Windows.Documents.Run("预估补差价: ") { Foreground = Brushes.Gray });
                    priceInfoTxt.Inlines.Add(new System.Windows.Documents.Run($"{totalPrice} {coinName}")
                    {
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.DodgerBlue
                    });
                    priceInfoTxt.Inlines.Add(new System.Windows.Documents.LineBreak());
                    priceInfoTxt.Inlines.Add(new System.Windows.Documents.Run($"公式: {unitPrice}(单价) × {addBand}M × {remainingDays}天")
                    {
                        FontSize = 11,
                        Foreground = Brushes.Gray
                    });
                };

                bandSlider.ValueChanged += (s, ev) => updatePrice();
                updatePrice();

                var dialog = new Modern.ContentDialog
                {
                    Title = "配置升配",
                    Content = rootGrid,
                    PrimaryButtonText = "确认支付并升级",
                    CloseButtonText = "取消",
                    DefaultButton = Modern.ContentDialogButton.Primary
                };

                dialog.PrimaryButtonClick += async (s, args) =>
                {
                    var deferral = args.GetDeferral();
                    contentStack.Visibility = Visibility.Collapsed;
                    loadingStack.Visibility = Visibility.Visible;
                    dialog.IsPrimaryButtonEnabled = false;
                    dialog.IsSecondaryButtonEnabled = false;

                    try
                    {
                        var res = await _api.PostWithTokenAsync<UserCodeListResponse>("UserCode/Upgrade", new
                        {
                            token = ApiService.CurrentToken,
                            codeID = item.codeID,
                            band = ((int)bandSlider.Value).ToString()
                        });

                        if (res != null && res.success)
                        {
                            dialog.Hide();
                            await LoadCodesAsync();
                        }
                        else
                        {
                            Modern.MessageBox.Show(res?.message ?? "服务器拒绝了升配请求");
                            contentStack.Visibility = Visibility.Visible;
                            loadingStack.Visibility = Visibility.Collapsed;
                            dialog.IsPrimaryButtonEnabled = true;
                            dialog.IsSecondaryButtonEnabled = true;
                            args.Cancel = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Modern.MessageBox.Show("发生错误: " + ex.Message);
                        args.Cancel = true;
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                };

                await dialog.ShowAsync();
            }
        }

        // 升配穿透码
        private async void Renew_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserCodeItem item)
            {

                var nodeRes = await _api.PostWithTokenAsync<NodeListResponse>("Node/List");
                if (nodeRes == null || !nodeRes.success) { Modern.MessageBox.Show("获取费率失败"); return; }

                var currentNode = nodeRes.nodes.FirstOrDefault(n => n.nodeID == item.node || n.name == item.node);
                if (currentNode == null) { Modern.MessageBox.Show("找不到节点信息"); return; }

                double unitPrice = double.TryParse(currentNode.price?.ToString(), out double p) ? p : 0.0;
                double currentBand = double.TryParse(item.band, out double b) ? b : 1.0;

                var rootGrid = new Grid();
                var contentStack = new StackPanel { Width = 300, Visibility = Visibility.Visible };

                contentStack.Children.Add(new TextBlock { Text = $"续费穿透码: {item.codeID}", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 15) });
                contentStack.Children.Add(new TextBlock { Text = "续费天数:", Opacity = 0.7 });

                // 输入框 默认7天
                var dayInput = new System.Windows.Controls.TextBox
                {
                    Text = "7",
                    Margin = new Thickness(0, 5, 0, 5),
                    Padding = new Thickness(5)
                };

                // 滑动条 1-365天
                var daySlider = new Slider
                {
                    Minimum = 1,
                    Maximum = 365,
                    Value = 7,
                    IsSnapToTickEnabled = true,
                    TickFrequency = 1,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                contentStack.Children.Add(dayInput);
                contentStack.Children.Add(daySlider);

                // 价格预览
                var priceBorder = new Border
                {
                    Background = (Brush)new BrushConverter().ConvertFrom("#0A000000"),
                    Padding = new Thickness(15),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 15, 0, 0)
                };
                var priceInfoTxt = new TextBlock { TextWrapping = TextWrapping.Wrap, LineHeight = 22 };
                priceBorder.Child = priceInfoTxt;
                contentStack.Children.Add(priceBorder);

                var loadingStack = new StackPanel { Visibility = Visibility.Collapsed, VerticalAlignment = System.Windows.VerticalAlignment.Center };
                loadingStack.Children.Add(new Modern.ProgressRing { IsActive = true, Width = 40, Height = 40, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
                loadingStack.Children.Add(new TextBlock { Text = "正在处理续费请求...", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0), Opacity = 0.6 });
                rootGrid.Children.Add(contentStack);
                rootGrid.Children.Add(loadingStack);

                bool isUpdating = false;
                string coinName = currentNode.coin == "gold" ? "金币" : "银币";

                Action updatePrice = () => {
                    if (double.TryParse(dayInput.Text, out double days) && days > 0)
                    {
                        double totalPrice = unitPrice * currentBand * days;
                        priceInfoTxt.Inlines.Clear();
                        priceInfoTxt.Inlines.Add(new System.Windows.Documents.Run("预估总价: ") { Foreground = Brushes.Gray });
                        priceInfoTxt.Inlines.Add(new System.Windows.Documents.Run($"{totalPrice} {coinName}") { FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Brushes.DodgerBlue });
                        priceInfoTxt.Inlines.Add(new System.Windows.Documents.LineBreak());
                        priceInfoTxt.Inlines.Add(new System.Windows.Documents.Run($"公式: {currentBand}M × {unitPrice}(单) × {days}天") { FontSize = 11, Foreground = Brushes.Gray });
                    }
                };

                daySlider.ValueChanged += (s, e_val) => {
                    if (isUpdating) return;
                    isUpdating = true;
                    dayInput.Text = ((int)daySlider.Value).ToString();
                    updatePrice();
                    isUpdating = false;
                };

                dayInput.TextChanged += (s, e_txt) => {
                    if (isUpdating) return;
                    if (double.TryParse(dayInput.Text, out double val))
                    {
                        isUpdating = true;
                        daySlider.Value = Math.Clamp(val, daySlider.Minimum, daySlider.Maximum);
                        updatePrice();
                        isUpdating = false;
                    }
                };

                updatePrice();

                var dialog = new Modern.ContentDialog
                {
                    Title = "续费穿透码",
                    Content = rootGrid,
                    PrimaryButtonText = "确认续费",
                    CloseButtonText = "取消",
                    DefaultButton = Modern.ContentDialogButton.Primary
                };

                dialog.PrimaryButtonClick += async (s, args) => {
                    var deferral = args.GetDeferral();
                    contentStack.Visibility = Visibility.Collapsed;
                    loadingStack.Visibility = Visibility.Visible;
                    dialog.IsPrimaryButtonEnabled = false;

                    try
                    {
                        var res = await _api.PostWithTokenAsync<UserCodeListResponse>("UserCode/Renew", new
                        {
                            token = ApiService.CurrentToken,
                            codeID = item.codeID,
                            day = dayInput.Text
                        });

                        if (res != null && res.success) { dialog.Hide(); await LoadCodesAsync(); }
                        else
                        {
                            Modern.MessageBox.Show(res?.message ?? "续费失败");
                            contentStack.Visibility = Visibility.Visible;
                            loadingStack.Visibility = Visibility.Collapsed;
                            dialog.IsPrimaryButtonEnabled = true;
                            args.Cancel = true;
                        }
                    }
                    finally { deferral.Complete(); }
                };

                await dialog.ShowAsync();
            }
        }

        // 删除穿透码
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserCodeItem item)
            {
                Random rnd = new Random();
                string pinCode = rnd.Next(1000, 9999).ToString();

                var inputTextBox = new TextBox
                {
                    Margin = new Thickness(0, 10, 0, 0),
                    HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold
                };

                var errorTip = new TextBlock
                {
                    Text = "验证码错误，请重新输入",
                    Foreground = Brushes.OrangeRed,
                    FontSize = 12,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 0),
                    Visibility = Visibility.Collapsed
                };

                inputTextBox.TextChanged += (s, args) => errorTip.Visibility = Visibility.Collapsed;

                var loadingStack = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
                loadingStack.Children.Add(new Modern.ProgressRing { IsActive = true, Width = 20, Height = 20, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
                loadingStack.Children.Add(new TextBlock { Text = "正在从服务器删除...", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, FontSize = 12, Opacity = 0.7 });

                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"确定要彻底删除穿透码 [{item.codeID}] 吗？\n此操作不可撤销，会退回未使用天数的银币",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.Gray
                });

                var pinDisplay = new TextBlock
                {
                    Text = pinCode,
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 15, 0, 15),
                    Foreground = Brushes.Red
                };

                stackPanel.Children.Add(pinDisplay);
                stackPanel.Children.Add(inputTextBox);
                stackPanel.Children.Add(errorTip);
                stackPanel.Children.Add(loadingStack);

                var dialog = new Modern.ContentDialog
                {
                    Title = "删除穿透码",
                    Content = stackPanel,
                    PrimaryButtonText = "确认删除",
                    CloseButtonText = "取消",
                    DefaultButton = Modern.ContentDialogButton.Primary
                };

                dialog.Opened += (s, args) =>
                {
                    inputTextBox.Dispatcher.BeginInvoke(new Action(() => inputTextBox.Focus()),
                        System.Windows.Threading.DispatcherPriority.Input);
                };

                dialog.PrimaryButtonClick += async (s, args) =>
                {
                    var deferral = args.GetDeferral();
                    try
                    {
                        if (inputTextBox.Text.Trim() != pinCode)
                        {
                            errorTip.Text = "验证码错误，请重新输入";
                            errorTip.Visibility = Visibility.Visible;
                            inputTextBox.SelectAll();
                            inputTextBox.Focus();
                            args.Cancel = true;
                            return;
                        }

                        loadingStack.Visibility = Visibility.Visible;
                        inputTextBox.IsEnabled = false;
                        dialog.IsPrimaryButtonEnabled = false;

                        var res = await _api.PostWithTokenAsync<UserCodeListResponse>("UserCode/Delete", new
                        {
                            token = ApiService.CurrentToken,
                            codeID = item.codeID
                        });

                        if (res != null && res.success)
                        {
                            dialog.Hide();
                            await LoadCodesAsync();
                        }
                        else
                        {
                            errorTip.Text = res?.message ?? "删除失败";
                            errorTip.Visibility = Visibility.Visible;
                            loadingStack.Visibility = Visibility.Collapsed;
                            inputTextBox.IsEnabled = true;
                            dialog.IsPrimaryButtonEnabled = true;
                            args.Cancel = true;
                        }
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                };

                await dialog.ShowAsync();
            }
        }

        private async Task CreateTunnelIsolatedAsync(string uniqueName, UserCodeItem item, string lip, string lpt, string rpt, string proto)
        {
            try
            {
                string rootPath = AppDomain.CurrentDomain.BaseDirectory;
                string tunnelRootDir = Path.Combine(rootPath, "MoBoxFrp", "Tunnels", uniqueName);
                if (!Directory.Exists(tunnelRootDir)) Directory.CreateDirectory(tunnelRootDir);
                string serverAddr = item.node.Contains(".") ? item.node : $"{item.node}.moboxfrp.cn";
                string serverPort = string.IsNullOrEmpty(item.portServer) ? "7000" : item.portServer;
                
                // toml配置文件内容写入
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"# MoboxFrp 自动生成 - {DateTime.Now}");
                sb.AppendLine($"# ID = {item.codeID ?? "未知"}");
                sb.AppendLine($"serverAddr = \"{serverAddr}\"");
                sb.AppendLine($"serverPort = {serverPort}");
                sb.AppendLine($"auth.token = \"{item.token}\"");
                sb.AppendLine("");
                sb.AppendLine("[[proxies]]");
                sb.AppendLine($"name = \"{uniqueName}\""); // 使用带时间戳的名字
                sb.AppendLine($"type = \"{proto}\"");
                sb.AppendLine($"localIP = \"{lip}\"");
                sb.AppendLine($"localPort = {lpt.Trim()}");
                sb.AppendLine($"remotePort = {rpt.Trim()}");

                string configPath = Path.Combine(tunnelRootDir, "config.toml");
                await File.WriteAllTextAsync(configPath, sb.ToString(), new UTF8Encoding(false));

                // 复制 frpc.exe
                string sourceFrpc = Path.Combine(rootPath, "Resources", "frpc.exe");
                if (File.Exists(sourceFrpc))
                {
                    string targetFrpc = Path.Combine(tunnelRootDir, $"frpc_{uniqueName}.exe");
                    await Task.Run(() => File.Copy(sourceFrpc, targetFrpc, true));
                }
                else
                {
                    throw new FileNotFoundException($"找不到核心组件：{sourceFrpc}");
                }

                await new Modern.ContentDialog
                {
                    Title = "生成成功",
                    Content = $"隧道 [{uniqueName}] 已就绪。\n本地 {lpt} -> 远程 {rpt}",
                    PrimaryButtonText = "确定"
                }.ShowAsync();

                var newTunnel = new TunnelItem
                {
                    Name = uniqueName,
                    ConfigPath = configPath,
                    IsRunning = false
                };
                newTunnel.ParseConfig();
                App.GlobalTunnelList.Add(newTunnel);

            }
            catch (Exception ex)
            {
                await Modern.MessageBox.ShowAsync($"创建失败: {ex.Message}");
            }
        }

        // 隧道配置生成
        private async void Config_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.DataContext as UserCodeItem;
            if (item == null) return;

            if (!(this.Resources["TunnelConfigFormTemplate"] is DataTemplate template)) return;
            var contentInstance = template.LoadContent() as FrameworkElement;

            FindControl<System.Windows.Controls.TextBox>(contentInstance, "CfgID").Text = item.codeID;
            var hint = FindControl<TextBlock>(contentInstance, "RemotePortHint");
            if (hint != null) hint.Text = $"(可用范围: {item.portOpen})";

            var dialog = new Modern.ContentDialog
            {
                Title = "生成隧道配置",
                Content = contentInstance,
                PrimaryButtonText = "确认生成",
                CloseButtonText = "取消",
                DefaultButton = Modern.ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() == Modern.ContentDialogResult.Primary)
            {
                string nameInput = FindControl<System.Windows.Controls.TextBox>(contentInstance, "CfgName")?.Text;

                string baseName = string.IsNullOrWhiteSpace(nameInput) ? (item.codeID ?? "Tunnel") : nameInput;
                foreach (char c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');

                // 生成带时间戳的唯一名称，优化多开时候的冲突
                string uniqueName = $"{baseName}_{DateTime.Now:MMddHHmmss}";

                string localIP = FindControl<System.Windows.Controls.TextBox>(contentInstance, "LocalIP")?.Text ?? "127.0.0.1";
                string localPort = FindControl<System.Windows.Controls.TextBox>(contentInstance, "LocalPort")?.Text ?? "8080";
                string remotePort = FindControl<System.Windows.Controls.TextBox>(contentInstance, "RemotePort")?.Text;

                var pg = FindControl<StackPanel>(contentInstance, "ProtocolGroup");
                string protocol = pg?.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Content.ToString().ToLower() ?? "tcp";

                if (string.IsNullOrWhiteSpace(remotePort) || !IsPortInRange(remotePort, item.portOpen))
                {
                    await new Modern.ContentDialog
                    {
                        Title = "端口校验失败",
                        Content = $"端口 [{remotePort}] 不在开放的范围({item.portOpen})内，请重新输入。",
                        CloseButtonText = "返回修改"
                    }.ShowAsync();
                    return;
                }
                await CreateTunnelIsolatedAsync(uniqueName, item, localIP, localPort, remotePort, protocol);
            }
        }

        private T FindControl<T>(FrameworkElement parent, string name) where T : FrameworkElement
        {
            if (parent.Name == name) return (T)parent;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i) as FrameworkElement;
                if (child != null)
                {
                    var result = FindControl<T>(child, name);
                    if (result != null) return result;
                }
            }
            return null;
        }

        private bool IsPortInRange(string inputPort, string rangeStr)
        {
            if (string.IsNullOrWhiteSpace(inputPort) || string.IsNullOrWhiteSpace(rangeStr)) return false;
            if (!int.TryParse(inputPort.Trim(), out int userPort)) return false;
            try
            {
                if (rangeStr.Contains("-"))
                {
                    var parts = rangeStr.Split('-');
                    if (parts.Length == 2)
                    {
                        int start = int.Parse(parts[0].Trim());
                        int end = int.Parse(parts[1].Trim());
                        return userPort >= start && userPort <= end;
                    }
                }
            }
            catch { return false; }
            return false;
        }

        // 购买新码
        private async void CreateNewCode_Click(object sender, RoutedEventArgs e)
        {
            var nodeRes = await _api.PostWithTokenAsync<NodeListResponse>("Node/List");
            if (nodeRes == null || !nodeRes.success)
            {
                Modern.MessageBox.Show("获取节点列表失败，请检查网络");
                return;
            }

            var rootGrid = new Grid();
            var inputStack = new StackPanel { Width = 320, Visibility = Visibility.Visible };

            inputStack.Children.Add(new TextBlock { Text = "选择节点:", Margin = new Thickness(0, 5, 0, 5), FontWeight = FontWeights.Medium });
            var combo = new System.Windows.Controls.ComboBox
            {
                ItemsSource = nodeRes.nodes.FindAll(n => n.online == "true"),
                DisplayMemberPath = "name",
                SelectedValuePath = "nodeID",
                SelectedIndex = 0
            };
            inputStack.Children.Add(combo);

            var portHeaderGrid = new Grid { Margin = new Thickness(0, 15, 0, 0) };
            portHeaderGrid.Children.Add(new TextBlock { Text = "指定开放端口 (选填):", HorizontalAlignment = System.Windows.HorizontalAlignment.Left });
            var portRangeTxt = new TextBlock { Text = "范围: --", HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Opacity = 0.6, FontSize = 11 };
            portHeaderGrid.Children.Add(portRangeTxt);
            inputStack.Children.Add(portHeaderGrid);
            var portInput = new System.Windows.Controls.TextBox { Text = "", Margin = new Thickness(0, 5, 0, 0) };
            inputStack.Children.Add(portInput);

            var bandHeaderGrid = new Grid { Margin = new Thickness(0, 15, 0, 0) };
            bandHeaderGrid.Children.Add(new TextBlock { Text = "选择带宽 (Mbps):", HorizontalAlignment = System.Windows.HorizontalAlignment.Left });
            var bandValueTxt = new TextBlock { Text = "1 Mbps", HorizontalAlignment = System.Windows.HorizontalAlignment.Right, FontWeight = FontWeights.Bold, Foreground = Brushes.DodgerBlue };
            bandHeaderGrid.Children.Add(bandValueTxt);
            inputStack.Children.Add(bandHeaderGrid);
            var bandSlider = new Slider { Minimum = 1, Maximum = 100, Value = 1, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(0, 5, 0, 0) };
            inputStack.Children.Add(bandSlider);

            inputStack.Children.Add(new TextBlock { Text = "购买时长 (天):", Margin = new Thickness(0, 15, 0, 5) });
            var dayInput = new System.Windows.Controls.TextBox { Text = "3" };
            inputStack.Children.Add(dayInput);

            var priceBorder = new Border
            {
                Margin = new Thickness(0, 20, 0, 0),
                Padding = new Thickness(10),
                Background = (Brush)new BrushConverter().ConvertFrom("#11000000"),
                CornerRadius = new CornerRadius(4)
            };
            var priceTxt = new TextBlock { Text = "预计总价: 计算中...", HorizontalAlignment = System.Windows.HorizontalAlignment.Center, FontWeight = FontWeights.SemiBold };
            priceBorder.Child = priceTxt;
            inputStack.Children.Add(priceBorder);

            var loadingStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            var progressRing = new Modern.ProgressRing
            {
                IsActive = true,
                Width = 60,
                Height = 60,
                Margin = new Thickness(0, 20, 0, 20)
            };
            var statusTxt = new TextBlock
            {
                Text = "正在提交请求...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                FontSize = 14,
                Opacity = 0.8
            };
            loadingStack.Children.Add(progressRing);
            loadingStack.Children.Add(statusTxt);

            rootGrid.Children.Add(inputStack);
            rootGrid.Children.Add(loadingStack);

            Action updateInfo = () => {
                if (combo.SelectedItem is NodeItem node)
                {
                    portRangeTxt.Text = $"范围: {node.portStart?.ToString()}-{node.portEnd?.ToString()}";
                    if (double.TryParse(node.price?.ToString(), out double p) && int.TryParse(dayInput.Text, out int d))
                    {
                        double total = p * (int)bandSlider.Value * d;
                        string coinName = node.coin == "gold" ? "金币" : "银币";
                        priceTxt.Text = $"预计消耗: {total} {coinName}";
                    }
                }
            };

            combo.SelectionChanged += (s, ev) => updateInfo();
            bandSlider.ValueChanged += (s, ev) => { bandValueTxt.Text = $"{(int)bandSlider.Value} Mbps"; updateInfo(); };
            dayInput.TextChanged += (s, ev) => updateInfo();
            updateInfo();

            var dialog = new Modern.ContentDialog
            {
                Title = "创建新的穿透码",
                Content = rootGrid,
                PrimaryButtonText = "立即创建",
                CloseButtonText = "取消",
                DefaultButton = Modern.ContentDialogButton.Primary
            };

            dialog.PrimaryButtonClick += async (s, args) =>
            {
                var deferral = args.GetDeferral();
                inputStack.Visibility = Visibility.Collapsed;
                loadingStack.Visibility = Visibility.Visible;
                dialog.IsPrimaryButtonEnabled = false;
                dialog.IsSecondaryButtonEnabled = false;

                try
                {
                    var createReq = new Dictionary<string, object> {
                        { "token", ApiService.CurrentToken },
                        { "node", combo.SelectedValue?.ToString() },
                        { "band", ((int)bandSlider.Value).ToString() },
                        { "day", dayInput.Text }
                    };
                    if (!string.IsNullOrWhiteSpace(portInput.Text)) createReq.Add("port", portInput.Text);

                    var res = await _api.PostWithTokenAsync<UserCodeListResponse>("UserCode/Create", createReq);

                    if (res != null && res.success)
                    {
                        statusTxt.Text = "创建成功！正在刷新列表...";
                        progressRing.Visibility = Visibility.Collapsed;
                        await Task.Delay(1200);
                        await LoadCodesAsync();
                        dialog.Hide();
                    }
                    else
                    {
                        Modern.MessageBox.Show(res?.message ?? "创建失败，请检查余额或参数");
                        inputStack.Visibility = Visibility.Visible;
                        loadingStack.Visibility = Visibility.Collapsed;
                        dialog.IsPrimaryButtonEnabled = true;
                        dialog.IsSecondaryButtonEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    Modern.MessageBox.Show("发生错误：" + ex.Message);
                }
                finally
                {
                    deferral.Complete();
                }
            };

            await dialog.ShowAsync();
        }
    }
}