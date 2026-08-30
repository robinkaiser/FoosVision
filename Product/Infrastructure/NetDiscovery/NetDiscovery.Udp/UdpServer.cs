using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace NetDiscovery.Udp;

/// <summary>
/// UDP server class
/// </summary>
internal sealed class UdpServer : IServer
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
    /// Callback invoked after a discovery announcement was sent.
    /// </summary>
    private readonly Action<IPAddress, IPAddress, int, string> _AnnouncementSent;

    /// <summary>
    /// Cancellation token source
    /// </summary>
    private CancellationTokenSource _Cancel;

    /// <summary>
    /// Worker thread
    /// </summary>
    private Thread _Thread;

    /// <summary>
    /// Initializes a new instance of the <see cref="UdpServer"/> class.
    /// </summary>
    /// <param name="port">UDP port</param>
    public UdpServer(
        int port,
        Action<IPAddress, IPAddress, int, string> announcementSent)
    {
        _Port = port;
        _AnnouncementSent = announcementSent;
    }

    /// <summary>
    /// Gets or sets the server identity
    /// </summary>
    public string Identity { get; set; }

    /// <summary>
    /// Disposes of this UDP server
    /// </summary>
    public void Dispose()
    {
        Stop();
    }

    /// <summary>
    /// Start the server
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
            _Thread = new Thread(RunDiscoveryServer);
            _Thread.IsBackground = true;
            _Thread.Start();
        }
    }

    /// <summary>
    /// Stop the server
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
    /// Discovery server thread procedure
    /// </summary>
    private void RunDiscoveryServer()
    {
        try
        {
            DiscoveryServer();
        }
        catch
        {
        }
    }

    private void DiscoveryServer()
    {
        // Dictionary of sockets by address
        var sockets = new Dictionary<IPAddress, Socket>();

        // Loop until asked to cancel
        while (true)
        {
            // Wait for 3 seconds or a cancel request
            if (_Cancel.Token.WaitHandle.WaitOne(3000))
                break;

            // Get the addresses of all interfaces
            var addresses = GetLocalAddresses().ToList();
            var ipAddresses = addresses.Select(a => a.Address).ToList();

            // Find any addresses that have been added or removed
            var addedAddresses = ipAddresses.Where(a => sockets.Keys.All(k => !k.Equals(a))).ToList();
            var removedAddresses = sockets.Keys.Where(k => ipAddresses.All(a => !a.Equals(k))).ToList();

            // Discard sockets for removed addresses
            foreach (var address in removedAddresses)
            {
                sockets[address].Dispose();
                sockets.Remove(address);
            }

            // Add sockets for new IPv4 addresses
            foreach (var address in addedAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork))
            {
                Socket socket = null;
                try
                {
                    // Create the socket
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                    {
                        EnableBroadcast = true,
                        ExclusiveAddressUse = false,
                    };

                    // Allow address reuse
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    // Bind to the address
                    socket.Bind(new IPEndPoint(address, _Port));

                    // Save the socket
                    sockets[address] = socket;
                    socket = null;
                }
                catch
                {
                    socket?.Dispose();
                }
            }

            // Get the identity bytes
            var identityBytes = Encoding.ASCII.GetBytes(Identity);

            // Send over IPv4 sockets
            foreach (var localAddress in addresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
            {
                if (!sockets.TryGetValue(localAddress.Address, out var socket))
                    continue;

                foreach (var broadcastAddress in localAddress.BroadcastAddresses)
                {
                    var endpoint = new IPEndPoint(broadcastAddress, _Port);
                    try
                    {
                        socket.SendTo(identityBytes, endpoint);
                        ReportAnnouncementSent(((IPEndPoint)socket.LocalEndPoint).Address, endpoint.Address);
                    }
                    catch
                    {
                    }
                }
            }
        }

        // Dispose of the sockets
        foreach (var socket in sockets.Values)
            socket.Dispose();
    }

    private static IEnumerable<(IPAddress Address, IReadOnlyList<IPAddress> BroadcastAddresses)> GetLocalAddresses()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up &&
                nic.OperationalStatus != OperationalStatus.Unknown)
                continue;

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            IPInterfaceProperties properties;
            try
            {
                properties = nic.GetIPProperties();
            }
            catch
            {
                continue;
            }

            foreach (var unicastAddress in properties.UnicastAddresses)
            {
                var address = unicastAddress.Address;
                if (IPAddress.IsLoopback(address))
                    continue;

                if (address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var broadcastAddresses = GetIPv4BroadcastAddresses(address, GetIPv4Mask(unicastAddress));

                yield return (address, broadcastAddresses);
            }
        }
    }

    private static IReadOnlyList<IPAddress> GetIPv4BroadcastAddresses(IPAddress address, IPAddress mask)
    {
        var broadcastAddresses = new List<IPAddress>();

        AddDistinctBroadcastAddress(broadcastAddresses, IPAddress.Broadcast);

        if (mask != null)
        {
            AddDistinctBroadcastAddress(broadcastAddresses, CalculateIPv4BroadcastAddress(address, mask));
        }

        AddDistinctBroadcastAddress(
            broadcastAddresses,
            CalculateIPv4BroadcastAddress(address, IPAddress.Parse("255.255.255.0")));

        return broadcastAddresses;
    }

    private static void AddDistinctBroadcastAddress(List<IPAddress> broadcastAddresses, IPAddress broadcastAddress)
    {
        if (broadcastAddresses.Any(x => x.Equals(broadcastAddress)))
            return;

        broadcastAddresses.Add(broadcastAddress);
    }

    private static IPAddress GetIPv4Mask(UnicastIPAddressInformation address)
    {
        try
        {
            return address.IPv4Mask;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static IPAddress CalculateIPv4BroadcastAddress(IPAddress address, IPAddress mask)
    {
        var addressBytes = address.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcastBytes = new byte[addressBytes.Length];

        for (var i = 0; i < addressBytes.Length; i++)
        {
            broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcastBytes);
    }

    private void ReportAnnouncementSent(IPAddress localAddress, IPAddress targetAddress)
    {
        try
        {
            _AnnouncementSent(localAddress, targetAddress, _Port, Identity);
        }
        catch
        {
        }
    }

}
