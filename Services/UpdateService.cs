using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace MoboxFrpGUI.Services
{
    public class UpdateInfo
    {
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string HtmlUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public long DownloadSize { get; set; }
        public bool IsSingleFileAsset { get; set; }
    }

    public static class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/ZhaYi-Miao/MoboxFrpGUI/releases/latest";
        private static readonly HttpClient _httpClient;

        static UpdateService()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            
            _httpClient = new HttpClient(CreateProxyHandler())
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MoboxFrpGUI-UpdateChecker");
        }

        /// <summary>
        /// 创建代理 Handler，自动读取 Windows 系统代理（含 Clash/v2ray 等软件设置的代理）
        /// </summary>
        private static HttpClientHandler CreateProxyHandler()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = true
            };

            try
            {
                // 1. 先尝试环境变量（标准做法）
                string envProxy = Environment.GetEnvironmentVariable("HTTPS_PROXY")
                                  ?? Environment.GetEnvironmentVariable("https_proxy")
                                  ?? Environment.GetEnvironmentVariable("HTTP_PROXY")
                                  ?? Environment.GetEnvironmentVariable("http_proxy");

                if (!string.IsNullOrEmpty(envProxy))
                {
                    handler.Proxy = new WebProxy(envProxy);
                    Debug.WriteLine($"[更新] 使用环境变量代理: {envProxy}");
                    return handler;
                }

                // 2. 读取 Windows 系统代理设置（注册表）
                // 这是 Clash/v2ray 等代理软件设置系统代理的位置
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (key != null)
                    {
                        bool proxyEnable = key.GetValue("ProxyEnable") is int enable && enable != 0;
                        object? proxyServerObj = key.GetValue("ProxyServer");

                        if (proxyEnable && proxyServerObj is string proxyServer && !string.IsNullOrEmpty(proxyServer))
                        {
                            // ProxyServer 可能是 "127.0.0.1:7890" 或 "http=127.0.0.1:7890;https=127.0.0.1:7890"
                            string? httpProxy = null;
                            string? httpsProxy = null;

                            if (proxyServer.Contains(';') || proxyServer.Contains('='))
                            {
                                // 分协议格式
                                var parts = proxyServer.Split(';');
                                foreach (var part in parts)
                                {
                                    var kv = part.Split('=');
                                    if (kv.Length == 2)
                                    {
                                        if (kv[0].Trim().Equals("https", StringComparison.OrdinalIgnoreCase))
                                            httpsProxy = kv[1].Trim();
                                        else if (kv[0].Trim().Equals("http", StringComparison.OrdinalIgnoreCase))
                                            httpProxy = kv[1].Trim();
                                    }
                                }
                                // 优先用 https
                                var proxyAddr = httpsProxy ?? httpProxy;
                                if (!string.IsNullOrEmpty(proxyAddr))
                                {
                                    var proxyUrl = proxyAddr.StartsWith("http") ? proxyAddr : $"http://{proxyAddr}";
                                    handler.Proxy = new WebProxy(proxyUrl, false);
                                    Debug.WriteLine($"[更新] 使用系统代理(分协议): {proxyUrl}");
                                    return handler;
                                }
                            }
                            else
                            {
                                // 单一地址格式
                                var proxyUrl = proxyServer.StartsWith("http") ? proxyServer : $"http://{proxyServer}";
                                handler.Proxy = new WebProxy(proxyUrl, false);
                                Debug.WriteLine($"[更新] 使用系统代理: {proxyUrl}");
                                return handler;
                            }
                        }
                    }
                }

                // 3. 回退到默认 WebProxy
                handler.Proxy = WebRequest.DefaultWebProxy;
                Debug.WriteLine("[更新] 使用默认 WebProxy");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[更新] 读取代理配置失败: {ex.Message}");
                handler.Proxy = WebRequest.DefaultWebProxy;
            }

            return handler;
        }

        /// <summary>
        /// 获取当前程序版本号
        /// </summary>
        public static string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                return $"{version.Major}.{version.Minor}.{version.Build}";
            }
            return "1.2.1";
        }

        /// <summary>
        /// 判断当前是否为单文件发布模式
        /// </summary>
        public static bool IsSingleFilePublish()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string depsFile = Path.Combine(baseDir, "MoboxFrpGUI.deps.json");
            // 单文件模式下 deps.json 不在 exe 旁边
            return !File.Exists(depsFile);
        }

        /// <summary>
        /// 从 GitHub API 获取最新版本信息
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(GitHubApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[更新] GitHub API 请求失败: {response.StatusCode}");
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                // 去掉 v 前缀
                string version = tagName.TrimStart('v', 'V');

                string title = "";
                if (root.TryGetProperty("name", out var nameEl))
                    title = nameEl.GetString() ?? "";

                string body = "";
                if (root.TryGetProperty("body", out var bodyEl))
                    body = bodyEl.GetString() ?? "";

                string htmlUrl = "";
                if (root.TryGetProperty("html_url", out var urlEl))
                    htmlUrl = urlEl.GetString() ?? "";

                // 解析 assets，查找 exe 和 zip
                string exeUrl = "";
                long exeSize = 0;
                string zipUrl = "";
                long zipSize = 0;

                if (root.TryGetProperty("assets", out var assetsEl))
                {
                    foreach (var asset in assetsEl.EnumerateArray())
                    {
                        string assetName = asset.GetProperty("name").GetString() ?? "";
                        string assetUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        long assetSize = asset.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;

                        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            exeUrl = assetUrl;
                            exeSize = assetSize;
                        }
                        else if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            zipUrl = assetUrl;
                            zipSize = assetSize;
                        }
                    }
                }

                return new UpdateInfo
                {
                    Version = version,
                    Title = title,
                    Body = body,
                    HtmlUrl = htmlUrl,
                    DownloadUrl = exeUrl,
                    DownloadSize = exeSize,
                    IsSingleFileAsset = !string.IsNullOrEmpty(exeUrl)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[更新] 检查更新失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 比较版本号，返回 true 表示有新版本
        /// </summary>
        public static bool HasNewVersion(string currentVersion, string latestVersion)
        {
            if (string.IsNullOrEmpty(latestVersion)) return false;

            var current = Version.TryParse(currentVersion, out var cv) ? cv : new Version(0, 0, 0);
            var latest = Version.TryParse(latestVersion, out var lv) ? lv : new Version(0, 0, 0);

            return latest > current;
        }

        /// <summary>
        /// 下载单文件 exe 并准备更新脚本
        /// </summary>
        public static async Task<bool> DownloadAndPrepareUpdateAsync(string downloadUrl, IProgress<(long downloaded, long total)>? progress = null)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "MoboxFrpGUI_Update");
                Directory.CreateDirectory(tempDir);

                string newExePath = Path.Combine(tempDir, "MoboxFrpGUI_new.exe");

                // 下载需要更长的超时时间（文件较大）
                using var downloadClient = new HttpClient(CreateProxyHandler())
                {
                    Timeout = TimeSpan.FromMinutes(10)
                };
                downloadClient.DefaultRequestHeaders.Add("User-Agent", "MoboxFrpGUI-UpdateChecker");

                // 下载新版本 exe
                using (var response = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode) return false;

                    long totalBytes = response.Content.Headers.ContentLength ?? 0;
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = File.Create(newExePath);

                    byte[] buffer = new byte[81920];
                    long bytesRead = 0;
                    int read;

                    while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        bytesRead += read;
                        progress?.Report((bytesRead, totalBytes));
                    }
                }

                // 生成更新脚本
                string currentExePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentExePath)) return false;

                string scriptPath = Path.Combine(tempDir, "update.cmd");
                string scriptContent = GenerateUpdateScript(currentExePath, newExePath);
                File.WriteAllText(scriptPath, scriptContent);

                Debug.WriteLine($"[更新] 下载完成，更新脚本已生成: {scriptPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[更新] 下载失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成更新替换脚本
        /// </summary>
        private static string GenerateUpdateScript(string currentExePath, string newExePath)
        {
            // 脚本逻辑：等待旧进程退出 → 用新 exe 覆盖旧 exe → 启动新版本 → 删除临时文件
            return $@"@echo off
chcp 65001 >nul
echo ============================================
echo   MoboxFrpGUI 正在更新...
echo ============================================
echo.

REM 等待旧进程退出
echo [1/4] 等待程序退出...
:wait_loop
tasklist /fi ""pid eq {Process.GetCurrentProcess().Id}"" 2>nul | find ""{Process.GetCurrentProcess().Id}"" >nul
if %errorlevel%==0 (
    timeout /t 1 /nobreak >nul
    goto wait_loop
)
timeout /t 2 /nobreak >nul

REM 替换 exe 文件
echo [2/4] 替换程序文件...
copy /y ""{newExePath}"" ""{currentExePath}""
if %errorlevel% neq 0 (
    echo.
    echo 替换失败！请手动替换。
    echo 新版本文件: {newExePath}
    echo 目标位置: {currentExePath}
    echo.
    pause
    goto cleanup
)

REM 启动新版本
echo [3/4] 启动新版本...
start "" ""{currentExePath}""

REM 清理临时文件
:cleanup
echo [4/4] 清理临时文件...
timeout /t 2 /nobreak >nul
del /f /q ""{newExePath}"" 2>nul
del /f /q ""%~f0"" 2>nul

echo.
echo 更新完成！
timeout /t 2 /nobreak >nul
";
        }

        /// <summary>
        /// 执行更新：启动更新脚本并退出当前程序
        /// </summary>
        public static void ApplyUpdateAndRestart()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "MoboxFrpGUI_Update");
            string scriptPath = Path.Combine(tempDir, "update.cmd");

            if (!File.Exists(scriptPath))
            {
                Debug.WriteLine("[更新] 更新脚本不存在");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = tempDir
                });

                // 退出当前程序
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[更新] 启动更新脚本失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开 GitHub Release 页面
        /// </summary>
        public static void OpenReleasePage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = string.IsNullOrEmpty(url) ? "https://github.com/ZhaYi-Miao/MoboxFrpGUI/releases" : url,
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
