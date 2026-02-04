using System;
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
        private static readonly HttpClient _client = new HttpClient();
        private const string BaseUrl = "https://www.moboxfrp.top/API/";
        public static string CurrentToken { get; set; }
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

                using var responseStream = await response.Content.ReadAsStreamAsync();
                var result = await JsonSerializer.DeserializeAsync<LoginResponse>(responseStream);

                if (result != null && result.Success)
                {
                    CurrentToken = result.Token;
                }
                return result;
            }
            catch (Exception ex)
            {
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
                System.Diagnostics.Debug.WriteLine($"API 返回内容 ({endpoint}): {responseString}");

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return JsonSerializer.Deserialize<T>(responseString, options);
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