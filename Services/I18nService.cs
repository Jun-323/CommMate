using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CommMate.Services;

public enum AppLanguage
{
    Chinese,
    English
}

public class I18nService : INotifyPropertyChanged
{
    private static I18nService? _instance;
    public static I18nService Instance => _instance ??= new I18nService();

    private AppLanguage _currentLanguage = AppLanguage.Chinese;

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged();
                OnLanguageChanged?.Invoke();
            }
        }
    }

    public int LanguageIndex
    {
        get => (int)_currentLanguage;
        set => CurrentLanguage = (AppLanguage)value;
    }

    public event Action? OnLanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Dictionary<string, Dictionary<string, string>> _strings = new()
    {
        ["zh"] = new()
        {
            ["App.Title"] = "CommMate - 串口网络调试助手",
            ["Menu.File"] = "文件",
            ["Menu.File.Exit"] = "退出",
            ["Menu.View"] = "视图",
            ["Menu.View.Language"] = "语言",
            ["Menu.View.Theme"] = "主题",
            ["Menu.Help"] = "帮助",
            ["Menu.Help.About"] = "关于",
            ["Tab.Serial"] = "串口调试",
            ["Tab.Network"] = "网络调试",
            ["Tab.Terminal"] = "终端模式",
            ["Serial.Port"] = "串口",
            ["Serial.BaudRate"] = "波特率",
            ["Serial.DataBits"] = "数据位",
            ["Serial.StopBits"] = "停止位",
            ["Serial.Parity"] = "校验位",
            ["Serial.FlowControl"] = "流控",
            ["Serial.NewLine"] = "换行符",
            ["Serial.Open"] = "打开串口",
            ["Serial.Close"] = "关闭串口",
            ["Serial.Refresh"] = "刷新",
            ["Serial.HexSend"] = "Hex 发送",
            ["Serial.HexRecv"] = "Hex 接收",
            ["Serial.Timestamp"] = "时间戳",
            ["Serial.AutoScroll"] = "自动滚动",
            ["Serial.Send"] = "发送",
            ["Serial.Clear"] = "清空",
            ["Serial.SaveLog"] = "保存日志",
            ["Serial.TimedSend"] = "定时发送",
            ["Serial.IntervalMs"] = "间隔(ms)",
            ["Serial.BytesSent"] = "发送字节",
            ["Serial.BytesRecv"] = "接收字节",
            ["Network.Mode"] = "模式",
            ["Network.RemoteHost"] = "远程地址",
            ["Network.RemotePort"] = "远程端口",
            ["Network.LocalPort"] = "本地端口",
            ["Network.Broadcast"] = "广播",
            ["Network.Connect"] = "连接",
            ["Network.Disconnect"] = "断开",
            ["Network.Listen"] = "开始监听",
            ["Network.Stop"] = "停止监听",
            ["Network.Clients"] = "客户端数",
            ["Network.HexSend"] = "Hex 发送",
            ["Network.HexRecv"] = "Hex 接收",
            ["Network.Timestamp"] = "时间戳",
            ["Network.Send"] = "发送",
            ["Network.Clear"] = "清空",
            ["Network.SaveLog"] = "保存日志",
            ["Network.BytesSent"] = "发送字节",
            ["Network.BytesRecv"] = "接收字节",
            ["Terminal.BaudRate"] = "波特率",
            ["Terminal.Port"] = "串口",
            ["Terminal.Connect"] = "连接",
            ["Terminal.Disconnect"] = "断开",
            ["Terminal.LocalEcho"] = "本地回显",
            ["Terminal.Clear"] = "清屏",
            ["Terminal.FontSize"] = "字号",
            ["Status.Ready"] = "就绪",
            ["Status.Connected"] = "已连接",
            ["Status.Disconnected"] = "已断开",
            ["Status.Error"] = "错误",
            ["Status.PortUnavailable"] = "串口不可用",
            ["About.Text"] = "CommMate v1.0\n串口网络调试助手\n支持串口、TCP/UDP、终端模式",
            ["Theme.Light"] = "浅色",
            ["Theme.Dark"] = "深色",
            ["Quick.Title"] = "常用指令",
            ["Quick.Add"] = "添加当前",
            ["Quick.Edit"] = "编辑",
            ["Quick.Delete"] = "删除",
            ["Quick.Command"] = "指令",
            ["Quick.HexMode"] = "Hex",
            ["Quick.Send"] = "发送",
            ["Quick.ConfirmDelete"] = "确定删除选中指令?",
        },
        ["en"] = new()
        {
            ["App.Title"] = "CommMate - Serial & Network Debug Tool",
            ["Menu.File"] = "File",
            ["Menu.File.Exit"] = "Exit",
            ["Menu.View"] = "View",
            ["Menu.View.Language"] = "Language",
            ["Menu.View.Theme"] = "Theme",
            ["Menu.Help"] = "Help",
            ["Menu.Help.About"] = "About",
            ["Tab.Serial"] = "Serial",
            ["Tab.Network"] = "Network",
            ["Tab.Terminal"] = "Terminal",
            ["Serial.Port"] = "Port",
            ["Serial.BaudRate"] = "Baud Rate",
            ["Serial.DataBits"] = "Data Bits",
            ["Serial.StopBits"] = "Stop Bits",
            ["Serial.Parity"] = "Parity",
            ["Serial.FlowControl"] = "Flow Control",
            ["Serial.NewLine"] = "New Line",
            ["Serial.Open"] = "Open",
            ["Serial.Close"] = "Close",
            ["Serial.Refresh"] = "Refresh",
            ["Serial.HexSend"] = "Hex Send",
            ["Serial.HexRecv"] = "Hex Recv",
            ["Serial.Timestamp"] = "Timestamp",
            ["Serial.AutoScroll"] = "Auto Scroll",
            ["Serial.Send"] = "Send",
            ["Serial.Clear"] = "Clear",
            ["Serial.SaveLog"] = "Save Log",
            ["Serial.TimedSend"] = "Timed Send",
            ["Serial.IntervalMs"] = "Interval(ms)",
            ["Serial.BytesSent"] = "TX Bytes",
            ["Serial.BytesRecv"] = "RX Bytes",
            ["Network.Mode"] = "Mode",
            ["Network.RemoteHost"] = "Remote Host",
            ["Network.RemotePort"] = "Remote Port",
            ["Network.LocalPort"] = "Local Port",
            ["Network.Broadcast"] = "Broadcast",
            ["Network.Connect"] = "Connect",
            ["Network.Disconnect"] = "Disconnect",
            ["Network.Listen"] = "Listen",
            ["Network.Stop"] = "Stop",
            ["Network.Clients"] = "Clients",
            ["Network.HexSend"] = "Hex Send",
            ["Network.HexRecv"] = "Hex Recv",
            ["Network.Timestamp"] = "Timestamp",
            ["Network.Send"] = "Send",
            ["Network.Clear"] = "Clear",
            ["Network.SaveLog"] = "Save Log",
            ["Network.BytesSent"] = "TX Bytes",
            ["Network.BytesRecv"] = "RX Bytes",
            ["Terminal.BaudRate"] = "Baud Rate",
            ["Terminal.Port"] = "Port",
            ["Terminal.Connect"] = "Connect",
            ["Terminal.Disconnect"] = "Disconnect",
            ["Terminal.LocalEcho"] = "Local Echo",
            ["Terminal.Clear"] = "Clear",
            ["Terminal.FontSize"] = "Font Size",
            ["Status.Ready"] = "Ready",
            ["Status.Connected"] = "Connected",
            ["Status.Disconnected"] = "Disconnected",
            ["Status.Error"] = "Error",
            ["Status.PortUnavailable"] = "Port unavailable",
            ["About.Text"] = "CommMate v1.0\nSerial & Network Debug Tool\nSupports Serial, TCP/UDP, Terminal Mode",
            ["Theme.Light"] = "Light",
            ["Theme.Dark"] = "Dark",
            ["Quick.Title"] = "Quick Commands",
            ["Quick.Add"] = "Add Current",
            ["Quick.Edit"] = "Edit",
            ["Quick.Delete"] = "Delete",
            ["Quick.Command"] = "Command",
            ["Quick.HexMode"] = "Hex",
            ["Quick.Send"] = "Send",
            ["Quick.ConfirmDelete"] = "Delete selected command?",
        }
    };

    public string T(string key)
    {
        var lang = _currentLanguage == AppLanguage.Chinese ? "zh" : "en";
        if (_strings.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var value))
            return value;
        return key;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
