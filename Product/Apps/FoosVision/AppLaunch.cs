// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision;

public static class AppLaunch
{
    public static event Action<AppRole>? RoleRequested;

    public static AppRole CurrentRole { get; private set; } = AppRole.Unknown;

    public static void SetRole(AppRole role)
    {
        ThrowIfUnsupported(role);
        CurrentRole = role;
    }

    public static void RequestRole(AppRole role)
    {
        SetRole(role);
        RoleRequested?.Invoke(role);
    }

    private static void ThrowIfUnsupported(AppRole role)
    {
        if (role is AppRole.Unknown)
        {
            throw new InvalidOperationException($"Unsupported app role '{role}'.");
        }
    }
}
