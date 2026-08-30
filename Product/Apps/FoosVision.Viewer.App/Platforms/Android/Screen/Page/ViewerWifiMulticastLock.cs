// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Net.Wifi;
using FoosVision.Common.Logging;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Page;

internal class ViewerWifiMulticastLock : IDisposable
{
    private static readonly Source _Log = new("Viewer.Android.WifiMulticastLock");

    private readonly WifiManager.MulticastLock? _MulticastLock;
    private int _Disposed;

    private ViewerWifiMulticastLock(WifiManager.MulticastLock? multicastLock)
    {
        _MulticastLock = multicastLock;
    }

    public static ViewerWifiMulticastLock Acquire(Context context)
    {
        try
        {
            var wifiManager = context.ApplicationContext?.GetSystemService(Context.WifiService) as WifiManager;
            if (wifiManager is null)
            {
                _Log.Warning("Could not acquire WiFi multicast lock because WiFi service is unavailable.");
                return new ViewerWifiMulticastLock(null);
            }

            WifiManager.MulticastLock? multicastLock = wifiManager.CreateMulticastLock("FoosVision.Viewer.Discovery");
            if (multicastLock is null)
            {
                _Log.Warning("Could not acquire WiFi multicast lock because lock creation returned null.");
                return new ViewerWifiMulticastLock(null);
            }

            multicastLock.SetReferenceCounted(false);
            multicastLock.Acquire();

            _Log.Information("Acquired WiFi multicast lock for viewer discovery.");
            return new ViewerWifiMulticastLock(multicastLock);
        }
        catch (Exception ex)
        {
            _Log.Warning("Could not acquire WiFi multicast lock for viewer discovery: {0}", ex);
            return new ViewerWifiMulticastLock(null);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _Disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_MulticastLock?.IsHeld == true)
            {
                _MulticastLock.Release();
                _Log.Information("Released WiFi multicast lock for viewer discovery.");
            }

            _MulticastLock?.Dispose();
        }
        catch (Exception ex)
        {
            _Log.Warning("Could not release WiFi multicast lock for viewer discovery: {0}", ex);
        }
    }
}
