using System.Text.Json.Serialization;

namespace CodeSharp.Model;

public class Config
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("apiBase")]
    public string ApiBase { get; set; } = "https://api.token-ai.cn/v1";

    [JsonPropertyName("model")] public string Model { get; set; } = "kimi-k2-0905";

    // 保持静态属性以兼容现有代码
    public static string API_KEY { get; set; } = "";
    public static string API_BASE { get; set; } = "https://api.token-ai.cn/v1";
    
    public static string MODEL { get; set; } = "kimi-k2-0905";
}