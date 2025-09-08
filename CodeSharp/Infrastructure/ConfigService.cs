using System.Text.Encodings.Web;
using CodeSharp.Model;
using System.Text.Json;

namespace CodeSharp.Infrastructure;

public class ConfigService
{
    private static Config _config;

    private static readonly string ConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".code-sharp");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new JsonSerializerOptions(JsonSerializerOptions.Web)
        {
            // 支持大小写忽略
            PropertyNameCaseInsensitive = true,
            // 中文乱码
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

    /// <summary>
    /// 获取配置信息
    /// </summary>
    public static Config GetConfig()
    {
        if (_config == null)
        {
            LoadConfig();
        }

        return _config;
    }

    /// <summary>
    /// 加载配置文件
    /// </summary>
    private static void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                CreateDefaultConfig();
            }

            var json = File.ReadAllText(ConfigFilePath);
            _config = JsonSerializer.Deserialize<Config>(json) ?? new Config();

            // 同步到静态属性
            Config.API_KEY = _config.ApiKey;
            Config.API_BASE = _config.ApiBase;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"警告: 加载配置文件失败: {ex.Message}");
            _config = new Config();
        }
    }

    /// <summary>
    /// 创建默认配置文件
    /// </summary>
    private static void CreateDefaultConfig()
    {
        try
        {
            // 确保目录存在
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            var defaultConfig = new Config
            {
                ApiKey = "",
                ApiBase = "https://api.token-ai.cn/v1"
            };

            var json = JsonSerializer.Serialize(defaultConfig, JsonSerializerOptions);

            File.WriteAllText(ConfigFilePath, json);
            _config = defaultConfig;
        }
        catch (Exception ex)
        {
            _config = new Config();
        }
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public static void SaveConfig(Config config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonSerializerOptions);

            File.WriteAllText(ConfigFilePath, json);
            _config = config;

            // 同步到静态属性
            Config.API_KEY = config.ApiKey;
            Config.API_BASE = config.ApiBase;
            Config.MODEL = config.Model;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: 保存配置文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取配置文件路径
    /// </summary>
    public static string GetConfigPath()
    {
        return ConfigFilePath;
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    public static void ReloadConfig()
    {
        _config = null;
        LoadConfig();
    }
}