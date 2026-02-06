using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MoboxFrpGUI.Models
{
    #region 登录相关
    public class LoginRequest
    {
        [JsonPropertyName("loginType")]
        public string LoginType { get; set; } = "email";

        [JsonPropertyName("account")]
        public string Account { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
    #endregion

    #region 穿透码（UserCode）相关
    public class UserCodeListResponse
    {
        public List<UserCodeItem> codes { get; set; }
        public bool success { get; set; }
        public string message { get; set; }
    }

    public class UserCodeItem
    {
        public string codeID { get; set; }      // 穿透码编号
        public string node { get; set; }        // 节点编号
        public string number { get; set; }      // 操作编号
        public string portServer { get; set; }  // 服务器端口
        public string portOpen { get; set; }    // 可用端口
        public string band { get; set; }        // 带宽
        public string status { get; set; }      // 状态: running/outdated
        public string token { get; set; }       // 穿透码
        public string timeOutdate { get; set; } // 过期毫秒时间戳
        public string timeCreate { get; set; }  // 创建毫秒时间戳

        // --- 仅保留核心显示逻辑 ---

        [JsonIgnore]
        public string CreateTimeDisplay =>
            long.TryParse(timeCreate, out long ms) ?
            DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString("yyyy/MM/dd HH:mm") : "未知";

        [JsonIgnore]
        public string ExpireTimeDisplay =>
            long.TryParse(timeOutdate, out long ms) ?
            DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime.ToString("yyyy/MM/dd HH:mm") : "未知";

        [JsonIgnore]
        public string StatusDisplay => (status?.ToLower() == "running") ? "运行中" : "已到期";

        [JsonIgnore]
        public string StatusColor => (status?.ToLower() == "running") ? "#28C76F" : "#FF4D4F";
    }
    #endregion

    #region 节点（Node）相关
    public class NodeListResponse
    {
        public List<NodeItem> nodes { get; set; }
        public bool success { get; set; }
        public string message { get; set; }
    }

    public class NodeItem
    {
        [JsonPropertyName("nodeID")]
        public string nodeID { get; set; }

        [JsonPropertyName("name")]
        public string name { get; set; }

        [JsonPropertyName("price")]
        public string price { get; set; }

        [JsonPropertyName("online")]
        public string online { get; set; }

        [JsonPropertyName("coin")]
        public string coin { get; set; }

        [JsonPropertyName("portStart")]
        public string portStart { get; set; }

        [JsonPropertyName("portEnd")]
        public string portEnd { get; set; }
    }
    #endregion

    #region 广告相关
    public class AdResponse
    {
        // 统一改为首字母大写，或者与你的代码调用保持一致
        public bool Success { get; set; }
        public AdData Data { get; set; }
        public string Message { get; set; }
    }

    public class AdData
    {
        public string AdID { get; set; }
        public string Url_jump { get; set; } // 对应报错的 Url_jump
        public string Url_pic { get; set; }  // 对应报错的 Url_pic
    }
    #endregion
}