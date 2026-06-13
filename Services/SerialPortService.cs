using System.IO.Ports;
using System.Management;
using CommMate.Models;

namespace CommMate.Services;

public class SerialPortService : IDisposable
{
    private SerialPort? _serialPort;
    private CancellationTokenSource? _readCts;
    private ManagementEventWatcher? _portWatcher;
    private bool _disposed;
    private readonly List<Task> _backgroundTasks = new();
    private readonly object _bgLock = new();
    private string? _currentPortName;

    public bool IsDisposed => _disposed;
    public bool IsOpen => _serialPort?.IsOpen ?? false;
    public SerialConfig Config { get; } = new();

    public event Action<byte[]>? DataReceived;
    public event Action<string>? ErrorOccurred;
    public event Action<bool>? ConnectionChanged;
    public event Action<string[]>? PortsChanged;

    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public void Open()
    {
        try
        {
            // 先向 PortRegistry 申请独占权
            if (!PortRegistry.TryAcquire(Config.PortName, this, out var err))
            {
                ErrorOccurred?.Invoke(err!);
                return;
            }

            _serialPort = new SerialPort(Config.PortName, Config.BaudRate, Config.Parity, Config.DataBits, Config.StopBits)
            {
                Handshake = Config.FlowControl,
                ReadTimeout = 500,
                WriteTimeout = 500,
                NewLine = Config.NewLine
            };

            _serialPort.ErrorReceived += OnErrorReceived;
            _serialPort.Open();

            _readCts = new CancellationTokenSource();
            var t = Task.Run(() => ReadLoop(_readCts.Token), _readCts.Token);
            TrackBackground(t);

            ConnectionChanged?.Invoke(true);
            _currentPortName = Config.PortName;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"打开串口失败: {ex.Message}");
            PortRegistry.Release(Config.PortName, this);
            _serialPort?.Dispose();
            _serialPort = null;
        }
    }

    // Close() 已移除：请改用 CloseAsync()，避免 UI 线程死锁。

    public bool Send(byte[] data)
    {
        if (_serialPort?.IsOpen != true) return false;
        try
        {
            _serialPort.Write(data, 0, data.Length);
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"发送失败: {ex.Message}");
            return false;
        }
    }

    private async Task ReadLoop(CancellationToken ct)
    {
        var mode = Config.FramingMode;

        if (mode == FramingMode.Streaming)
        {
            await ReadLoop_Streaming(ct);
        }
        else
        {
            await ReadLoop_Timeout(ct);
        }
    }

    /// <summary>流模式：有数据立即读出，不做缓存/拼包。适合连续数据传输。</summary>
    private async Task ReadLoop_Streaming(CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_serialPort?.IsOpen == true && _serialPort.BytesToRead > 0)
                {
                    var count = _serialPort.Read(buffer, 0, Math.Min(buffer.Length, _serialPort.BytesToRead));
                    if (count > 0)
                    {
                        var data = new byte[count];
                        Array.Copy(buffer, data, count);
                        DataReceived?.Invoke(data);
                    }
                }
                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"读取错误: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>超时帧模式：累积字节，隔 PacketTimeout ms 无新数据后作为一包发出。</summary>
    private async Task ReadLoop_Timeout(CancellationToken ct)
    {
        var readBuffer = new byte[4096];
        var accumulated = new List<byte>(4096);
        var gapTimer = new System.Diagnostics.Stopwatch();
        var timeout = Config.PacketTimeout;

        void Flush()
        {
            if (accumulated.Count == 0) return;
            var packet = accumulated.ToArray();
            accumulated.Clear();
            gapTimer.Reset();
            DataReceived?.Invoke(packet);
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_serialPort?.IsOpen == true && _serialPort.BytesToRead > 0)
                {
                    var available = Math.Min(readBuffer.Length, _serialPort.BytesToRead);
                    var count = _serialPort.Read(readBuffer, 0, available);
                    if (count > 0)
                    {
                        accumulated.AddRange(new ArraySegment<byte>(readBuffer, 0, count));
                        gapTimer.Restart();
                    }
                }
                else if (accumulated.Count > 0 && gapTimer.IsRunning && gapTimer.ElapsedMilliseconds >= timeout)
                {
                    Flush();
                }

                if (accumulated.Count > 65536)
                {
                    Flush();
                }

                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (TimeoutException) { }
            catch (Exception ex)
            {
                if (accumulated.Count > 0) Flush();
                ErrorOccurred?.Invoke($"读取错误: {ex.Message}");
                break;
            }
        }

        if (accumulated.Count > 0) Flush();
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke($"串口错误: {e.EventType}");
    }

    public void StartPortMonitoring()
    {
        // 先停掉已有的 watcher，防止重复启动
        StopPortMonitoring();

        try
        {
            // 显式指定 polling interval（WITHIN 子句），避免某些系统上 WMI 默认 1s 触发额外回调
            _portWatcher = new ManagementEventWatcher(
                new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WITHIN 2"));
            // 使用异步延迟以避免阻塞 WMI 事件线程
            _portWatcher.EventArrived += async (s, e) =>
            {
                try
                {
                    await Task.Delay(500);
                    PortsChanged?.Invoke(GetAvailablePorts());
                }
                catch { }
            };
            _portWatcher.Start();
        }
        catch
        {
            // WMI may not be available, fallback silently
        }
    }

    public void StopPortMonitoring()
    {
        try
        {
            _portWatcher?.Stop();
            _portWatcher?.Dispose();
        }
        catch
        {
            // 忽略停止时的异常
        }
        _portWatcher = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopPortMonitoring();

        // 同步：立即取消读取、关闭端口（快速操作，不阻塞）
        try { _readCts?.Cancel(); } catch { }

        try { _serialPort?.Close(); } catch { }
        try { _serialPort?.Dispose(); } catch { }
        _serialPort = null;

        try { _readCts?.Dispose(); } catch { }
        _readCts = null;

        ConnectionChanged?.Invoke(false);

        PortRegistry.Release(_currentPortName, this);
        _currentPortName = null;

        // 异步等待后台任务退出（不阻塞 Dispose，超时 5 秒自动放弃）
        _ = CleanupBackgroundTasksAsync();
    }

    private async Task CleanupBackgroundTasksAsync()
    {
        Task[] tasks;
        lock (_bgLock)
        {
            tasks = _backgroundTasks.ToArray();
        }

        if (tasks.Length > 0)
        {
            try
            {
                var wait = Task.WhenAll(tasks);
                var timeout = Task.Delay(TimeSpan.FromSeconds(5));
                var completed = await Task.WhenAny(wait, timeout);
                if (completed != wait)
                {
                    System.Diagnostics.Debug.WriteLine("串口后台任务未能在超时内退出");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"等待串口后台任务时发生异常: {ex.Message}");
            }
        }
    }

    private void TrackBackground(Task t)
    {
        lock (_bgLock)
        {
            _backgroundTasks.Add(t);
        }
        t.ContinueWith(_ =>
        {
            lock (_bgLock)
            {
                _backgroundTasks.Remove(t);
            }
        }, TaskScheduler.Default);
    }

    public async Task CloseAsync()
    {
        try
        {
            _readCts?.Cancel();

            Task[] tasks;
            lock (_bgLock)
            {
                tasks = _backgroundTasks.ToArray();
            }

            if (tasks.Length > 0)
            {
                try
                {
                    var wait = Task.WhenAll(tasks);
                    var timeout = Task.Delay(TimeSpan.FromSeconds(5));
                    var completed = await Task.WhenAny(wait, timeout);
                    if (completed != wait)
                    {
                        ErrorOccurred?.Invoke("串口后台任务未能在超时内退出");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"等待串口后台任务时发生异常: {ex.Message}");
                }
            }

            try { _serialPort?.Close(); } catch (Exception ex) { ErrorOccurred?.Invoke($"关闭串口异常: {ex.Message}"); }
            try { _serialPort?.Dispose(); } catch { }
            _serialPort = null;

            try { _readCts?.Dispose(); } catch { }
            _readCts = null;

            ConnectionChanged?.Invoke(false);

            // 释放端口独占权
            PortRegistry.Release(_currentPortName, this);
            _currentPortName = null;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"关闭串口时发生错误: {ex.Message}");
        }
    }
}
