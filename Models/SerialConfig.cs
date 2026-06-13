using System.IO.Ports;

namespace CommMate.Models;

public class SerialConfig
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 115200;
    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Parity Parity { get; set; } = Parity.None;
    public Handshake FlowControl { get; set; } = Handshake.None;
    public string NewLine { get; set; } = "\r\n";
    public int PacketTimeout { get; set; } = 50; // ms, inter-character timeout for framing
    public FramingMode FramingMode { get; set; } = FramingMode.Timeout;
}
