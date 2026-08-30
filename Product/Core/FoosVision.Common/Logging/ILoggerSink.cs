// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

public interface ILoggerSink : IDisposable
{
    void Emit(in LogEntry entry);
}

public class NullSink : ILoggerSink
{
    public void Emit(in LogEntry entry)
    {
    }

    public void Dispose()
    {
    }
}
