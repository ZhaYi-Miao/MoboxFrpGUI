using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;

namespace MoboxFrpGUI.Models
{
    public class TunnelItem : INotifyPropertyChanged
    {
        private string _name;
        private string _node = "null";
        private string _portOpen = "未分配";
        private string _status = "已停止";
        private string _configPath;
        private bool _isRunning;
        private string _fullLogText = "";
        private Process _process;
        private string _remoteAddress = "未获取";
        private string _protocol = "TCP";
        private string _localAddress = "127.0.0.1:0";
        private bool _hasLog;
        private string _id = "暂未获取到id";

        // 线程安全相关
        private readonly object _processLock = new object();
        private readonly object _logLock = new object();
        private int? _runningPid;

        // 高性能日志缓冲区
        private readonly List<string> _logBuffer = new List<string>();
        private const int MaxLogLines = 500;

        public string ID { get => _id; set { _id = value; OnPropertyChanged(); } }
        public bool HasLog { get => _hasLog; set { _hasLog = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Node { get => _node; set { _node = value; OnPropertyChanged(); } }
        public string PortOpen { get => _portOpen; set { _portOpen = value; OnPropertyChanged(); } }
        public string ConfigPath { get => _configPath; set { _configPath = value; OnPropertyChanged(); } }
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }
        public string FullLogText { get => _fullLogText; set { _fullLogText = value; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        public string RemoteAddress { get => _remoteAddress; set { _remoteAddress = value; OnPropertyChanged(); } }
        public string Protocol { get => _protocol; set { _protocol = value; OnPropertyChanged(); } }
        public string LocalAddress { get => _localAddress; set { _localAddress = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));



        #region 配置解析
        public void ParseConfig()
        {
            if (!File.Exists(ConfigPath)) return;

            try
            {
                string content = File.ReadAllText(ConfigPath);

                // 读取服务器地址
                var serverMatch = Regex.Match(content, @"serverAddr\s*=\s*""([^""]+)""");
                string server = serverMatch.Success ? serverMatch.Groups[1].Value : "未读取到服务器地址";

                // 隧道对应的穿透码id
                var idMatch = Regex.Match(content, @"#\s*ID\s*=\s*([^\r\n]+)");
                ID = idMatch.Success ? idMatch.Groups[1].Value.Trim() : "无ID";

                // 协议
                var typeMatch = Regex.Match(content, @"type\s*=\s*""([^""]+)""");
                Protocol = typeMatch.Success ? typeMatch.Groups[1].Value.ToUpper() : "TCP";

                // 本地端口
                var localIpMatch = Regex.Match(content, @"localIP\s*=\s*""([^""]+)""");
                var localPortMatch = Regex.Match(content, @"localPort\s*=\s*(\d+)");
                string lip = localIpMatch.Success ? localIpMatch.Groups[1].Value : "127.0.0.1";
                string lpt = localPortMatch.Success ? localPortMatch.Groups[1].Value : "0";
                LocalAddress = $"{lip}:{lpt}";

                // 远程端口 + 输出地址
                var remotePortMatch = Regex.Match(content, @"remotePort\s*=\s*(\d+)");
                if (remotePortMatch.Success)
                {
                    RemoteAddress = $"{server}:{remotePortMatch.Groups[1].Value}";
                }
                else
                {
                    RemoteAddress = server;
                }
            }
            catch (Exception ex)
            {
                RemoteAddress = "解析出错";
                Debug.WriteLine($"解析配置失败: {ex.Message}");
            }
        }

        #endregion

        #region 隧道控制

        // 启动隧道
        public void Start()
        {
            lock (_processLock)
            {
                if (IsRunning || _process != null) return;
                ParseConfig();

                string folder = Path.GetDirectoryName(ConfigPath);
                if (string.IsNullOrEmpty(folder)) return;

                string privateExe = Path.Combine(folder, $"frpc_{Name}.exe");
                string publicExe = Path.Combine(folder, "frpc.exe");
                string exePath = File.Exists(privateExe) ? privateExe : (File.Exists(publicExe) ? publicExe : null);

                if (exePath == null)
                {
                    AppendLog($"[错误] 找不到执行程序！");
                    return;
                }

                try
                {
                    _process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"-c \"{ConfigPath}\"",
                            WorkingDirectory = folder,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8
                        },
                        EnableRaisingEvents = true
                    };

                    _process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog(e.Data); };
                    _process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) AppendLog($"[错误] {e.Data}"); };
                    _process.Exited += (s, e) =>
                    {
                        lock (_processLock)
                        {
                            IsRunning = false;
                            Status = "已停止";
                            _runningPid = null;

                            try
                            {
                                if (_process != null)
                                {
                                    _process.CancelOutputRead();
                                    _process.CancelErrorRead();
                                    _process.Dispose();
                                }
                            }
                            catch { }

                            _process = null;
                        }
                    };

                    if (_process.Start())
                    {
                        _runningPid = _process.Id;
                        _process.BeginOutputReadLine();
                        _process.BeginErrorReadLine();
                        IsRunning = true;
                        Status = "运行中";
                        AppendLog($"[信息] 隧道已启动，PID: {_runningPid}");
                    }
                }
                catch (Exception ex)
                {
                    IsRunning = false;
                    Status = "启动失败";
                    AppendLog($"[ERROR] {ex.Message}");

                    // 清理进程对象
                    try
                    {
                        if (_process != null)
                        {
                            _process.Dispose();
                            _process = null;
                        }
                    }
                    catch { }
                }
            }
        }

        // 停止隧道
        public void Stop()
        {
            lock (_processLock)
            {
                if (_process == null || !_runningPid.HasValue)
                {
                    ResetUIStatus();
                    return;
                }

                int pidToKill = _runningPid.Value;
                Status = "正在停止...";
            }

            // 在后台线程中执行停止操作
            Task.Run(() =>
            {
                Process processToStop = null;
                int? pidToKill = null;

                lock (_processLock)
                {
                    processToStop = _process;
                    pidToKill = _runningPid;
                }

                if (processToStop != null && pidToKill.HasValue)
                {
                    try
                    {
                        // 先尝试优雅关闭
                        processToStop.EnableRaisingEvents = false;
                        processToStop.CancelOutputRead();
                        processToStop.CancelErrorRead();

                        // 尝试正常关闭进程
                        processToStop.CloseMainWindow();

                        // 等待最多3秒让进程自己退出
                        if (!processToStop.WaitForExit(3000))
                        {
                            // 如果进程还未退出，使用Kill强制结束
                            processToStop.Kill();
                            processToStop.WaitForExit(2000);
                        }

                        Debug.WriteLine($"[停止] 已优雅关闭 PID: {pidToKill}");
                    }
                    catch (Exception ex)
                    {
                        // 如果优雅关闭失败，使用taskkill作为最后手段
                        try
                        {
                            var startInfo = new ProcessStartInfo
                            {
                                FileName = "taskkill",
                                Arguments = $"/F /T /PID {pidToKill}",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };

                            using (var killer = Process.Start(startInfo))
                            {
                                killer?.WaitForExit(2000);
                            }

                            Debug.WriteLine($"[停止] 已使用 taskkill 强制终止 PID: {pidToKill}");
                        }
                        catch (Exception killEx)
                        {
                            Debug.WriteLine($"[停止] 强制终止失败: {killEx.Message}");
                        }
                    }
                }

                // 在UI线程中更新状态
                App.Current.Dispatcher.Invoke(() => ResetUIStatus());
            });
        }

        private void ResetUIStatus()
        {
            lock (_processLock)
            {
                try
                {
                    if (_process != null)
                    {
                        _process.Dispose();
                    }
                }
                catch { }

                _process = null;
                _runningPid = null;
                IsRunning = false;
                Status = "已停止";
                AppendLog("[信息] 隧道已停止。");
            }
        }

        // 日志相关显示盒清除
        private void AppendLog(string message)
        {
            lock (_logLock)
            {
                if (!HasLog) HasLog = true;
                string newLog = $"[{DateTime.Now:HH:mm:ss}] {message}";

                // 高性能日志缓冲区管理
                _logBuffer.Add(newLog);

                // 保持最多500行日志
                if (_logBuffer.Count > MaxLogLines)
                {
                    _logBuffer.RemoveAt(0);
                }

                // 使用StringBuilder提高性能，避免频繁的字符串拼接
                var builder = new StringBuilder();
                for (int i = 0; i < _logBuffer.Count; i++)
                {
                    builder.AppendLine(_logBuffer[i]);
                }

                FullLogText = builder.ToString();
            }
        }

        public void ClearLog()
        {
            lock (_logLock)
            {
                _logBuffer.Clear();
                FullLogText = "";
                HasLog = false;
            }
        }

        #endregion
    }
}