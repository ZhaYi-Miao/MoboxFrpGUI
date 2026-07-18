using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iNKORE.UI.WPF.Modern.Controls;
using Modern = iNKORE.UI.WPF.Modern.Controls;
using MoboxFrpGUI.Models;
using MoboxFrpGUI.Helpers;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace MoboxFrpGUI
{
    public partial class LogsPage : Modern.Page
    {
        private TunnelItem? _selectedTunnel;
        private int _lastLogCount = 0;
        private readonly WpfSolidColorBrush _defaultForeground = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        private string _searchKeyword = "";
        private string _originalFullLog = "";

        public LogsPage()
        {
            InitializeComponent();
            App.GlobalTunnelList.CollectionChanged += GlobalTunnelList_CollectionChanged;
            Loaded += (s, e) => {
                UpdateEmptyHint();

                if (!string.IsNullOrEmpty(App.PendingToastTunnelName))
                {
                    string name = App.PendingToastTunnelName;
                    App.PendingToastTunnelName = null;
                    SelectTunnelByName(name);
                }
                else
                {
                    TrySelectFirstTunnel();
                }
            };
        }

        private void GlobalTunnelList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                if (_selectedTunnel != null && e.OldItems != null)
                {
                    foreach (var oldItem in e.OldItems)
                    {
                        if (oldItem is TunnelItem t && t == _selectedTunnel)
                        {
                            _selectedTunnel.PropertyChanged -= CurrentTunnel_PropertyChanged;
                            _selectedTunnel = null;
                            LogRichTextBox.Document.Blocks.Clear();
                            var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };
                            LogRichTextBox.Document.Blocks.Add(para);
                            _lastLogCount = 0;
                            UpdateEmptyHint();
                            CurrentTunnelName.Text = "未选择隧道";
                            break;
                        }
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (_selectedTunnel != null)
                {
                    _selectedTunnel.PropertyChanged -= CurrentTunnel_PropertyChanged;
                    _selectedTunnel = null;
                }
                LogRichTextBox.Document.Blocks.Clear();
                var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };
                LogRichTextBox.Document.Blocks.Add(para);
                _lastLogCount = 0;
                UpdateEmptyHint();
                CurrentTunnelName.Text = "未选择隧道";
            }
        }

        private void TrySelectFirstTunnel()
        {
            if (_selectedTunnel != null) return;
            if (App.GlobalTunnelList == null) return;

            var firstTunnel = App.GlobalTunnelList.FirstOrDefault(t => t.IsRunning || t.HasLog);
            if (firstTunnel != null)
            {
                SelectTunnel(firstTunnel);
            }
        }

        private void TunnelItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is TunnelItem tunnel)
            {
                SelectTunnel(tunnel);
            }
        }

        private void SelectTunnel(TunnelItem tunnel)
        {
            if (_selectedTunnel == tunnel) return;

            if (_selectedTunnel != null)
            {
                _selectedTunnel.PropertyChanged -= CurrentTunnel_PropertyChanged;
            }

            _selectedTunnel = tunnel;
            _selectedTunnel.PropertyChanged += CurrentTunnel_PropertyChanged;

            CurrentTunnelName.Text = tunnel.Name;
            LogContentBorder.Visibility = Visibility.Visible;
            EmptyHint.Visibility = Visibility.Collapsed;

            RenderFullLog(tunnel.FullLogText);
            _lastLogCount = CountLines(tunnel.FullLogText);

            HighlightSelectedTunnel();
        }

        private int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return text.Split('\n').Length;
        }

        private void RenderFullLog(string fullLog)
        {
            _originalFullLog = fullLog;
            LogRichTextBox.Document.Blocks.Clear();

            if (string.IsNullOrEmpty(fullLog))
            {
                var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };
                LogRichTextBox.Document.Blocks.Add(para);
                return;
            }

            string displayLog = FilterLogByKeyword(fullLog, _searchKeyword);
            AnsiColorParser.AppendColoredText(LogRichTextBox.Document, displayLog, _defaultForeground);
            ScrollToEnd();
        }

        private string FilterLogByKeyword(string fullLog, string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return fullLog;

            var lines = fullLog.Split('\n');
            var filteredLines = lines.Where(line => line.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            return string.Join("\n", filteredLines);
        }

        private void CurrentTunnel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TunnelItem.FullLogText) && _selectedTunnel != null)
            {
                string fullText = _selectedTunnel.FullLogText;
                _originalFullLog = fullText;
                int currentCount = CountLines(fullText);

                if (currentCount > _lastLogCount)
                {
                    int newLineCount = currentCount - _lastLogCount;
                    string[] lines = fullText.Split('\n');
                    int startIdx = lines.Length - newLineCount;
                    if (startIdx < 0) startIdx = 0;

                    var newLines = new string[newLineCount];
                    for (int i = 0; i < newLineCount; i++)
                    {
                        newLines[i] = lines[startIdx + i];
                    }
                    string newText = string.Join("\n", newLines);

                    string displayText = newText;
                    if (!string.IsNullOrEmpty(_searchKeyword))
                    {
                        displayText = FilterLogByKeyword(newText, _searchKeyword);
                    }

                    if (!string.IsNullOrEmpty(displayText))
                    {
                        LogRichTextBox.Dispatcher.Invoke(() =>
                        {
                            AnsiColorParser.AppendColoredText(LogRichTextBox.Document, displayText, _defaultForeground);
                            ScrollToEnd();
                        });
                    }

                    _lastLogCount = currentCount;
                }
            }
        }

        private void ScrollToEnd()
        {
            try
            {
                if (LogRichTextBox.IsFocused) return;
                LogRichTextBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
                {
                    try
                    {
                        LogRichTextBox.ScrollToEnd();
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void HighlightSelectedTunnel()
        {
            var containers = GetTunnelItemBorders();
            foreach (var border in containers)
            {
                if (border.Tag is TunnelItem tunnel)
                {
                    if (tunnel == _selectedTunnel)
                    {
                        border.Background = new WpfSolidColorBrush(WpfColor.FromArgb(25, 0, 120, 212));
                        border.BorderBrush = new WpfSolidColorBrush(WpfColor.FromArgb(60, 0, 120, 212));
                        border.BorderThickness = new Thickness(1);
                    }
                    else
                    {
                        border.Background = WpfBrushes.Transparent;
                        border.BorderBrush = WpfBrushes.Transparent;
                        border.BorderThickness = new Thickness(0);
                    }
                }
            }
        }

        private System.Collections.Generic.List<Border> GetTunnelItemBorders()
        {
            var result = new System.Collections.Generic.List<Border>();
            FindVisualChildren(TunnelListItemsControl, result);
            return result;
        }

        private void FindVisualChildren(DependencyObject parent, System.Collections.Generic.List<Border> result)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Border border && border.Name == "TunnelItem")
                {
                    result.Add(border);
                }
                FindVisualChildren(child, result);
            }
        }

        public void UpdateEmptyHint()
        {
            if (App.GlobalTunnelList == null) return;

            bool hasVisibleTunnels = App.GlobalTunnelList.Any(t => t.IsRunning || t.HasLog);
            if (!hasVisibleTunnels)
            {
                _selectedTunnel = null;
                LogContentBorder.Visibility = Visibility.Collapsed;
                EmptyHint.Visibility = Visibility.Visible;
                CurrentTunnelName.Text = "未选择隧道";
            }
        }

        public void SelectTunnelByName(string tunnelName)
        {
            if (string.IsNullOrEmpty(tunnelName)) return;
            if (App.GlobalTunnelList == null) return;

            var tunnel = App.GlobalTunnelList.FirstOrDefault(t => t.Name == tunnelName);
            if (tunnel != null)
            {
                SelectTunnel(tunnel);
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTunnel != null)
            {
                _selectedTunnel.ClearLog();
                LogRichTextBox.Document.Blocks.Clear();
                var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };
                LogRichTextBox.Document.Blocks.Add(para);
                _lastLogCount = 0;
                _originalFullLog = "";
                UpdateEmptyHint();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchKeyword = SearchTextBox.Text.Trim();

            if (_selectedTunnel != null)
            {
                RenderFullLog(_selectedTunnel.FullLogText);
                _lastLogCount = CountLines(_selectedTunnel.FullLogText);
            }
        }
    }
}
