using System;
using System.IO;
using System.Text;

namespace MoboxFrpGUI.Services
{
    public static class LogService
    {
        private static readonly object _writeLock = new object();
        private static string _logDir;
        private static string _currentLogFile;
        private static string _currentErrorFile;

        public static string LogDirectory => _logDir;

        static LogService()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _logDir = Path.Combine(appData, "MoboxFrp", "logs");
                if (!Directory.Exists(_logDir))
                {
                    Directory.CreateDirectory(_logDir);
                }
                string date = DateTime.Now.ToString("yyyyMMdd");
                _currentLogFile = Path.Combine(_logDir, $"app_{date}.log");
                _currentErrorFile = Path.Combine(_logDir, $"error_{date}.log");
            }
            catch
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _logDir = Path.Combine(baseDir, "logs");
                if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);
                string date = DateTime.Now.ToString("yyyyMMdd");
                _currentLogFile = Path.Combine(_logDir, $"app_{date}.log");
                _currentErrorFile = Path.Combine(_logDir, $"error_{date}.log");
            }
        }

        public static void Info(string message)
        {
            WriteLog(_currentLogFile, "INFO", message);
        }

        public static void Warn(string message)
        {
            WriteLog(_currentLogFile, "WARN", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"InnerException: {ex.InnerException.GetType().FullName}");
                    sb.AppendLine($"InnerMessage: {ex.InnerException.Message}");
                    sb.AppendLine($"InnerStackTrace: {ex.InnerException.StackTrace}");
                }
            }
            WriteLog(_currentErrorFile, "ERROR", sb.ToString());
            WriteLog(_currentLogFile, "ERROR", message);
        }

        public static void Fatal(string message, Exception ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 致命错误 ===");
            sb.AppendLine(message);
            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"InnerException: {ex.InnerException.GetType().FullName}");
                    sb.AppendLine($"InnerMessage: {ex.InnerException.Message}");
                    sb.AppendLine($"InnerStackTrace: {ex.InnerException.StackTrace}");
                }
            }
            WriteLog(_currentErrorFile, "FATAL", sb.ToString());
            WriteLog(_currentLogFile, "FATAL", message);
        }

        public static string GetFullErrorReport(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MoboxFrp 错误报告 ===");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"版本: {App.Current.TryFindResource("AppVersion")}");
            sb.AppendLine($"操作系统: {Environment.OSVersion}");
            sb.AppendLine($"运行时: {Environment.Version}");
            sb.AppendLine($"进程ID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
            sb.AppendLine();
            sb.AppendLine("--- 异常信息 ---");
            sb.AppendLine($"类型: {ex.GetType().FullName}");
            sb.AppendLine($"消息: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("--- 堆栈跟踪 ---");
            sb.AppendLine(ex.StackTrace);
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("--- 内部异常 ---");
                sb.AppendLine($"类型: {ex.InnerException.GetType().FullName}");
                sb.AppendLine($"消息: {ex.InnerException.Message}");
                sb.AppendLine();
                sb.AppendLine(ex.InnerException.StackTrace);
            }
            return sb.ToString();
        }

        private static void WriteLog(string filePath, string level, string message)
        {
            try
            {
                lock (_writeLock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string line = $"[{timestamp}] [{level}] {message}";
                    File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        public static void CleanOldLogs(int keepDays = 7)
        {
            try
            {
                var dir = new DirectoryInfo(_logDir);
                if (!dir.Exists) return;

                foreach (var file in dir.GetFiles("*.log"))
                {
                    if ((DateTime.Now - file.LastWriteTime).TotalDays > keepDays)
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
