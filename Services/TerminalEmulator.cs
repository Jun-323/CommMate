using System.Text;

namespace CommMate.Services;

public struct TerminalCell
{
    public char Character;
    public ConsoleColor Foreground;
    public ConsoleColor Background;
    public bool Bold;
    public bool Underline;
    public bool Inverse;

    public TerminalCell()
    {
        Character = ' ';
        Foreground = ConsoleColor.Gray;
        Background = ConsoleColor.Black;
        Bold = false;
        Underline = false;
        Inverse = false;
    }

    public readonly TerminalCell Clone()
    {
        return new TerminalCell
        {
            Character = Character,
            Foreground = Foreground,
            Background = Background,
            Bold = Bold,
            Underline = Underline,
            Inverse = Inverse
        };
    }
}

public class TerminalEmulator
{
    private TerminalCell[,] _screen;
    private TerminalCell _currentAttrs = new();

    public int CursorRow { get; private set; }
    public int CursorCol { get; private set; }

    private int _savedCursorRow;
    private int _savedCursorCol;

    public int Rows { get; private set; }
    public int Cols { get; private set; }

    private int _scrollTop;
    private int _scrollBottom;

    private readonly StringBuilder _escapeBuffer = new();
    private bool _inEscape;
    private bool _isOsc;
    private readonly List<byte> _oscBytes = new();

    private bool _autoWrap;
    private bool _originMode;
    private bool _cursorVisible = true;
    public bool IsCursorVisible => _cursorVisible;

    // 备用屏幕缓冲区（?1049h/l）
    private TerminalCell[,]? _altScreen;
    private int _altCursorRow, _altCursorCol;
    private bool _inAltScreen;

    public event Action<string>? OnBeep;
    public event Action<byte[]>? OnSendResponse;
    public event Action<string>? OnTitleChanged;

    public TerminalEmulator(int cols = 80, int rows = 24)
    {
        Cols = cols;
        Rows = rows;
        _scrollTop = 0;
        _scrollBottom = rows - 1;
        _autoWrap = true; // VT100 默认开启自动换行
        _screen = new TerminalCell[rows, cols];
        ClearScreen();
    }

    public void Resize(int cols, int rows)
    {
        var oldRows = Rows;
        var oldCols = Cols;
        var oldScreen = _screen;

        Cols = cols;
        Rows = rows;
        _scrollTop = 0;
        _scrollBottom = rows - 1;
        _screen = new TerminalCell[rows, cols];

        // 保留旧屏幕上能容纳的内容
        var copyRows = Math.Min(oldRows, rows);
        var copyCols = Math.Min(oldCols, cols);
        for (var row = 0; row < copyRows; row++)
        {
            for (var col = 0; col < copyCols; col++)
            {
                _screen[row, col] = oldScreen[row, col];
            }
            // 新列多出的部分填空白
            for (var col = copyCols; col < cols; col++)
            {
                _screen[row, col] = new TerminalCell();
            }
        }
        // 新行多出的部分填空白
        for (var row = copyRows; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                _screen[row, col] = new TerminalCell();
            }
        }

