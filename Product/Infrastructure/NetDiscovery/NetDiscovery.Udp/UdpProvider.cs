namespace NetDiscovery.Udp;

/// <summary>
/// UDP discovery provider
/// </summary>
public sealed class UdpProvider : IProvider
{
    /// <summary>
    /// Discovery port
    /// </summary>
    private readonly int _Port;

    /// <summary>
    /// Callback invoked after a discovery announcement was sent.
    /// </summary>
    private readonly Action<System.Net.IPAddress, System.Net.IPAddress, int, string> _AnnouncementSent;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpProvider"/> class.
    /// </summary>
    /// <param name="port">Discovery port</param>
    public UdpProvider(int port)
        : this(port, (_, _, _, _) => { })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpProvider"/> class.
    /// </summary>
    /// <param name="port">Discovery port</param>
    /// <param name="announcementSent">Callback invoked after a discovery announcement was sent.</param>
    public UdpProvider(int port, Action<System.Net.IPAddress, System.Net.IPAddress, int, string> announcementSent)
    {
        _Port = port;
        _AnnouncementSent = announcementSent;
    }

    /// <summary>
    /// Dispose of this provider
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Create discovery client
    /// </summary>
    /// <returns>New discovery client</returns>
    public IClient CreateClient()
    {
        return new UdpClient(_Port);
    }

    /// <summary>
    /// Create discovery server
    /// </summary>
    /// <returns>New discovery server</returns>
    public IServer CreateServer()
    {
        return new UdpServer(_Port, _AnnouncementSent);
    }

    /// <summary>
    /// Start this provider
    /// </summary>
    public void Start()
    {
    }

    /// <summary>
    /// Stop this provider
    /// </summary>
    public void Stop()
    {
    }
}
