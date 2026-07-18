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
        public bool AutoLogin { get; set; }
        public string Theme { get; set; } = "Default"; // Default, Light, Dark
        public bool AutoCheckUpdate { get; set; } = true;
    }

    public static class ConfigService
    {
        private static readonly string FilePath;
        private static readonly byte[] FixedEntropy;

        static ConfigService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dir = Path.Combine(appData, "MoboxFrpGUI");
            Directory.CreateDirectory(dir);
            FilePath = Path.Combine(dir, "user.dat");
            FixedEntropy = Encoding.UTF8.GetBytes("MoboxFrpGUI_Persistent_v3");
        }

        public static bool SaveConfig(string account, string password, bool remember, bool autoLogin)
        {
            try
            {
                var config = LoadConfig() ?? new UserConfig();
                config.Account = account;
                config.Password = password;
                config.RememberMe = remember;
                config.AutoLogin = autoLogin;
                string json = JsonSerializer.Serialize(config);
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(data, FixedEntropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, encryptedData);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
                return false;
            }
        }

        public static bool SaveConfig(UserConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config);
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] encryptedData = ProtectedData.Protect(data, FixedEntropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, encryptedData);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
                return false;
            }
        }

        public static UserConfig LoadConfig()
        {
            if (!File.Exists(FilePath)) return null;

            try
            {
                byte[] encryptedData = File.ReadAllBytes(FilePath);
                byte[] data = ProtectedData.Unprotect(encryptedData, FixedEntropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(data);
                return JsonSerializer.Deserialize<UserConfig>(json);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("配置文件解密失败，可能需要重新登录");
                return null;
            }
        }

        public static void ClearCorruptedConfig()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理配置文件失败: {ex.Message}");
            }
        }
    }
}
