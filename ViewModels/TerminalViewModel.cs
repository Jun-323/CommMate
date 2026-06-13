using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommMate.Models;
using CommMate.Services;

namespace CommMate.ViewModels;

public partial class TerminalViewModel : ObservableObject, IDisposable
{
    private readonly SerialPortService _serial = new();
    private readonly TerminalEmulator _terminal = new(80, 24);
    private readonly DispatcherTimer _updateTimer;
    // 缓存 UI 线程的 Dispatcher
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private bool _pendingUpdate;
    private bool _disposed;

    [ObservableProperty] private string _selectedPort = "";
    [ObservableProperty] private int _selectedBaudRate = 115200;
    [ObservableProperty] private int _selectedBaudIndex = 7;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _localEcho = true;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private int _fontSize = 14;
    // 终端协议里 ESC ] 0;title BEL 改窗口标题
    [ObservableProperty] private string _windowTitle = "";

    public event Action<string>? OnTerminalOutput;

    public System.Collections.ObjectModel.ObservableCollection<string> PortNames { get; } = new();
    public int[] BaudRates { get; } = { 110, 300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 56000, 57600, 115200, 128000, 256000, 460800, 921600 };

    public TerminalViewModel()
    {
        StatusText = I18nService.Instance.T("Status.Ready");
        _serial.DataReceived += OnDataReceived;
        _serial.ErrorOccurred += OnError;
        _serial.ConnectionChanged += OnConnectionChanged;
        _terminal.OnBeep += _ => System.Media.SystemSounds.Beep.Play();
        _terminal.OnSendResponse += data => _serial.Send(data);
        _terminal.OnTitleChanged += title => _dispatcher.Invoke(() => WindowTitle = title);

        // 批处理定时器：每 30ms 合并一次终端渲染，避免逐字节重建 FlowDocument
        _updateTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(30), DispatcherPriority.Background,
            (_, _) =>
            {
                if (!_pendingUpdate) return;
                _pendingUpdate = false;
                var content = _terminal.GetVisibleContent();
                OnTerminalOutput?.Invoke(string.Join("\r\n", content));
            }, _dispatcher);
        _updateTimer.Stop();

        RefreshPorts();
    }

    private void OnDataReceived(byte[] data)
    {
        _terminal.ProcessData(data);
        _pendingUpdate = true;
    }

    private void OnError(string msg)
    {
        _dispatcher.Invoke(() =>
        {
            StatusText = $"⚠️ {msg}";
        });
    }

    private void OnConnectionChanged(bool connected)
    {
        _dispatcher.Invoke(() =>
        {
            IsConnected = connected;
            if (!connected) _updateTimer.Stop();
            StatusText = connected
                ? $"{I18nService.Instance.T("Status.Connected")} ({SelectedPort} @ {SelectedBaudRate})"
                : I18nService.Instance.T("Status.Disconnected");
        });
    }

    [RelayCommand]
    public void RefreshPorts()
    {
        PortNames.Clear();
        foreach (var port in _serial.GetAvailablePorts())
            PortNames.Add(port);
    }

    [RelayCommand]
    public async Task ConnectDisconnect()
    {
        if (IsConnected)
        {
            _updateTimer.Stop();
            await _serial.CloseAsync();
        }
        else
        {
            if (string.IsNullOrEmpty(SelectedPort)) return;

            _serial.Config.PortName = SelectedPort;
            _serial.Config.BaudRate = SelectedBaudRate;
            _serial.Config.DataBits = 8;
            _serial.Config.StopBits = StopBits.One;
            _serial.Config.Parity = Parity.None;
            _serial.Config.FlowControl = Handshake.None;
            _serial.Config.NewLine = "\r";
            _serial.Open();
            _terminal.ClearScreen();
            _updateTimer.Start();
        }
    }

    public void SendKeyInput(string text)
    {
        if (!IsConnected) return;
        var data = Encoding.ASCII.GetBytes(text);
        _serial.Send(data);
        if (LocalEcho)
        {
            _terminal.ProcessData(data);
            _pendingUpdate = true;
        }
    }

    [RelayCommand]
    public void ClearScreen()
    {
        _terminal.ClearScreen();
        OnTerminalOutput?.Invoke(string.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _updateTimer.Stop();

        _serial.DataReceived -= OnDataReceived;
        _serial.ErrorOccurred -= OnError;
        _serial.ConnectionChanged -= OnConnectionChanged;

        _serial.Dispose();
    }
}
