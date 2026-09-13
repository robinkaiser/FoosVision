// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Viewer.App.Runtime;

public static class ViewerForegroundState
{
    private static int _IsForeground;

    public static event Action<bool>? Changed;

    public static bool IsForeground => Volatile.Read(ref _IsForeground) != 0;

    public static void SetForeground(bool isForeground)
    {
        var next = isForeground ? 1 : 0;

        if (Interlocked.Exchange(ref _IsForeground, next) == next)
        {
            return;
        }

        Changed?.Invoke(isForeground);
    }
}
