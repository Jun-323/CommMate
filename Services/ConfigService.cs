using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommMate.Models;

namespace CommMate.Services;

public class AppConfig
{
    public List<QuickCommand> QuickCommands { get; set; } = new();

    // 串口配置
    public string SerialPortName { get; set; } = "";
    public int SerialBaudRate { get; set; } = 115200;
    public Parity SerialParity { get; set; } = Parity.None;
    public int SerialDataBits { get; set; } = 8;
    public StopBits SerialStopBits { get; set; } = StopBits.One;
    public Handshake SerialFlowControl { get; set; } = Handshake.None;
    public FramingMode SerialFramingMode { get; set; } = FramingMode.Timeout;
    public int SerialPacketTimeout { get; set; } = 50;

    // 串口 UI 状态
    public bool SerialIsHex { get; set; }
    public bool SerialShowTimestamp { get; set; } = true;
    public bool SerialAutoScroll { get; set; } = true;
    public bool SerialAppendNewLine { get; set; }
    public int SerialSelectedNewLineIndex { get; set; }

    // 网络配置
    public int NetworkModeIndex { get; set; }
    public string NetworkRemoteHost { get; set; } = "127.0.0.1";
    public int NetworkRemotePort { get; set; } = 8080;
    public int NetworkLocalPort { get; set; } = 8080;
    public string NetworkLocalBindAddress { get; set; } = "0.0.0.0";
    public bool NetworkEnableBroadcast { get; set; }

    // 网络 UI 状态
    public bool NetworkIsHex { get; set; }
    public bool NetworkShowTimestamp { get; set; } = true;
    public bool NetworkAutoScroll { get; set; } = true;
    public bool NetworkAppendNewLine { get; set; }
    public int NetworkSelectedNewLineIndex { get; set; }

    // 应用偏好
    public bool IsDarkTheme { get; set; }
    public string Language { get; set; } = "zh";
}

public static class ConfigService
{
    private static readonly string ConfigFileName = "commmate.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string GetConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, ConfigFileName);
    }
    
    public static AppConfig LoadConfig()
    {
        var configPath = GetConfigPath();
        
        if (!File.Exists(configPath))
        {
            return new AppConfig();
        }
        
        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return config ?? new AppConfig();
        }
        catch (Exception ex)
        {
            // 如果加载失败，返回空配置
            System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
            return new AppConfig();
        }
    }
    
    public static void SaveConfig(AppConfig config)
    {
        var configPath = GetConfigPath();
        
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置文件失败: {ex.Message}");
        }
    }
}
