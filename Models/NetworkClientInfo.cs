namespace CommMate.Models;

public class NetworkClientInfo
{
    public string Id { get; set; } = "";
    public string RemoteEndPoint { get; set; } = "";
    public DateTime ConnectedTime { get; set; } = DateTime.Now;
}
