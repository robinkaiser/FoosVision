// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace RecorderCli;

public sealed class LastCommandTracker
{
    private readonly object _Gate = new();
    private Guid _LastCommandId = Guid.Empty;

    public Guid GetOrNew()
    {
        lock (_Gate)
        {
            return _LastCommandId == Guid.Empty ? Guid.NewGuid() : _LastCommandId;
        }
    }

    public void Remember(Guid commandId)
    {
        lock (_Gate)
        {
            _LastCommandId = commandId;
        }
    }
}
