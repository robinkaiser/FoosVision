// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using Android.Content;
using Android.Net;
using FoosVision.Common.Logging;

namespace FoosVision.Recorder.App.Platforms.Android;

internal sealed class AndroidWifiProcessNetworkBinding : IDisposable
{
    private static readonly Source _Log = new("Recorder.Android.WifiProcessNetworkBinding");

    private readonly ConnectivityManager _ConnectivityManager;
    private readonly Network _Network;
    private bool _Disposed;

    private AndroidWifiProcessNetworkBinding(ConnectivityManager connectivityManager, Network network)
    {
        _ConnectivityManager = connectivityManager;
        _Network = network;
    }

    public static IDisposable? Bind(Context context)
    {
        try
        {
            Context appContext = context.ApplicationContext ?? context;
            var connectivityManager = appContext.GetSystemService(Context.ConnectivityService) as ConnectivityManager;
            if (connectivityManager is null)
            {
                _Log.Warning("Could not bind recorder process to WiFi network because ConnectivityManager is unavailable.");
                return null;
            }

            Network? wifiNetwork = FindWifiNetwork(connectivityManager);
            if (wifiNetwork is null)
            {
                _Log.Warning("Could not bind recorder process to WiFi network because no WiFi network is available.");
                return null;
            }

            if (!connectivityManager.BindProcessToNetwork(wifiNetwork))
            {
                _Log.Warning("Could not bind recorder process to WiFi network. Network={0}", wifiNetwork);
                return null;
            }

            _Log.Information(
                "Bound recorder process to WiFi network. Network={0} LocalAddresses={1}",
                wifiNetwork,
                DescribeLocalIPv4Addresses(connectivityManager, wifiNetwork));

            return new AndroidWifiProcessNetworkBinding(connectivityManager, wifiNetwork);
        }
        catch (Exception ex)
        {
            _Log.Warning("Could not bind recorder process to WiFi network: {0}", ex);
            return null;
        }
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;

        try
        {
            _ConnectivityManager.BindProcessToNetwork(null);
            _Log.Information("Released recorder WiFi process network binding. Network={0}", _Network);
        }
        catch (Exception ex)
        {
            _Log.Warning("Could not release recorder WiFi process network binding: {0}", ex);
        }
    }

    private static Network? FindWifiNetwork(ConnectivityManager connectivityManager)
    {
#pragma warning disable CA1422
        foreach (Network network in connectivityManager.GetAllNetworks())
#pragma warning restore CA1422
        {
            NetworkCapabilities? capabilities = connectivityManager.GetNetworkCapabilities(network);
            if (capabilities is not null &&
                capabilities.HasTransport(TransportType.Wifi))
            {
                return network;
            }
        }

        return null;
    }

    private static string DescribeLocalIPv4Addresses(ConnectivityManager connectivityManager, Network network)
    {
        try
        {
            LinkProperties? linkProperties = connectivityManager.GetLinkProperties(network);
            if (linkProperties is null)
            {
                return "<none>";
            }

            List<string> addresses = [];
            foreach (LinkAddress linkAddress in linkProperties.LinkAddresses)
            {
                IPAddress? address = ToIPAddress(linkAddress.Address);
                if (address is not null)
                {
                    addresses.Add(address.ToString());
                }
            }

            return addresses.Count == 0 ? "<none>" : string.Join(",", addresses);
        }
        catch
        {
            return "<unavailable>";
        }
    }

    private static IPAddress? ToIPAddress(Java.Net.InetAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        byte[]? bytes = address.GetAddress();
        if (bytes is null ||
            bytes.Length != 4)
        {
            return null;
        }

        return new IPAddress(bytes);
    }
}
