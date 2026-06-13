using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommMate.Services;

namespace CommMate.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private string _statusText = "";

    public SerialViewModel Serial { get; } = new();
    public NetworkViewModel Network { get; } = new();
    public TerminalViewModel Terminal { get; } = new();

    public MainViewModel()
    {
        // 任意子面板状态变化时，刷新主状态栏
        Serial.PropertyChanged += (_, e) =>
        { if (e.PropertyName == nameof(SerialViewModel.StatusText)) RefreshStatus(); };
        Network.PropertyChanged += (_, e) =>
        { if (e.PropertyName == nameof(NetworkViewModel.StatusText)) RefreshStatus(); };
        Terminal.PropertyChanged += (_, e) =>
        { if (e.PropertyName == nameof(TerminalViewModel.StatusText)) RefreshStatus(); };
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplyTheme(value);
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        StatusText = SelectedTabIndex switch
        {
            0 => Serial.StatusText,
            1 => Network.StatusText,
            2 => Terminal.StatusText,
            _ => ""
        };
    }

    [RelayCommand]
    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    public void ApplyTheme(bool dark)
    {
        var window = System.Windows.Application.Current?.MainWindow;
        if (window == null) return;

        if (dark)
        {
            // VS Code-inspired dark palette
            window.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 30, 30));
            window.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(204, 204, 204));

            window.Resources["PanelBgBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(37, 37, 38));
            window.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(45, 45, 45));
            window.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(62, 62, 62));
            window.Resources["ControlBgBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(60, 60, 60));
            window.Resources["TextPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(204, 204, 204));
            window.Resources["TextSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(153, 153, 153));

            window.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(100, 181, 246));
            window.Resources["AccentLightBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 60, 100));
            window.Resources["SuccessBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(76, 175, 80));
            window.Resources["DangerBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(244, 67, 54));
            window.Resources["GrayBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(120, 120, 120));

            MainWindow.ApplyTitleBarTheme(window, true);
        }
        else
        {
            window.Background = System.Windows.SystemColors.WindowBrush;
            window.Foreground = System.Windows.SystemColors.WindowTextBrush;

            window.Resources["PanelBgBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(250, 250, 250));
            window.Resources["SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(245, 245, 245));
            window.Resources["BorderBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(208, 208, 208));
            window.Resources["ControlBgBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Colors.White);
            window.Resources["TextPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 30, 30));
            window.Resources["TextSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(102, 102, 102));

            window.Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(33, 150, 243));
            window.Resources["AccentLightBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(187, 222, 251));
            window.Resources["SuccessBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(76, 175, 80));
            window.Resources["DangerBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(244, 67, 54));
            window.Resources["GrayBrush"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(158, 158, 158));

            MainWindow.ApplyTitleBarTheme(window, false);
        }
    }

    public void ApplyConfig(AppConfig config)
    {
        if (config == null) return;
        IsDarkTheme = config.IsDarkTheme;
        I18nService.Instance.CurrentLanguage = config.Language == "en"
            ? AppLanguage.English
            : AppLanguage.Chinese;
    }

    public void UpdateConfig(AppConfig config)
    {
        if (config == null) return;
        config.IsDarkTheme = IsDarkTheme;
        config.Language = I18nService.Instance.CurrentLanguage == AppLanguage.Chinese ? "zh" : "en";
    }
}
