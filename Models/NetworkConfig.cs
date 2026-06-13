namespace CommMate.Models;

public enum NetworkMode
{
    TcpClient,
    TcpServer,
    Udp
}

public class NetworkConfig
{
    public NetworkMode Mode { get; set; } = NetworkMode.TcpClient;
    public string RemoteHost { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; } = 8080;
    public int LocalPort { get; set; } = 8080;
    public string LocalBindAddress { get; set; } = "0.0.0.0";
    public bool EnableBroadcast { get; set; }
    public string NewLine { get; set; } = "\r\n";
}
