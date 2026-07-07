using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using xa = iNKORE.UI.WPF.Controls;
using WpfApplication = System.Windows.Application;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfCursors = System.Windows.Input.Cursors;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfPanel = System.Windows.Controls.Panel;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace MoboxFrpGUI.Services
{
    public enum NotificationType
    {
        Info,
        Warning,
        Error
    }

    public static class NotificationService
    {
        private const double ToastWidth = 384;
        private static readonly Duration SlideInDuration = TimeSpan.FromMilliseconds(350);
        private static readonly Duration SlideOutDuration = TimeSpan.FromMilliseconds(250);

        public static xa.SimpleStackPanel AlertPanel { get; set; }

        public static void NavigateToSettings()
        {
            try
            {
                if (WpfApplication.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.NavigateToSettings();
                }
            }
            catch { }
        }

        public static void Show(string message, NotificationType type = NotificationType.Info, int durationMs = 5000)
        {
            if (AlertPanel == null) return;

            App.LastExceptionReport = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}: {message}\n\n这是一个测试通知，用于验证异常处理系统是否正常工作。";

            AlertPanel.Dispatcher.Invoke(() =>
            {
                try
                {
                    var card = CreateCard(message, type, NavigateToSettings, false);
                    AlertPanel.Children.Add(card);
                    AnimateIn(card);

                    if (durationMs > 0)
                    {
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                        timer.Tick += (s, e) =>
                        {
                            timer.Stop();
                            AnimateOut(card, () => RemoveCard(AlertPanel, card));
                        };
                        timer.Start();
                    }
                }
                catch { }
            });
        }

        public static void ShowPersistent(string message, NotificationType type, Action onClick)
        {
            if (AlertPanel == null) return;

            App.LastExceptionReport = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}: {message}\n\n这是一个测试通知，用于验证异常处理系统是否正常工作。";

            AlertPanel.Dispatcher.Invoke(() =>
            {
                try
                {
                    var card = CreateCard(message, type, onClick, true);
                    AlertPanel.Children.Add(card);
                    AnimateIn(card);
                }
                catch { }
            });
        }

        private static void AnimateIn(Border card)
        {
            var transform = new TranslateTransform(420, 0);
            card.RenderTransform = transform;
            card.RenderTransformOrigin = new WpfPoint(0.5, 0.5);

            var anim = new DoubleAnimation
            {
                To = 0,
                Duration = SlideInDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private static void AnimateOut(Border card, Action onComplete)
        {
            var anim = new DoubleAnimation
            {
                To = 420,
                Duration = SlideOutDuration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            anim.Completed += (s, e) => onComplete?.Invoke();

            if (card.RenderTransform is TranslateTransform transform)
                transform.BeginAnimation(TranslateTransform.XProperty, anim);
            else
                onComplete?.Invoke();
        }

        private static Border CreateCard(string message, NotificationType type, Action onClick, bool persistent)
        {
            var typeColors = GetTypeColors(type);
            var fg = GetThemeBrush("TextFillColorPrimaryBrush", 255, 255, 255);
            var secFg = GetThemeBrush("TextFillColorSecondaryBrush", 156, 156, 156);
            var bg = GetThemeBrush("CardBackgroundFillColorDefaultBrush", 44, 44, 44);
            var borderBrush = GetThemeBrush("CardStrokeColorDefaultBrush", 68, 68, 68);
            var subtleBg = new SolidColorBrush(typeColors.accent) { Opacity = 0.18 };

            var card = new Border
            {
                Width = ToastWidth,
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                BorderBrush = borderBrush,
                Background = bg,
                Padding = new Thickness(0),
                Cursor = onClick != null ? WpfCursors.Hand : WpfCursors.Arrow,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 3,
                    Opacity = 0.15,
                    Direction = 270
                }
            };

            var root = new Grid();

            var tint = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = subtleBg,
                IsHitTestVisible = false
            };
            root.Children.Add(tint);

            var stripe = new WpfRectangle
            {
                Width = 2,
                Fill = new SolidColorBrush(typeColors.accent),
                HorizontalAlignment = WpfHorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Margin = new Thickness(0, 8, 0, 8)
            };
            root.Children.Add(stripe);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (persistent)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(typeColors.accent) { Opacity = 0.12 },
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(14, 13, 0, 0)
            };

            var iconText = new TextBlock
            {
                Text = typeColors.glyph,
                FontSize = 14,
                Foreground = new SolidColorBrush(typeColors.accent),
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = iconText;
            Grid.SetColumn(iconBorder, 0);
            grid.Children.Add(iconBorder);

            var textPanel = new StackPanel
            {
                Margin = new Thickness(12, 12, persistent ? 8 : 18, 12),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(typeColors.label))
            {
                var titleText = new TextBlock
                {
                    Text = typeColors.label,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = fg,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                textPanel.Children.Add(titleText);
            }

            var msgText = new TextBlock
            {
                Text = message,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = ToastWidth - 110,
                Foreground = secFg,
                LineHeight = 18
            };
            textPanel.Children.Add(msgText);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            if (persistent)
            {
                var closeBtn = new WpfButton
                {
                    Content = "✕",
                    FontSize = 12,
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(0, 8, 8, 0),
                    Padding = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = WpfHorizontalAlignment.Right,
                    Background = WpfBrushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = secFg,
                    Opacity = 0.6,
                    ToolTip = "关闭"
                };
                closeBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    AnimateOut(card, () => RemoveCardFromParent(card));
                };
                Grid.SetColumn(closeBtn, 2);
                grid.Children.Add(closeBtn);
            }

            root.Children.Add(grid);
            card.Child = root;

            if (onClick != null)
            {
                card.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.OriginalSource is WpfButton)
                        return;
                    e.Handled = true;
                    onClick?.Invoke();
                };
            }

            return card;
        }

        private static (WpfColor accent, string glyph, string label) GetTypeColors(NotificationType type)
        {
            return type switch
            {
                NotificationType.Warning => (WpfColor.FromRgb(255, 193, 7), "⚠", "警告"),
                NotificationType.Error => (WpfColor.FromRgb(243, 78, 78), "✕", "错误"),
                _ => (WpfColor.FromRgb(0, 150, 230), "●", "提示")
            };
        }

        private static WpfBrush GetThemeBrush(string key, byte r, byte g, byte b)
        {
            return WpfApplication.Current.TryFindResource(key) as WpfBrush
                   ?? new SolidColorBrush(WpfColor.FromRgb(r, g, b));
        }

        private static void RemoveCard(WpfPanel container, Border card)
        {
            try { if (container.Children.Contains(card)) container.Children.Remove(card); } catch { }
        }

        private static void RemoveCardFromParent(Border card)
        {
            try { if (card.Parent is WpfPanel p) p.Children.Remove(card); } catch { }
        }
    }
}
