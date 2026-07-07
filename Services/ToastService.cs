using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MoboxFrpGUI.Services
{
    public static class ToastService
    {
        public static bool IsSupported { get; private set; } = false;

        public static Action<string>? OnTunnelToastClicked { get; set; }

        public static void Initialize()
        {
            try
            {
                ToastNotificationManagerCompat.History.Clear();
                ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
                IsSupported = true;
                Debug.WriteLine("[ToastService] 初始化成功");
            }
            catch (Exception ex)
            {
                IsSupported = false;
                Debug.WriteLine($"[ToastService] 初始化异常: {ex.Message}");
            }
        }

        private static void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            try
            {
                string arg = e.Argument ?? "";
                Debug.WriteLine($"[ToastService] Toast 激活，参数: '{arg}'");

                string tunnelName = "";
                if (arg.StartsWith("tunnel:"))
                {
                    tunnelName = arg.Substring("tunnel:".Length);
                    Debug.WriteLine($"[ToastService] 解析出隧道名称: '{tunnelName}'");
                }
                else
                {
                    Debug.WriteLine($"[ToastService] 参数格式不正确: '{arg}'");
                }

                if (!string.IsNullOrEmpty(tunnelName))
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Debug.WriteLine($"[ToastService] 调用 OnTunnelToastClicked");
                        OnTunnelToastClicked?.Invoke(tunnelName);
                    });
                }
                else
                {
                    Debug.WriteLine($"[ToastService] 未找到隧道名称");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToastService] Toast 激活异常: {ex.Message}");
            }
        }

        public static void ShowTunnelStarted(string tunnelName)
        {
            Debug.WriteLine($"[ToastService] ShowTunnelStarted 调用: {tunnelName}, IsSupported = {IsSupported}");
            if (!IsSupported) return;

            try
            {
                string xml = $@"<toast launch=""tunnel:{tunnelName}"">
                    <visual>
                        <binding template=""ToastGeneric"">
                            <text>隧道已启动</text>
                            <text>{tunnelName} 正在运行</text>
                        </binding>
                    </visual>
                </toast>";

                var doc = new Windows.Data.Xml.Dom.XmlDocument();
                doc.LoadXml(xml);

                var toast = new Windows.UI.Notifications.ToastNotification(doc);
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);

                Debug.WriteLine("[ToastService] Toast 已显示");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToastService] ShowTunnelStarted 异常: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public static void ShowTunnelStoppedUnexpected(string tunnelName)
        {
            Debug.WriteLine($"[ToastService] ShowTunnelStoppedUnexpected 调用: {tunnelName}, IsSupported = {IsSupported}");
            if (!IsSupported) return;

            try
            {
                string xml = $@"<toast launch=""tunnel:{tunnelName}"">
                    <visual>
                        <binding template=""ToastGeneric"">
                            <text>隧道意外停止</text>
                            <text>{tunnelName} 已停止运行，点击查看日志</text>
                        </binding>
                    </visual>
                </toast>";

                var doc = new Windows.Data.Xml.Dom.XmlDocument();
                doc.LoadXml(xml);

                var toast = new Windows.UI.Notifications.ToastNotification(doc);
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);

                Debug.WriteLine("[ToastService] Toast 已显示");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToastService] ShowTunnelStoppedUnexpected 异常: {ex.GetType().Name} - {ex.Message}");
            }
        }

        public static void ShowTunnelError(string tunnelName, string errorMessage)
        {
            Debug.WriteLine($"[ToastService] ShowTunnelError 调用: {tunnelName}, error = {errorMessage}, IsSupported = {IsSupported}");
            if (!IsSupported) return;

            try
            {
                string truncatedError = errorMessage.Length > 60 ? errorMessage.Substring(0, 60) + "..." : errorMessage;
                string xml = $@"<toast launch=""tunnel:{tunnelName}"">
                    <visual>
                        <binding template=""ToastGeneric"">
                            <text>隧道启动失败</text>
                            <text>{tunnelName}: {truncatedError}</text>
                        </binding>
                    </visual>
                </toast>";

                var doc = new Windows.Data.Xml.Dom.XmlDocument();
                doc.LoadXml(xml);

                var toast = new Windows.UI.Notifications.ToastNotification(doc);
                ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);

                Debug.WriteLine("[ToastService] Toast 已显示");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToastService] ShowTunnelError 异常: {ex.GetType().Name} - {ex.Message}");
            }
        }
    }
}
