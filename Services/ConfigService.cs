using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MoboxFrpGUI.Services
{
    public class UserConfig
    {
        public string Account { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
    }

    // 利用windows账号的凭据直接处理保存的登录信息
    public static class ConfigService
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MoboxFrp_zhayi");

        public static void SaveConfig(string account, string password, bool remember)
        {
            try
            {
                var config = new UserConfig { Account = account, Password = password, RememberMe = remember };
                string json = JsonSerializer.Serialize(config);
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, encryptedData);
            }
            catch { }
        }

        public static UserConfig LoadConfig()
        {
            if (!File.Exists(FilePath)) return null;
            try
            {
                byte[] encryptedData = File.ReadAllBytes(FilePath);
                byte[] data = ProtectedData.Unprotect(encryptedData, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<UserConfig>(json);
            }
            catch { return null; }
        }
    }
}