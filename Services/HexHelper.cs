using System.Globalization;
using System.Text;

namespace CommMate.Services;

public static class HexHelper
{
    /// <summary>
    /// 将 HEX 字符串转换为字节数组。支持带空格、连字符、下划线、换行的格式。
    /// 例如 "41 54 0D 0A" / "41540D0A" / "0x41, 0x54" / "41-54-0D-0A" 都能解析。
    /// </summary>
    /// <exception cref="FormatException">字符串为空、长度为奇数或包含非 hex 字符</exception>
    public static byte[] HexStringToBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Array.Empty<byte>();

        // 去掉首尾空白和 0x/0X 前缀（整体），容忍中间空格/连字符/下划线/换行
        var cleaned = hex.Trim();
        if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[2..];

        var sb = new StringBuilder(cleaned.Length);
        foreach (var c in cleaned)
        {
            if (c is ' ' or '-' or '_' or '\t' or '\r' or '\n') continue;
            sb.Append(c);
        }

        if (sb.Length % 2 != 0)
            throw new FormatException("Hex 字符串长度为奇数，无法成对解码");

        var bytes = new byte[sb.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(sb.ToString(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
                throw new FormatException($"非法的 hex 字符: '{sb[i * 2]}{sb[i * 2 + 1]}'");
        }
        return bytes;
    }
}
