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
    }

    // 利用windows账号的凭据直接处理保存的登录信息
    public static class ConfigService
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user.dat");

        // 动态生成Entropy，结合用户名和机器名提高安全性
        private static byte[] GenerateEntropy(string account)
        {
            string entropyBase = $"{account}_{Environment.UserName}_{Environment.MachineName}_MoboxFrp";
            return Encoding.UTF8.GetBytes(entropyBase);
        }

        public static bool SaveConfig(string account, string password, bool remember, bool autoLogin)
        {
            try
            {
                var config = new UserConfig { Account = account, Password = password, RememberMe = remember, AutoLogin = autoLogin };
                string json = JsonSerializer.Serialize(config);
                byte[] data = Encoding.UTF8.GetBytes(json);
                byte[] entropy = GenerateEntropy(account);
                byte[] encryptedData = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
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
                // 先读取文件获取账号，用于生成正确的Entropy
                byte[] encryptedData = File.ReadAllBytes(FilePath);

                // 尝试使用默认账号加载（如果文件存在但无法解密）
                // 这里使用一个临时解密策略：先尝试用空账号解密
                try
                {
                    byte[] data = ProtectedData.Unprotect(encryptedData, GenerateEntropy(""), DataProtectionScope.CurrentUser);
                    string json = Encoding.UTF8.GetString(data);
                    var tempConfig = JsonSerializer.Deserialize<UserConfig>(json);

                    // 如果成功，用正确的账号重新加密
                    if (tempConfig != null && !string.IsNullOrEmpty(tempConfig.Account))
                    {
                        // 用正确的账号重新保存
                        SaveConfig(tempConfig.Account, tempConfig.Password, tempConfig.RememberMe, tempConfig.AutoLogin);
                        return tempConfig;
                    }
                }
                catch
                {
                    // 如果失败，文件可能已损坏或使用了旧的加密方式
                    System.Diagnostics.Debug.WriteLine("配置文件解密失败，可能需要重新登录");
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
                return null;
            }
        }

        // 清理损坏的配置文件
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