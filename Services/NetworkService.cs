using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using CommMate.Models;

namespace CommMate.Services;

public class NetworkService : IDisposable
{
    private TcpClient? _tcpClient;
    private TcpListener? _tcpListener;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private readonly List<Task> _backgroundTasks = new();
    private readonly object _bgLock = new();
    private readonly ConcurrentDictionary<string, TcpClient> _tcpClients = new();
    private readonly ConcurrentDictionary<string, NetworkClientInfo> _clientInfos = new();
    private IPEndPoint? _lastUdpRemoteEndPoint;
    private bool _disposed;

    public bool IsConnected { get; private set; }
    public bool IsListening { get; private set; }
    public int ClientCount => _tcpClients.Count;
    public NetworkConfig Config { get; } = new();
    
    public IReadOnlyCollection<NetworkClientInfo> ClientInfos => _clientInfos.Values.ToList().AsReadOnly();

    public event Action<byte[], string?>? DataReceived;
    public event Action<string>? ErrorOccurred;
    public event Action<bool>? ConnectionChanged;
    public event Action<int>? ClientCountChanged;
    public event Action<NetworkClientInfo>? ClientConnected;
    public event Action<string>? ClientDisconnected;

    public async Task ConnectAsync()
    {
        try
        {
            _cts = new CancellationTokenSource();

            switch (Config.Mode)
            {
                case NetworkMode.TcpClient:
                    await ConnectTcpClientAsync(_cts.Token);
                    break;
                case NetworkMode.TcpServer:
                    await StartTcpServerAsync(_cts.Token);
                    break;
                case NetworkMode.Udp:
                    StartUdp(_cts.Token);
                    break;
            }

            IsConnected = true;
            ConnectionChanged?.Invoke(true);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"连接失败: {ex.Message}");
        }
    }

    private async Task ConnectTcpClientAsync(CancellationToken ct)
    {
        var localAddr = ResolveLocalBindAddress();
        if (localAddr != null && !IPAddress.Any.Equals(localAddr))
        {
            _tcpClient = new TcpClient(new IPEndPoint(localAddr, 0));
        }
        else
        {
            _tcpClient = new TcpClient();
        }
        // 关闭 Nagle 算法：AT 响应等小包不需要合并，能显著降低首字节延迟
        _tcpClient.NoDelay = true;
        await _tcpClient.ConnectAsync(Config.RemoteHost, Config.RemotePort, ct);
        var t = Task.Run(() => ReadTcpStream(_tcpClient.GetStream(), "TCP", ct), ct);
        TrackBackground(t);
    }

    private async Task StartTcpServerAsync(CancellationToken ct)
    {
        var localAddr = ResolveLocalBindAddress();
        _tcpListener = new TcpListener(localAddr ?? IPAddress.Any, Config.LocalPort);
        _tcpListener.Start();
        IsListening = true;

        var acceptTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync(ct);
                    var id = Guid.NewGuid().ToString()[..8];
                    var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

                    _tcpClients[id] = client;
                    // 关闭 Nagle，避免小包合并引入延迟
                    client.NoDelay = true;
                    var info = new NetworkClientInfo
                    {
                        Id = id,
                        RemoteEndPoint = remoteEp,
                        ConnectedTime = DateTime.Now
                    };
                    _clientInfos[id] = info;

                    ClientCountChanged?.Invoke(_tcpClients.Count);
                    ClientConnected?.Invoke(info);

                    var readTask = Task.Run(() => ReadTcpStream(client.GetStream(), id, ct), ct);
                    TrackBackground(readTask);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke($"TCP Server 接受客户端失败: {ex.Message}");
                    // 仅退出当前 accept 循环，不break整个监听
                }
            }
        }, ct);
        TrackBackground(acceptTask);
    }

    private void StartUdp(CancellationToken ct)
    {
        var localAddr = ResolveLocalBindAddress();
        if (localAddr != null && !IPAddress.Any.Equals(localAddr))
        {
            _udpClient = new UdpClient(new IPEndPoint(localAddr, Config.LocalPort));
        }
        else
        {
            _udpClient = new UdpClient(Config.LocalPort);
        }
        if (Config.EnableBroadcast)
            _udpClient.EnableBroadcast = true;
        var t = Task.Run(() => ReadUdpLoop(ct), ct);
        TrackBackground(t);
    }

    private IPAddress? ResolveLocalBindAddress()
    {
        if (string.IsNullOrEmpty(Config.LocalBindAddress) || Config.LocalBindAddress == "0.0.0.0")
            return IPAddress.Any;
        
        if (IPAddress.TryParse(Config.LocalBindAddress, out var addr))
            return addr;
        
        return IPAddress.Any;
    }

    private async Task ReadTcpStream(NetworkStream stream, string clientId, CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var count = await stream.ReadAsync(buffer, ct);
                if (count == 0) break;
                var data = new byte[count];
                Array.Copy(buffer, data, count);
                DataReceived?.Invoke(data, clientId);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ex)
        {
            // 客户端正常断开（SocketException/IOException），静默处理
            System.Diagnostics.Debug.WriteLine($"TCP 客户端 [{clientId}] 断开: {ex.Message}");
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"TCP 读取异常 [{clientId}]: {ex.Message}");
        }
        finally
        {
            RemoveClient(clientId);
        }
    }

    private void RemoveClient(string clientId)
    {
        if (_tcpClients.TryRemove(clientId, out var tcpClient))
        {
            try { tcpClient.Dispose(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭客户端连接异常: {ex.Message}");
            }
        }
        if (_clientInfos.TryRemove(clientId, out _))
        {
            ClientDisconnected?.Invoke(clientId);
        }
        ClientCountChanged?.Invoke(_tcpClients.Count);
    }

    private async Task ReadUdpLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await _udpClient!.ReceiveAsync(ct);
                _lastUdpRemoteEndPoint = result.RemoteEndPoint;
                DataReceived?.Invoke(result.Buffer, result.RemoteEndPoint.ToString());
            }
        }
        catch (OperationCanceledException) { }
    }

    private void TrackBackground(Task t)
    {
        lock (_bgLock)
        {
            _backgroundTasks.Add(t);
        }
        // remove when completed
        t.ContinueWith(_ =>
        {
            lock (_bgLock)
            {
                _backgroundTasks.Remove(t);
            }
        }, TaskScheduler.Default);
    }

    public async Task<bool> SendAsync(byte[] data)
    {
        try
        {
            switch (Config.Mode)
            {
                case NetworkMode.TcpClient:
                    if (_tcpClient?.Connected == true)
                    {
                        await _tcpClient.GetStream().WriteAsync(data);
                        return true;
                    }
                    break;
                case NetworkMode.TcpServer:
                    var anySent = false;
                    foreach (var (clientId, client) in _tcpClients.ToArray())
                    {
                        try
                        {
                            if (client.Connected)
                            {
                                await client.GetStream().WriteAsync(data);
                                anySent = true;
                            }
                        }
                        catch
                        {
                            // 单客户端发送失败，移除并继续
                            RemoveClient(clientId);
                        }
                    }
                    return anySent;
                case NetworkMode.Udp:
                    if (_udpClient != null)
                    {
                        // 优先回复最近收到数据的来源，否则用配置的 RemoteHost
                        if (_lastUdpRemoteEndPoint != null)
                        {
                            await _udpClient.SendAsync(data, data.Length, _lastUdpRemoteEndPoint);
                        }
                        else
                        {
                            await _udpClient.SendAsync(data, data.Length, Config.RemoteHost, Config.RemotePort);
                        }
                        return true;
                    }
                    break;
            }
            return false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"发送失败: {ex.Message}");
            return false;
        }
    }

    public async Task SendToClientAsync(string clientId, byte[] data)
    {
        try
        {
            if (_tcpClients.TryGetValue(clientId, out var client) && client.Connected)
            {
                await client.GetStream().WriteAsync(data);
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"发送失败: {ex.Message}");
        }
    }

    public void DisconnectClient(string clientId)
    {
        RemoveClient(clientId);
    }

    public async Task DisconnectAsync()
    {
        try
        {
            // cancel ongoing operations
            _cts?.Cancel();

            // wait for background tasks to complete (with a timeout)
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
                        // not all tasks finished in time, attempt best-effort
                        ErrorOccurred?.Invoke("部分后台任务未能在超时内退出");
                    }
                }
                catch (Exception ex)
                {
                    // 捕获 Task.WhenAll 异常，继续清理
                    System.Diagnostics.Debug.WriteLine($"等待后台任务时发生异常: {ex.Message}");
                }
            }

            // close/stop network resources
            try { _tcpClient?.Close(); } catch { }
            try { _tcpClient?.Dispose(); } catch { }
            try { _tcpListener?.Stop(); } catch { }
            try { _udpClient?.Close(); } catch { }
            try { _udpClient?.Dispose(); } catch { }

            foreach (var (_, client) in _tcpClients)
            {
                try { client.Dispose(); } catch { }
            }
            _tcpClients.Clear();
            _clientInfos.Clear();
            _lastUdpRemoteEndPoint = null;

            _tcpClient = null;
            _tcpListener = null;
            _udpClient = null;

            // dispose and clear CTS
            try { _cts?.Dispose(); } catch { }
            _cts = null;

            IsConnected = false;
            IsListening = false;
            ConnectionChanged?.Invoke(false);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"断开连接时发生错误: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // 同步：立即取消操作、关闭网络资源（快速操作，不阻塞）
        try { _cts?.Cancel(); } catch { }

        try { _tcpClient?.Close(); } catch { }
        try { _tcpClient?.Dispose(); } catch { }
        try { _tcpListener?.Stop(); } catch { }
        try { _udpClient?.Close(); } catch { }
        try { _udpClient?.Dispose(); } catch { }

        foreach (var (_, client) in _tcpClients)
        {
            try { client.Dispose(); } catch { }
        }
        _tcpClients.Clear();
        _clientInfos.Clear();
        _lastUdpRemoteEndPoint = null;

        _tcpClient = null;
        _tcpListener = null;
        _udpClient = null;

        try { _cts?.Dispose(); } catch { }
        _cts = null;

        IsConnected = false;
        IsListening = false;
        ConnectionChanged?.Invoke(false);

        // 异步等待后台任务退出（不阻塞 Dispose）
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
                    System.Diagnostics.Debug.WriteLine("网络后台任务未能在超时内退出");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"等待网络后台任务时发生异常: {ex.Message}");
            }
        }
    }
}
