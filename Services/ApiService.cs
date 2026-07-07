using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MoboxFrpGUI.Models;

namespace MoboxFrpGUI.Services
{
    public class ApiService
    {
        private static readonly HttpClient _client;
        private const string BaseUrl = "https://www.moboxfrp.top/API/";
        private static string _currentToken;
        private static readonly object _tokenLock = new object();

        // 线程安全的Token访问
        public static string CurrentToken
        {
            get { lock (_tokenLock) { return _currentToken; } }
            set { lock (_tokenLock) { _currentToken = value; } }
        }

        static ApiService()
        {
            // 配置HttpClientHandler支持TLS 1.2和TLS 1.3
            var handler = new HttpClientHandler
            {
                // 设置TLS版本
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                // 允许自动重定向
                AllowAutoRedirect = true,
                // 设置最大自动重定向次数
                MaxAutomaticRedirections = 5
            };

            _client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            // 添加必要的请求头，模拟浏览器行为
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36");
            _client.DefaultRequestHeaders.Add("Accept", "*/*");
            _client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            _client.DefaultRequestHeaders.Add("Origin", "https://www.moboxfrp.top");
            _client.DefaultRequestHeaders.Add("Referer", "https://www.moboxfrp.top/login");
        }
        public async Task<LoginResponse> LoginAsync(string account, string password)
        {
            // 如果是11位数的数字就是手机号 否则就是邮箱，应该没人会把qq号直接当手机号写吧（？
            string loginType = "email";
            if (!account.Contains("@") && Regex.IsMatch(account, @"^\d{11}$"))
            {
                loginType = "phone";
            }

            var requestData = new LoginRequest
            {
                LoginType = loginType,
                Account = account,
                Password = password
            };

            try
            {
                string json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{BaseUrl}Login", content);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new LoginResponse { Success = false, Message = "账号或密码错误 (404)" };
                }

                string responseString = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"登录响应: {responseString}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<LoginResponse>(responseString, options);

                if (result != null && result.Success)
                {
                    CurrentToken = result.Token;
                }
                return result;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                string errorMsg = "网络异常";
                if (ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
                {
                    errorMsg = "SSL连接失败，请检查网络设置或更新系统";
                }
                else if (ex.Message.Contains("timed"))
                {
                    errorMsg = "连接超时，请检查网络连接";
                }
                else if (ex.Message.Contains("refused"))
                {
                    errorMsg = "连接被拒绝，请检查网络防火墙设置";
                }
                System.Diagnostics.Debug.WriteLine($"登录异常: {ex.Message}");
                return new LoginResponse { Success = false, Message = $"{errorMsg}: {ex.Message}" };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"登录异常: {ex.Message}");
                return new LoginResponse { Success = false, Message = $"网络异常: {ex.Message}" };
            }
        }

        // 基础post且附带token
        public async Task<T> PostWithTokenAsync<T>(string endpoint, object data = null)
        {
            try
            {
                var requestObj = data ?? new { token = CurrentToken };
                string json = JsonSerializer.Serialize(requestObj);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync($"{BaseUrl}{endpoint}", content);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"API 错误: {endpoint} 返回状态码 {response.StatusCode}");
                    return default;
                }

                string responseString = await response.Content.ReadAsStringAsync();
                // 避免输出敏感信息，只在调试模式下输出摘要
                System.Diagnostics.Debug.WriteLine($"API 返回成功 ({endpoint}): {responseString.Length} 字节");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<T>(responseString, options);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                string errorMsg = "网络异常";
                if (ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
                {
                    errorMsg = "SSL连接失败";
                }
                else if (ex.Message.Contains("timed"))
                {
                    errorMsg = "连接超时";
                }
                System.Diagnostics.Debug.WriteLine($"API 访问异常 ({endpoint}): {errorMsg} - {ex.Message}");
                return default;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API 访问异常 ({endpoint}): {ex.Message}");
                return default;
            }
        }
    }

    public class UserInfoResponse
    {
        public string userID { get; set; }     // 用户ID
        public string username { get; set; }   // 用户名
        public string email { get; set; }      // 邮箱
        public string phone { get; set; }      // 手机号
        public string permission { get; set; } // 权限组
        public string gold { get; set; }       // 金币
        public string silver { get; set; }     // 银币
        public bool success { get; set; }      // 状态
    }


}