        CursorRow = Math.Min(CursorRow, rows - 1);
        CursorCol = Math.Min(CursorCol, cols - 1);
    }

    public void ProcessData(byte[] data)
    {
        foreach (var b in data)
            ProcessByte(b);
    }

    public void ProcessByte(byte b)
    {
        if (_inEscape)
        {
            ProcessEscapeSequence(b);
            return;
        }

        if (b == 0x1B)
        {
            _inEscape = true;
            _escapeBuffer.Clear();
            _escapeBuffer.Append((char)b);
            return;
        }

        if (b == 0x0E)
        {
            // SO - Shift Out (ignore for now)
            return;
        }
        if (b == 0x0F)
        {
            // SI - Shift In (ignore for now)
            return;
        }

        switch (b)
        {
            case 0x07: OnBeep?.Invoke(""); break; // BEL
            case 0x08: CursorBack(); break; // BS
            case 0x09: CursorForwardTab(); break; // TAB
            case 0x0A: LineFeed(); break; // LF
            case 0x0D: CarriageReturn(); break; // CR
            default:
                if (b >= 32)
                {
                    PutChar((char)b);
                }
                break;
        }
    }

    private void ProcessEscapeSequence(byte b)
    {
        // OSC 序列：用原始字节收集，不走 StringBuilder
        if (_isOsc)
        {
            _oscBytes.Add(b);

            // BEL (0x07) 终止
            if (b == 0x07)
            {
                if (_oscBytes.Count > 1)
                    ProcessOsc(_oscBytes.ToArray()[..^1]);
                _isOsc = false;
                _oscBytes.Clear();
                return;
            }

            // ST (ESC \) 终止：检测最后两字节是否为 0x1B 0x5C
            if (_oscBytes.Count >= 2 &&
                _oscBytes[^2] == 0x1B && _oscBytes[^1] == 0x5C)
            {
                if (_oscBytes.Count > 2)
                    ProcessOsc(_oscBytes.ToArray()[..^2]);
                _isOsc = false;
                _oscBytes.Clear();
                return;
            }

            return;
        }

        _escapeBuffer.Append((char)b);
        var seq = _escapeBuffer.ToString();

        if (seq.Length == 2)
        {
            switch (b)
            {
                case (byte)'[': return;
                case (byte)']':
                    _isOsc = true;
                    _oscBytes.Clear();
                    return;
                case (byte)'(': return;
                case (byte)')': return;
                case (byte)'7': SaveCursor(); _inEscape = false; return;
                case (byte)'8': RestoreCursor(); _inEscape = false; return;
                case (byte)'D': ScrollDown(1); _inEscape = false; return;
                case (byte)'M': ScrollUp(1); _inEscape = false; return;
                case (byte)'E': NextLine(); _inEscape = false; return;
                case (byte)'H': SetTabStop(); _inEscape = false; return;
                case (byte)'c': FullReset(); _inEscape = false; return;
                default: _inEscape = false; return;
            }
        }

        // CSI 终止判断（原有逻辑保留）
        if (b is >= 0x40 and <= 0x7E)
        {
            var csi = seq;
            if (seq.StartsWith("\x1b["))
                csi = seq[2..];

            if (csi.EndsWith('\x07') || csi.EndsWith('\x1b'))
            {
                _inEscape = false;
                return;
            }

            if (csi.EndsWith('m') || csi.EndsWith('H') || csi.EndsWith('f') ||
                csi.EndsWith('A') || csi.EndsWith('B') || csi.EndsWith('C') || csi.EndsWith('D') ||
                csi.EndsWith('J') || csi.EndsWith('K') || csi.EndsWith('L') || csi.EndsWith('M') ||
                csi.EndsWith('P') || csi.EndsWith('r') || csi.EndsWith('h') || csi.EndsWith('l') ||
                csi.EndsWith('s') || csi.EndsWith('u') || csi.EndsWith('G') || csi.EndsWith('d') ||
                csi.EndsWith('n') || csi.EndsWith('X') || csi.EndsWith('@') || csi.EndsWith('t'))
            {
                ProcessCsi(csi);
                _inEscape = false;
            }
        }
    }

    private void ProcessCsi(string seq)
    {
        var cmd = seq[^1];
        var args = seq[..^1].Length > 0
            ? seq[..^1].Split(';').Select(s => int.TryParse(s, out var n) ? n : 1).ToArray()
            : Array.Empty<int>();

        var n = args.Length > 0 ? args[0] : 1;
        var m = args.Length > 1 ? args[1] : 1;

        switch (cmd)
        {
            case 'A': CursorUp(n); break;
            case 'B': CursorDown(n); break;
            case 'C': CursorForward(n); break;
            case 'D': CursorBack(n); break;
            case 'E': CursorNextLine(n); break;
            case 'F': CursorPrevLine(n); break;
            case 'G': CursorHorizontalAbsolute(n); break;
            case 'H':
            case 'f': CursorPosition(m, n); break;
            case 'J': EraseDisplay(n); break;
            case 'K': EraseLine(n); break;
            case 'L': InsertLines(n); break;
            case 'M': DeleteLines(n); break;
            case 'P': DeleteChars(n); break;
            case '@': InsertChars(n); break;
            case 'm': SetGraphicsRendition(args.Length > 0 ? args : new[] { 0 }); break;
            case 'r': SetScrollRegion(m, n); break;
            case 's': SaveCursor(); break;
            case 'u': RestoreCursor(); break;
            case 'h':
                if (args.Length > 0)
                {
                    var mode = args[args.Length - 1];
                    if (seq.Contains('?'))
                    {
                        switch (mode)
                        {
                            case 7: _autoWrap = true; break;   // ?7h — 开启自动换行
                            case 25: _cursorVisible = true; break;
                            case 1049: EnterAltScreen(); break;
                        }
                    }
                }
                break;
            case 'l':
                if (args.Length > 0)
                {
                    var mode = args[args.Length - 1];
                    if (seq.Contains('?'))
                    {
                        switch (mode)
                        {
                            case 7: _autoWrap = false; break;  // ?7l — 关闭自动换行
                            case 25: _cursorVisible = false; break;
                            case 1049: ExitAltScreen(); break;
                        }
                    }
                }
                break;
            case 'n':
                // DSR — Device Status Report
                if (args.Length > 0 && args[0] == 6)
                {
                    // CSI 6n → respond with CSI row;col R
                    var resp = Encoding.ASCII.GetBytes(
                        $"\x1b[{CursorRow + 1};{CursorCol + 1}R");
                    OnSendResponse?.Invoke(resp);
                }
                break;
        }
    }

    private void SetScrollRegion(int top, int bottom)
    {
        _scrollTop = Math.Max(0, Math.Min(top - 1, Rows - 1));
        _scrollBottom = Math.Max(0, Math.Min(bottom - 1, Rows - 1));
        CursorPosition(1, 1);
    }

    private void SaveCursor()
    {
        _savedCursorRow = CursorRow;
        _savedCursorCol = CursorCol;
    }

    private void RestoreCursor()
    {
        CursorRow = Math.Clamp(_savedCursorRow, 0, Rows - 1);
        CursorCol = Math.Clamp(_savedCursorCol, 0, Cols - 1);
    }

    private void CursorUp(int n)
    {
        CursorRow = Math.Max(_scrollTop, CursorRow - n);
    }

    private void CursorDown(int n)
    {
        CursorRow = Math.Min(_scrollBottom, CursorRow + n);
    }

    private void CursorForward(int n)
    {
        CursorCol = Math.Min(Cols - 1, CursorCol + n);
    }

    private void CursorBack(int n = 1)
    {
        CursorCol = Math.Max(0, CursorCol - n);
    }

    private void CursorForwardTab()
    {
        var nextTab = ((CursorCol / 8) + 1) * 8;
        CursorCol = Math.Min(Cols - 1, nextTab);
    }

    // TODO: 实现 VT100 制表符停止位
    private void SetTabStop() { }

    private void CursorNextLine(int n)
    {
        CursorCol = 0;
        CursorDown(n);
    }

    private void CursorPrevLine(int n)
    {
        CursorCol = 0;
        CursorUp(n);
    }

    private void CursorHorizontalAbsolute(int n)
    {
        CursorCol = Math.Clamp(n - 1, 0, Cols - 1);
    }

    private void NextLine()
    {
        CursorCol = 0;
        LineFeed();
    }

    private void CarriageReturn()
    {
        CursorCol = 0;
    }

    private void LineFeed()
    {
        if (CursorRow >= _scrollBottom)
        {
            ScrollUp(1);
        }
        else
        {
            CursorRow++;
        }
    }

    private void ReverseIndex()
    {
        if (CursorRow <= _scrollTop)
        {
            ScrollDown(1);
        }
        else
        {
            CursorRow--;
        }
    }

    private void ScrollUp(int count)
    {
        for (var c = 0; c < count; c++)
        {
            for (var row = _scrollTop; row < _scrollBottom; row++)
            {
                for (var col = 0; col < Cols; col++)
                {
                    _screen[row, col] = _screen[row + 1, col];
                }
            }
            for (var col = 0; col < Cols; col++)
            {
                _screen[_scrollBottom, col] = new TerminalCell();
            }
        }
    }

    private void ScrollDown(int count)
    {
        for (var c = 0; c < count; c++)
        {
            for (var row = _scrollBottom; row > _scrollTop; row--)
            {
                for (var col = 0; col < Cols; col++)
                {
                    _screen[row, col] = _screen[row - 1, col];
                }
            }
            for (var col = 0; col < Cols; col++)
            {
                _screen[_scrollTop, col] = new TerminalCell();
            }
        }
    }

    private void EraseDisplay(int mode)
    {
        switch (mode)
        {
            case 0: // Cursor to end
                EraseFromCursorToEnd();
                break;
            case 1: // Start to cursor
                EraseFromStartToCursor();
                break;
            case 2: // Entire display
            case 3:
                ClearScreen();
                break;
        }
    }

    private void EraseLine(int mode)
    {
        switch (mode)
        {
            case 0: // Cursor to end of line
                for (var col = CursorCol; col < Cols; col++)
                    _screen[CursorRow, col] = new TerminalCell { Foreground = _currentAttrs.Foreground, Background = _currentAttrs.Background };
                break;
            case 1: // Start of line to cursor
                for (var col = 0; col <= CursorCol; col++)
                    _screen[CursorRow, col] = new TerminalCell { Foreground = _currentAttrs.Foreground, Background = _currentAttrs.Background };
                break;
            case 2: // Entire line
                for (var col = 0; col < Cols; col++)
                    _screen[CursorRow, col] = new TerminalCell { Foreground = _currentAttrs.Foreground, Background = _currentAttrs.Background };
                break;
        }
    }

    private void InsertLines(int n)
    {
        for (var i = 0; i < n; i++)
        {
            for (var row = _scrollBottom; row > CursorRow; row--)
                for (var col = 0; col < Cols; col++)
                    _screen[row, col] = _screen[row - 1, col];
            for (var col = 0; col < Cols; col++)
                _screen[CursorRow, col] = new TerminalCell();
        }
    }

    private void DeleteLines(int n)
    {
        for (var i = 0; i < n; i++)
        {
            for (var row = CursorRow; row < _scrollBottom; row++)
                for (var col = 0; col < Cols; col++)
                    _screen[row, col] = _screen[row + 1, col];
            for (var col = 0; col < Cols; col++)
                _screen[_scrollBottom, col] = new TerminalCell();
        }
    }

    private void DeleteChars(int n)
    {
        for (var col = CursorCol; col + n < Cols; col++)
            _screen[CursorRow, col] = _screen[CursorRow, col + n];
        for (var col = Cols - n; col < Cols; col++)
            _screen[CursorRow, col] = new TerminalCell();
    }

    private void InsertChars(int n)
    {
        for (var col = Cols - 1; col >= CursorCol + n; col--)
            _screen[CursorRow, col] = _screen[CursorRow, col - n];
        for (var col = CursorCol; col < CursorCol + n; col++)
            _screen[CursorRow, col] = new TerminalCell();
    }

    private void SetGraphicsRendition(int[] args)
    {
        if (args.Length == 0)
        {
            _currentAttrs = new TerminalCell();
            return;
        }

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case 0: _currentAttrs = new TerminalCell(); break;
                case 1: _currentAttrs.Bold = true; break;
                case 4: _currentAttrs.Underline = true; break;
                case 7: _currentAttrs.Inverse = true; break;
                case 22: _currentAttrs.Bold = false; break;
                case 24: _currentAttrs.Underline = false; break;
                case 27: _currentAttrs.Inverse = false; break;
                case >= 30 and <= 37:
                    _currentAttrs.Foreground = (ConsoleColor)(args[i] - 30);
                    break;
                case 38 when i + 2 < args.Length && args[i + 1] == 5:
                    _currentAttrs.Foreground = X256ToConsoleColor(args[i + 2]);
                    i += 2;
                    break;
                case 39: _currentAttrs.Foreground = ConsoleColor.Gray; break;
                case >= 40 and <= 47:
                    _currentAttrs.Background = (ConsoleColor)(args[i] - 40);
                    break;
                case 48 when i + 2 < args.Length && args[i + 1] == 5:
                    _currentAttrs.Background = X256ToConsoleColor(args[i + 2]);
                    i += 2;
                    break;
                case 49: _currentAttrs.Background = ConsoleColor.Black; break;
                case >= 90 and <= 97:
                    _currentAttrs.Foreground = (ConsoleColor)(args[i] - 90 + 8);
                    _currentAttrs.Bold = true;
                    break;
                case >= 100 and <= 107:
                    _currentAttrs.Background = (ConsoleColor)(args[i] - 100 + 8);
                    break;
            }
        }
    }

    private static ConsoleColor X256ToConsoleColor(int index)
    {
        if (index < 16)
        {
            return index switch
            {
                0 => ConsoleColor.Black, 1 => ConsoleColor.DarkRed, 2 => ConsoleColor.DarkGreen,
                3 => ConsoleColor.DarkYellow, 4 => ConsoleColor.DarkBlue, 5 => ConsoleColor.DarkMagenta,
                6 => ConsoleColor.DarkCyan, 7 => ConsoleColor.Gray, 8 => ConsoleColor.DarkGray,
                9 => ConsoleColor.Red, 10 => ConsoleColor.Green, 11 => ConsoleColor.Yellow,
                12 => ConsoleColor.Blue, 13 => ConsoleColor.Magenta, 14 => ConsoleColor.Cyan,
                15 => ConsoleColor.White, _ => ConsoleColor.Gray
            };
        }
        return ConsoleColor.Gray;
    }

    private void PutChar(char ch)
    {
        if (_autoWrap && CursorCol >= Cols)
        {
            CursorCol = 0;
            if (CursorRow >= _scrollBottom)
                ScrollUp(1);
            else
                CursorRow++;
        }

        if (CursorCol >= Cols) CursorCol = Cols - 1;

        var cell = new TerminalCell
        {
            Character = ch,
            Foreground = _currentAttrs.Foreground,
            Background = _currentAttrs.Background,
            Bold = _currentAttrs.Bold,
            Underline = _currentAttrs.Underline,
            Inverse = _currentAttrs.Inverse
        };

        _screen[CursorRow, CursorCol] = cell;
        CursorCol++;
    }

    private void CursorPosition(int row, int col)
    {
        if (_originMode)
        {
            CursorRow = Math.Clamp(_scrollTop + row - 1, _scrollTop, _scrollBottom);
        }
        else
        {
            CursorRow = Math.Clamp(row - 1, 0, Rows - 1);
        }
        CursorCol = Math.Clamp(col - 1, 0, Cols - 1);
    }

    private void EraseFromCursorToEnd()
    {
        EraseLine(0);
        for (var row = CursorRow + 1; row < Rows; row++)
        {
            for (var col = 0; col < Cols; col++)
            {
                _screen[row, col] = new TerminalCell();
            }
        }
    }

    private void EraseFromStartToCursor()
    {
        for (var row = 0; row < CursorRow; row++)
            for (var col = 0; col < Cols; col++)
                _screen[row, col] = new TerminalCell();
        for (var col = 0; col <= CursorCol; col++)
            _screen[CursorRow, col] = new TerminalCell();
    }

    public void ClearScreen()
    {
        for (var row = 0; row < Rows; row++)
            for (var col = 0; col < Cols; col++)
                _screen[row, col] = new TerminalCell();
        CursorRow = 0;
        CursorCol = 0;
    }

    private void FullReset()
    {
        _currentAttrs = new TerminalCell();
        _scrollTop = 0;
        _scrollBottom = Rows - 1;
        _autoWrap = true;
        _originMode = false;
        _cursorVisible = true;
        ClearScreen();
    }

    private void ProcessOsc(byte[] data)
    {
        // Format: ESC ] Ps ; Pt BEL  or  ESC ] Ps ; Pt ST
        // Ps = parameter (ASCII digits), Pt = text (typically UTF-8)
        // Find the semicolon separator
        int semi = -1;
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == (byte)';')
            {
                semi = i;
                break;
            }
        }

        if (semi < 0) return;

        // Extract Ps (parameter) - ASCII digits
        var ps = Encoding.ASCII.GetString(data, 0, semi);

        // Extract Pt (text) - decode as UTF-8
        var pt = Encoding.UTF8.GetString(data, semi + 1, data.Length - semi - 1);

        switch (ps)
        {
            case "0":
            case "1":
            case "2":
                OnTitleChanged?.Invoke(pt);
                break;
        }
    }

    private void EnterAltScreen()
    {
        if (_inAltScreen) return;
        _inAltScreen = true;

        // 保存主屏幕和光标位置
        _altScreen = new TerminalCell[Rows, Cols];
        for (var r = 0; r < Rows; r++)
            for (var c = 0; c < Cols; c++)
                _altScreen[r, c] = _screen[r, c].Clone();
        _altCursorRow = CursorRow;
        _altCursorCol = CursorCol;

        // 清屏并重置光标
        ClearScreen();
    }

    private void ExitAltScreen()
    {
        if (!_inAltScreen || _altScreen == null) return;
        _inAltScreen = false;

        // 恢复主屏幕和光标
        _screen = _altScreen;
        CursorRow = _altCursorRow;
        CursorCol = _altCursorCol;
        _altScreen = null;
    }

    public string[] GetVisibleContent()
    {
        var lines = new List<string>();
        for (var row = 0; row < Rows; row++)
        {
            var sb = new StringBuilder();
            for (var col = 0; col < Cols; col++)
            {
                sb.Append(_screen[row, col].Character);
            }
            lines.Add(sb.ToString().TrimEnd());
        }
        // 截掉尾部空行，避免 UI 积累大量空白
        var lastNonEmpty = lines.Count - 1;
        while (lastNonEmpty >= 0 && string.IsNullOrEmpty(lines[lastNonEmpty]))
            lastNonEmpty--;
        if (lastNonEmpty < 0)
            return Array.Empty<string>();
        return lines.GetRange(0, lastNonEmpty + 1).ToArray();
    }

    public TerminalCell[,] GetScreenBuffer() => _screen;
}
