using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoboxFrpGUI.Services;

namespace MoboxFrpGUI.Models
{
    public enum TunnelStatus
    {
        Stopped,
        Starting,
        Running,
        Error
    }

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

        private TunnelStatus _tunnelStatus = TunnelStatus.Stopped;
        private System.Threading.Timer _startupTimer;
        private const int StartupTimeoutSeconds = 15;
        private bool _startupCompleted = false;

        // 线程安全相关
        private readonly object _processLock = new object();
        private readonly object _logLock = new object();
        private int? _runningPid;
        private bool _isStoppingByUser = false;

        // 高性能日志缓冲区
        private readonly List<string> _logBuffer = new List<string>();
        private const int MaxLogLines = 500;

        // 日志实时持久化（定时器）
        private System.Threading.Timer _runningLogTimer;
        private bool _runningLogDirty = false;

        public string ID { get => _id; set { _id = value; OnPropertyChanged(); } }
        public bool HasLog { get => _hasLog; set { _hasLog = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Node { get => _node; set { _node = value; OnPropertyChanged(); } }
        public string PortOpen { get => _portOpen; set { _portOpen = value; OnPropertyChanged(); } }
        public string ConfigPath { get => _configPath; set { _configPath = value; OnPropertyChanged(); } }
        public bool IsRunning { get => _isRunning; set { _isRunning = value; OnPropertyChanged(); } }
        public string FullLogText { get => _fullLogText; set { _fullLogText = value; OnPropertyChanged(); } }
        public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

        public TunnelStatus TunnelStatus
        {
            get => _tunnelStatus;
            set
            {
                _tunnelStatus = value;
                OnPropertyChanged();
            }
        }

        public string RemoteAddress { get => _remoteAddress; set { _remoteAddress = value; OnPropertyChanged(); } }
        public string Protocol { get => _protocol; set { _protocol = value; OnPropertyChanged(); } }
        public string LocalAddress { get => _localAddress; set { _localAddress = value; OnPropertyChanged(); } }

        private string _nodeId = "未获取";
        private string _token = "未获取";
        private string _localIP = "127.0.0.1";
        private string _localPort = "0";
        private string _serverAddr = "未获取";
        private string _serverPort = "0";

        public string NodeId { get => _nodeId; set { _nodeId = value; OnPropertyChanged(); } }
        public string Token { get => _token; set { _token = value; OnPropertyChanged(); } }
        public string LocalIP { get => _localIP; set { _localIP = value; OnPropertyChanged(); } }
        public string LocalPort { get => _localPort; set { _localPort = value; OnPropertyChanged(); } }
        public string ServerAddr { get => _serverAddr; set { _serverAddr = value; OnPropertyChanged(); } }
        public string ServerPort { get => _serverPort; set { _serverPort = value; OnPropertyChanged(); } }

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
                ServerAddr = serverMatch.Success ? serverMatch.Groups[1].Value : "未读取到服务器地址";

                // 服务器端口
                var serverPortMatch = Regex.Match(content, @"serverPort\s*=\s*(\d+)");
                ServerPort = serverPortMatch.Success ? serverPortMatch.Groups[1].Value : "0";

                // 节点编号（从 serverAddr 中提取，如 bj2.moboxfrp.cn -> bj2）
                if (!string.IsNullOrEmpty(ServerAddr))
                {
                    int dotIndex = ServerAddr.IndexOf('.');
                    if (dotIndex > 0)
                    {
                        NodeId = ServerAddr.Substring(0, dotIndex);
                    }
                    else
                    {
                        NodeId = ServerAddr;
                    }
                }

                // 穿透码（token）
                var tokenMatch = Regex.Match(content, @"auth\.token\s*=\s*""([^""]+)""");
                Token = tokenMatch.Success ? tokenMatch.Groups[1].Value : "未获取";

                // 隧道对应的穿透码id
                var idMatch = Regex.Match(content, @"#\s*ID\s*=\s*([^\r\n]+)");
                ID = idMatch.Success ? idMatch.Groups[1].Value.Trim() : "无ID";

                // 协议
                var typeMatch = Regex.Match(content, @"type\s*=\s*""([^""]+)""");
                Protocol = typeMatch.Success ? typeMatch.Groups[1].Value.ToUpper() : "TCP";

                // 本地IP
                var localIpMatch = Regex.Match(content, @"localIP\s*=\s*""([^""]+)""");
                LocalIP = localIpMatch.Success ? localIpMatch.Groups[1].Value : "127.0.0.1";

                // 本地端口
                var localPortMatch = Regex.Match(content, @"localPort\s*=\s*(\d+)");
                LocalPort = localPortMatch.Success ? localPortMatch.Groups[1].Value : "0";

                LocalAddress = $"{LocalIP}:{LocalPort}";

                // 远程端口 + 输出地址
                var remotePortMatch = Regex.Match(content, @"remotePort\s*=\s*(\d+)");
                if (remotePortMatch.Success)
                {
                    RemoteAddress = $"{ServerAddr}:{remotePortMatch.Groups[1].Value}";
                }
                else
                {
                    RemoteAddress = ServerAddr;
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
                    SetErrorState("找不到执行程序");
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
                            bool unexpected = !_isStoppingByUser;
                            var oldStatus = TunnelStatus;
                            IsRunning = false;
                            Status = "已停止";
                            TunnelStatus = TunnelStatus.Stopped;
                            _runningPid = null;
                            StopStartupTimer();
                            StopRunningLogTimer();

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

                            if (unexpected && oldStatus == TunnelStatus.Running)
                            {
                                ToastService.ShowTunnelStoppedUnexpected(Name);
                            }
                        }
                    };

                    if (_process.Start())
                    {
                        _runningPid = _process.Id;
                        _process.BeginOutputReadLine();
                        _process.BeginErrorReadLine();
                        _isStoppingByUser = false;
                        IsRunning = true;
                        Status = "启动中...";
                        TunnelStatus = TunnelStatus.Starting;
                        _startupCompleted = false;
                        AppendLog($"[信息] 隧道已启动，PID: {_runningPid}");
                        StartStartupTimer();
                        StartRunningLogTimer();
                    }
                }
                catch (Exception ex)
                {
                    IsRunning = false;
                    Status = "启动失败";
                    TunnelStatus = TunnelStatus.Error;
                    AppendLog($"[ERROR] {ex.Message}");
                    SetErrorState(ex.Message);

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

                _isStoppingByUser = true;
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
                StopStartupTimer();
                StopRunningLogTimer();

                SavePersistentLog(_isStoppingByUser);

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
                TunnelStatus = TunnelStatus.Stopped;
                AppendLog("[信息] 隧道已停止。");
            }
        }

        private void StartStartupTimer()
        {
            StopStartupTimer();
            _startupTimer = new System.Threading.Timer(StartupTimerCallback, null, StartupTimeoutSeconds * 1000, Timeout.Infinite);
        }

        private void StopStartupTimer()
        {
            if (_startupTimer != null)
            {
                _startupTimer.Dispose();
                _startupTimer = null;
            }
        }

        private void StartupTimerCallback(object state)
        {
            lock (_processLock)
            {
                if (!_startupCompleted && TunnelStatus == TunnelStatus.Starting)
                {
                    SetErrorState("启动超时");
                }
            }
        }

        private void SetErrorState(string errorMessage)
        {
            IsRunning = false;
            Status = $"异常: {errorMessage}";
            TunnelStatus = TunnelStatus.Error;
            ToastService.ShowTunnelError(Name, errorMessage);

            if (_process != null)
            {
                try
                {
                    _process.Kill();
                    _process.Dispose();
                }
                catch { }
                _process = null;
            }
        }

        private void SetRunningState()
        {
            _startupCompleted = true;
            StopStartupTimer();
            Status = "运行中";
            TunnelStatus = TunnelStatus.Running;
            ToastService.ShowTunnelStarted(Name);
        }

        private void CheckLogForStatus(string logLine)
        {
            if (_startupCompleted) return;

            string lowerLine = logLine.ToLower();

            if (lowerLine.Contains("start proxy success"))
            {
                SetRunningState();
            }
            else if (lowerLine.Contains("error") || lowerLine.Contains("fail") || lowerLine.Contains("failed"))
            {
                string errorMsg = ExtractErrorMessage(logLine);
                SetErrorState(errorMsg);
            }
        }

        private string ExtractErrorMessage(string logLine)
        {
            string lowerLine = logLine.ToLower();
            int errorIndex = lowerLine.IndexOf("error");
            int failIndex = lowerLine.IndexOf("fail");

            int startIndex = -1;
            if (errorIndex >= 0)
                startIndex = errorIndex;
            else if (failIndex >= 0)
                startIndex = failIndex;

            if (startIndex >= 0)
            {
                string errorPart = logLine.Substring(startIndex);
                int endIndex = errorPart.IndexOf('\n');
                if (endIndex > 0)
                    errorPart = errorPart.Substring(0, endIndex);
                return errorPart.Trim();
            }

            return "启动失败";
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

            CheckLogForStatus(message);

            _runningLogDirty = true;
        }

        private void StartRunningLogTimer()
        {
            StopRunningLogTimer();
            _runningLogTimer = new System.Threading.Timer(RunningLogTimerCallback, null, 1000, 1000);
        }

        private void StopRunningLogTimer()
        {
            if (_runningLogTimer != null)
            {
                _runningLogTimer.Dispose();
                _runningLogTimer = null;
            }
        }

        private void RunningLogTimerCallback(object state)
        {
            if (!_runningLogDirty) return;
            _runningLogDirty = false;

            try
            {
                string logPath = GetLogFilePath();
                string tunnelDir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(tunnelDir))
                    Directory.CreateDirectory(tunnelDir);

                lock (_logLock)
                {
                    string content = $"[RUNNING]\n{FullLogText}";
                    File.WriteAllText(logPath, content);
                }
            }
            catch { }
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

        private string GetLogFilePath()
        {
            string tunnelDir = Path.GetDirectoryName(ConfigPath);
            return Path.Combine(tunnelDir, "last_run.log");
        }

        public void SavePersistentLog(bool normalShutdown)
        {
            try
            {
                string logPath = GetLogFilePath();
                string tunnelDir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(tunnelDir))
                    Directory.CreateDirectory(tunnelDir);

                string marker = normalShutdown ? "[NORMAL_SHUTDOWN]" : "[CRASH]";
                string content = $"{marker}\n{FullLogText}";
                File.WriteAllText(logPath, content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[持久化] 保存日志失败: {ex.Message}");
            }
        }

        public bool LoadPersistentLog()
        {
            try
            {
                string logPath = GetLogFilePath();
                if (!File.Exists(logPath))
                    return false;

                string content = File.ReadAllText(logPath);
                if (string.IsNullOrEmpty(content))
                    return false;

                string[] lines = content.Split('\n');
                if (lines.Length == 0)
                    return false;

                string firstLine = lines[0].Trim();
                bool isNormalShutdown = firstLine == "[NORMAL_SHUTDOWN]";

                if (isNormalShutdown)
                {
                    try
                    {
                        File.Delete(logPath);
                    }
                    catch { }
                    return false;
                }

                string logContent = content.Substring(firstLine.Length + 1);
                if (string.IsNullOrEmpty(logContent))
                    return false;

                lock (_logLock)
                {
                    _logBuffer.Clear();
                    var logLines = logContent.Split('\n');
                    foreach (var line in logLines)
                    {
                        if (!string.IsNullOrEmpty(line))
                            _logBuffer.Add(line.TrimEnd('\r'));
                    }

                    if (_logBuffer.Count > MaxLogLines)
                    {
                        _logBuffer.RemoveRange(0, _logBuffer.Count - MaxLogLines);
                    }

                    var builder = new StringBuilder();
                    for (int i = 0; i < _logBuffer.Count; i++)
                    {
                        builder.AppendLine(_logBuffer[i]);
                    }

                    FullLogText = builder.ToString();
                    HasLog = _logBuffer.Count > 0;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[持久化] 加载日志失败: {ex.Message}");
                return false;
            }
        }

        public bool CheckAndAttachExistingProcess()
        {
            try
            {
                string tunnelDir = Path.GetDirectoryName(ConfigPath);
                string privateExeName = $"frpc_{Name}";

                Debug.WriteLine($"[进程检测] 检测隧道 {Name} 的残留进程");
                Debug.WriteLine($"[进程检测] 隧道目录: {tunnelDir}");

                // 先按私有进程名查找（frpc_{Name}）
                var privateProcesses = Process.GetProcessesByName(privateExeName);
                Debug.WriteLine($"[进程检测] 按 {privateExeName} 查找: 找到 {privateProcesses.Length} 个");

                foreach (var proc in privateProcesses)
                {
                    try
                    {
                        string procPath = proc.MainModule?.FileName ?? "";
                        string procDir = Path.GetDirectoryName(procPath) ?? "";
                        Debug.WriteLine($"[进程检测] 私有进程 PID={proc.Id}, 路径={procPath}, 目录={procDir}");

                        if (procDir.Equals(tunnelDir, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"[进程检测] 目录匹配! 附加到进程 PID={proc.Id}");
                            AttachToProcess(proc);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[进程检测] 检测私有进程失败: {ex.Message}");
                        continue;
                    }
                }

                // 再按公用进程名查找（frpc），在隧道目录内的
                var publicProcesses = Process.GetProcessesByName("frpc");
                Debug.WriteLine($"[进程检测] 按 frpc 查找: 找到 {publicProcesses.Length} 个");

                foreach (var proc in publicProcesses)
                {
                    try
                    {
                        string procPath = proc.MainModule?.FileName ?? "";
                        string procDir = Path.GetDirectoryName(procPath) ?? "";
                        Debug.WriteLine($"[进程检测] 公用进程 PID={proc.Id}, 路径={procPath}, 目录={procDir}");

                        if (procDir.Equals(tunnelDir, StringComparison.OrdinalIgnoreCase))
                        {
                            Debug.WriteLine($"[进程检测] 目录匹配! 附加到进程 PID={proc.Id}");
                            AttachToProcess(proc);
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[进程检测] 检测公用进程失败: {ex.Message}");
                        continue;
                    }
                }

                Debug.WriteLine($"[进程检测] 未找到匹配的残留进程");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[进程检测] 检测残留进程失败: {ex.Message}");
                return false;
            }
        }

        private void AttachToProcess(Process existingProcess)
        {
            lock (_processLock)
            {
                _process = existingProcess;
                _runningPid = existingProcess.Id;
                _isStoppingByUser = false;
                _startupCompleted = true;
                IsRunning = true;
                Status = "运行中";
                TunnelStatus = TunnelStatus.Running;
                StartRunningLogTimer();

                _process.EnableRaisingEvents = true;
                _process.Exited += (s, e) =>
                {
                    lock (_processLock)
                    {
                        bool unexpected = !_isStoppingByUser;
                        var oldStatus = TunnelStatus;
                        IsRunning = false;
                        Status = "已停止";
                        TunnelStatus = TunnelStatus.Stopped;
                        _runningPid = null;
                        StopStartupTimer();
                        StopRunningLogTimer();

                        try
                        {
                            if (_process != null)
                            {
                                _process.Dispose();
                            }
                        }
                        catch { }

                        _process = null;

                        if (unexpected && oldStatus == TunnelStatus.Running)
                        {
                            ToastService.ShowTunnelStoppedUnexpected(Name);
                        }
                    }
                };

                AppendLog($"[信息] 已附加到现有进程，PID: {_runningPid}");
            }
        }

        #endregion
    }
}