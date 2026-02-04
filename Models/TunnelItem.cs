using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

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

        private int? _runningPid;

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
            if (IsRunning) return;
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
                    IsRunning = false;
                    Status = "已停止";
                    _runningPid = null;
                    _process?.Dispose();
                    _process = null;
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
            }
        }

        // 停止隧道
        public void Stop()
        {
            if (_runningPid.HasValue)
            {
                KillProcessByPid(_runningPid.Value);
                _runningPid = null;
            }

            try
            {
                if (_process != null)
                {
                    if (!_process.HasExited) _process.Kill(true);
                    _process.Dispose();
                }
            }
            catch { }
            finally
            {
                _process = null;
                IsRunning = false;
                Status = "已停止";
            }
        }
        
        // 利用pid尝试kill frpc
        public static void KillProcessByPid(int pid)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    if (p.ProcessName.Contains("frpc", StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(true);
                    }
                }
            }
            catch (ArgumentException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[清理] 终止 PID {pid} 失败: {ex.Message}");
            }
        }

        // 日志相关显示盒清除
        private void AppendLog(string message)
        {
            if (!HasLog) HasLog = true;
            string newLog = $"[{DateTime.Now:HH:mm:ss}] {message}";
            var lines = FullLogText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            // 一定要限制log的行数 10gb的log不是闹着玩的（
            if (lines.Count >= 500)
            {
                lines.RemoveAt(0); // 删掉最开始的一行
            }

            lines.Add(newLog);
            FullLogText = string.Join("\n", lines) + "\n";
        }

        public void ClearLog()
        {
            FullLogText = "";  
            HasLog = false; 

        }

        #endregion
    }
}