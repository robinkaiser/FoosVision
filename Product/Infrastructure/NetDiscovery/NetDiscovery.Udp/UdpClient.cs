using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetDiscovery.Udp;

/// <summary>
/// UDP client class
/// </summary>
internal sealed class UdpClient : IClient
{
    /// <summary>
    /// Lock object
    /// </summary>
    private readonly object _Lock = new object();

    /// <summary>
    /// UDP port
    /// </summary>
    private readonly int _Port;

    /// <summary>
    /// Cancellation token source
    /// </summary>
    private CancellationTokenSource _Cancel;

    /// <summary>
    /// Worker thread
    /// </summary>
    private Thread _Thread;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpClient"/> class.
    /// </summary>
    /// <param name="port">UDP port</param>
    public UdpClient(int port)
    {
        _Port = port;
    }

    /// <summary>
    /// Discovery event
    /// </summary>
    public event EventHandler<DiscoveryEventArgs> Discovery;

    /// <summary>
    /// Dispose of this UDP client
    /// </summary>
    public void Dispose()
    {
        Stop();
    }

    /// <summary>
    /// Start the client
    /// </summary>
    public void Start()
    {
        lock (_Lock)
        {
            // Skip if started
            if (_Thread != null)
                return;

            // Start discovery
            _Cancel = new CancellationTokenSource();
            _Thread = new Thread(DiscoveryClient);
            _Thread.Start();
        }
    }

    /// <summary>
    /// Stop the client
    /// </summary>
    public void Stop()
    {
        lock (_Lock)
        {
            // Skip if stopped
            if (_Thread == null)
                return;

            // Stop discovery
            _Cancel.Cancel();
            _Thread.Join();
            _Cancel = null;
            _Thread = null;
        }
    }

    /// <summary>
    /// Discovery client thread procedure
    /// </summary>
    private void DiscoveryClient()
    {
        var sockets = new List<Socket>();

        // Detect if IPv4 is supported
        if (Socket.OSSupportsIPv4)
        {
            // Create the IPv4 socket
            var socketV4 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                EnableBroadcast = true,
                ExclusiveAddressUse = false,
            };

            // Allow address reuse
            socketV4.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Bind to the port
            socketV4.Bind(new IPEndPoint(IPAddress.Any, _Port));

            // Add to the list of sockets
            sockets.Add(socketV4);
        }

        // Detect if IPv6 is supported
        if (Socket.OSSupportsIPv6)
        {
            // Create the IPv6 socket
            var socketV6 = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)
            {
                EnableBroadcast = true,
                ExclusiveAddressUse = false,
            };

            // Allow both sockets to reuse addresses
            socketV6.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Join the IPv6 socket to the local-link group
            socketV6.SetSocketOption(
                SocketOptionLevel.IPv6,
                SocketOptionName.AddMembership,
                new IPv6MulticastOption(IPAddress.Parse("ff02::1")));

            // Bind to the port
            socketV6.Bind(new IPEndPoint(IPAddress.IPv6Any, _Port));

            // Add to the list of sockets
            sockets.Add(socketV6);
        }

        // If no listener sockets then IP networking is unsupported
        if (sockets.Count == 0)
            return;

        // Read buffer
        var buffer = new byte[1024];

        // Loop until cancelled
        while (!_Cancel.IsCancellationRequested)
        {
            // Wait for incoming data
            var checkRead = sockets.ToList();
            var checkError = new List<Socket>();
            Socket.Select(checkRead, null, checkError, 1000000);

            // Process all sockets with incoming data
            foreach (var socket in checkRead)
            {
                // Read the next packet
                var ep = socket.LocalEndPoint;
                var len = socket.ReceiveFrom(buffer, ref ep);

                // Get the address and identity
                var address = ((IPEndPoint)ep).Address;
                var identity = Encoding.ASCII.GetString(buffer, 0, len);

                // Report the discovery
                Discovery?.Invoke(this, new DiscoveryEventArgs(address, identity));
            }
        }

        // Dispose of the sockets
        foreach (var socket in sockets)
            socket.Dispose();
    }
}
