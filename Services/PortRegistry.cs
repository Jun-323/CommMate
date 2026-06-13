using System.Collections.Concurrent;

namespace CommMate.Services;

/// <summary>
/// 端口占用量管理器。
/// 防止 SerialViewModel 和 TerminalViewModel 同时打开同一物理端口。
/// </summary>
public static class PortRegistry
{
    /// <summary>
    /// portName(大写) → 持有该端口的 SerialPortService 实例
    /// </summary>
    private static readonly ConcurrentDictionary<string, SerialPortService> _openPorts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 尝试为 service 获取 portName 的独占权。
    /// 返回 true 表示获取成功（或该 service 已持有该端口）；
    /// 返回 false 表示端口已被其他 service 占用，error 中有提示文本。
    /// </summary>
    public static bool TryAcquire(string? portName, SerialPortService service, out string? error)
    {
        error = null;
        if (string.IsNullOrEmpty(portName))
            return true;

        // 清理已释放（GC 回收）的条目
        var deadKeys = _openPorts
            .Where(kvp => kvp.Value == null || kvp.Value.IsDisposed)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in deadKeys)
            _openPorts.TryRemove(key, out _);

        // 乐观插入
        bool acquired = _openPorts.TryAdd(portName, service);

        if (acquired)
            return true;

        // 已存在条目：检查是否是同一个 service（允许重入）
        if (_openPorts.TryGetValue(portName, out var existing) && ReferenceEquals(existing, service))
            return true;

        error = $"端口 {portName} 已被其他标签页占用，请先关闭后再试。";
        return false;
    }

    /// <summary>
    /// 释放 portName 的独占权（仅当持有者是 service 时才真正移除）。
    /// </summary>
    public static void Release(string? portName, SerialPortService service)
    {
        if (string.IsNullOrEmpty(portName))
            return;

        if (_openPorts.TryGetValue(portName, out var existing) && ReferenceEquals(existing, service))
            _openPorts.TryRemove(portName, out _);
    }
}
