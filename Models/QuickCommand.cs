using CommunityToolkit.Mvvm.ComponentModel;

namespace CommMate.Models;

public partial class QuickCommand : ObservableObject
{
    [ObservableProperty] private string _commandText = "";
    [ObservableProperty] private bool _isHex;

    public QuickCommand() { }

    public QuickCommand(string commandText, bool isHex = false)
    {
        _commandText = commandText;
        _isHex = isHex;
    }

    public QuickCommand Clone() => new(CommandText, IsHex);
}
