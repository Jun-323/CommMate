using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommMate.Services;
using CommMate.ViewModels;

namespace CommMate;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;

    public MainWindow()
    {
        InitializeComponent();
        _vm = (MainViewModel)DataContext;
        _vm.Terminal.OnTerminalOutput += OnTerminalOutput;

        // Load window icon from embedded resource
        var iconUri = new Uri("pack://application:,,,/Resources/app.ico");
        using var stream = Application.GetResourceStream(iconUri)?.Stream;
        if (stream != null)
        {
            var decoder = new IconBitmapDecoder(stream,
                BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Icon = decoder.Frames[decoder.Frames.Count - 1]; // use highest resolution
        }

        _vm.Serial.Log.OnNewLine += OnSerialLogLine;
        _vm.Network.Log.OnNewLine += OnNetworkLogLine;

        TabSerial.Checked += (_, _) => ShowPanel("Serial");
        TabNetwork.Checked += (_, _) => ShowPanel("Network");
        TabTerminal.Checked += (_, _) => ShowPanel("Terminal");

        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => DateTimeText.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clockTimer.Start();

        I18nService.Instance.OnLanguageChanged += UpdateMenuTexts;
        UpdateMenuTexts();

        // 加载并应用配置
        var config = ConfigService.LoadConfig();
        _vm.ApplyConfig(config);
        _vm.Serial.ApplyConfig(config);
        _vm.Network.ApplyConfig(config);
    }

    private void UpdateMenuTexts()
    {
        var i = I18nService.Instance;
        Title = i.T("App.Title");
        MenuFile.Header = i.T("Menu.File");
        ((MenuItem)MenuFile.Items[0]).Header = i.T("Menu.File.Exit");
        MenuView.Header = i.T("Menu.View");
        MenuLanguage.Header = i.T("Menu.View.Language");
        MenuTheme.Header = i.T("Menu.View.Theme");
        MenuHelp.Header = i.T("Menu.Help");
        ((MenuItem)MenuHelp.Items[0]).Header = i.T("Menu.Help.About");
        TabSerial.Content = i.T("Tab.Serial");
        TabNetwork.Content = i.T("Tab.Network");
        TabTerminal.Content = i.T("Tab.Terminal");

        // Sync language checkmark
        var isZh = I18nService.Instance.CurrentLanguage == AppLanguage.Chinese;
        MenuLangZh.IsChecked = isZh;
        MenuLangEn.IsChecked = !isZh;
    }

    private void ShowPanel(string name)
    {
        SerialPanel.Visibility = name == "Serial" ? Visibility.Visible : Visibility.Collapsed;
        NetworkPanel.Visibility = name == "Network" ? Visibility.Visible : Visibility.Collapsed;
        TerminalPanel.Visibility = name == "Terminal" ? Visibility.Visible : Visibility.Collapsed;

        _vm.SelectedTabIndex = name switch
        {
            "Network" => 1,
            "Terminal" => 2,
            _ => 0
        };

        if (name == "Terminal")
            TerminalDisplay.Focus();
    }

    private void ScrollSerialToEnd()
    {
        if (_vm.Serial.AutoScroll)
            SerialRecvBox.ScrollToEnd();
    }

    private void OnSerialLogLine(string text)
    {
        Dispatcher.BeginInvoke(() =>
        {
            SerialRecvBox.AppendText(text);
            ScrollSerialToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ScrollNetworkToEnd()
    {
        if (_vm.Network.AutoScroll)
            NetworkRecvBox.ScrollToEnd();
    }

    private void OnNetworkLogLine(string text)
    {
        Dispatcher.BeginInvoke(() =>
        {
            NetworkRecvBox.AppendText(text);
            ScrollNetworkToEnd();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void LangZh_Click(object sender, RoutedEventArgs e)
    {
        I18nService.Instance.CurrentLanguage = AppLanguage.Chinese;
    }

    private void LangEn_Click(object sender, RoutedEventArgs e)
    {
        I18nService.Instance.CurrentLanguage = AppLanguage.English;
    }

    private void ThemeLight_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsDarkTheme = false;
    }

    private void ThemeDark_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsDarkTheme = true;
    }

    private void SendBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+Enter: insert newline
                var caretIndex = textBox.CaretIndex;
                var text = textBox.Text;
                textBox.Text = text.Insert(caretIndex, Environment.NewLine);
                textBox.CaretIndex = caretIndex + Environment.NewLine.Length;
            }
            else
            {
                // Enter: trigger send
                e.Handled = true;
                if (textBox == SerialSendBox)
                    _vm.Serial.SendCommand.Execute(null);
                else if (textBox == NetworkSendBox)
                    _vm.Network.SendCommand.Execute(null);
            }
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(I18nService.Instance.T("About.Text"), "CommMate",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Terminal input handling
    private void TerminalDisplay_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!_vm.Terminal.IsConnected) return;
        _vm.Terminal.SendKeyInput(e.Text);
        e.Handled = true;
    }

    private void TerminalDisplay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_vm.Terminal.IsConnected)
        {
            e.Handled = true;
            return;
        }

        byte[]? data = e.Key switch
        {
            Key.Enter => new byte[] { 0x0D },
            Key.Back => new byte[] { 0x08 },
            Key.Tab => new byte[] { 0x09 },
            Key.Escape => new byte[] { 0x1B },
            Key.Up => new byte[] { 0x1B, 0x5B, 0x41 },
            Key.Down => new byte[] { 0x1B, 0x5B, 0x42 },
            Key.Right => new byte[] { 0x1B, 0x5B, 0x43 },
            Key.Left => new byte[] { 0x1B, 0x5B, 0x44 },
            Key.Home => new byte[] { 0x1B, 0x5B, 0x48 },
            Key.End => new byte[] { 0x1B, 0x5B, 0x46 },
            Key.Delete => new byte[] { 0x1B, 0x5B, 0x33, 0x7E },
            Key.F1 => new byte[] { 0x1B, 0x4F, 0x50 },
            Key.F2 => new byte[] { 0x1B, 0x4F, 0x51 },
            Key.F3 => new byte[] { 0x1B, 0x4F, 0x52 },
            Key.F4 => new byte[] { 0x1B, 0x4F, 0x53 },
            Key.F5 => new byte[] { 0x1B, 0x5B, 0x31, 0x35, 0x7E },
            Key.F6 => new byte[] { 0x1B, 0x5B, 0x31, 0x37, 0x7E },
            Key.F7 => new byte[] { 0x1B, 0x5B, 0x31, 0x38, 0x7E },
            Key.F8 => new byte[] { 0x1B, 0x5B, 0x31, 0x39, 0x7E },
            Key.F9 => new byte[] { 0x1B, 0x5B, 0x32, 0x30, 0x7E },
            Key.F10 => new byte[] { 0x1B, 0x5B, 0x32, 0x31, 0x7E },
            Key.F11 => new byte[] { 0x1B, 0x5B, 0x32, 0x33, 0x7E },
            Key.F12 => new byte[] { 0x1B, 0x5B, 0x32, 0x34, 0x7E },
            _ => null
        };

        if (data != null)
        {
            _vm.Terminal.SendKeyInput(Encoding.ASCII.GetString(data));
            e.Handled = true;
        }
    }

    private FlowDocument? _terminalDoc;
    private Paragraph? _terminalPara;

    private void OnTerminalOutput(string content)
    {
        // 首次创建 FlowDocument / Paragraph，后续复用
        if (_terminalDoc == null)
        {
            _terminalDoc = new FlowDocument();
            _terminalPara = new Paragraph { Margin = new Thickness(0), LineHeight = 1 };
            _terminalDoc.Blocks.Add(_terminalPara);
            TerminalDisplay.Document = _terminalDoc;
        }

        var para = _terminalPara!;
        var inlines = para.Inlines;
        inlines.Clear();

        if (!string.IsNullOrEmpty(content))
        {
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var text = i < lines.Length - 1 ? lines[i] + "\n" : lines[i];
                inlines.Add(new Run(text)
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = _vm.Terminal.FontSize
                });
            }
        }

        TerminalDisplay.ScrollToEnd();
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    /// <summary>
    /// 设置窗口标题栏的深色/浅色模式（需要 Windows 10 20H1+ 或 Windows 11）
    /// </summary>
    public static void ApplyTitleBarTheme(Window window, bool dark)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            var hwnd = helper.EnsureHandle();
            if (hwnd != IntPtr.Zero)
            {
                int useDarkMode = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
            }
        }
        catch
        {
            // DWM 不可用时静默忽略
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop();
        _vm.Serial.Log.OnNewLine -= OnSerialLogLine;
        _vm.Network.Log.OnNewLine -= OnNetworkLogLine;
        _vm.Terminal.OnTerminalOutput -= OnTerminalOutput;
        
        // 保存所有配置
        var config = ConfigService.LoadConfig();
        _vm.UpdateConfig(config);
        _vm.Serial.UpdateConfig(config);
        _vm.Network.UpdateConfig(config);
        ConfigService.SaveConfig(config);

        // 释放底层资源（串口、网络、WMI 监听、定时器等）
        _vm.Serial.Dispose();
        _vm.Network.Dispose();
        _vm.Terminal.Dispose();
        
        base.OnClosed(e);
    }
}
