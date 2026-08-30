// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

public class SourceInterval : Source
{
    private readonly TimeSpan _MinInterval;
    private readonly Func<DateTimeOffset> _UtcNow;

    private DateTimeOffset _Last;
    private int _Discarded;

    public SourceInterval(string name, TimeSpan minInterval, Func<DateTimeOffset>? utcNow = null)
        : base(name)
    {
        _MinInterval = minInterval;
        _UtcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _Last = DateTimeOffset.MinValue;
    }

    public override void Write(
        Severity severity,
        string messageTemplate,
        params object?[] args)
    {
        if (severity < LogControl.MinimumSeverity) return;

        var now = _UtcNow();

        if (now - _Last < _MinInterval)
        {
            _Discarded++;
            return;
        }

        _Last = now;

        if (_Discarded > 0)
        {
            var augmentedTemplate = messageTemplate + " (+{DiscardedCount} in {DiscardWindowMs}ms)";

            var augmentedArgs = new object?[args.Length + 2];
            Array.Copy(args, augmentedArgs, args.Length);

            augmentedArgs[^2] = _Discarded;
            augmentedArgs[^1] = (int)_MinInterval.TotalMilliseconds;

            _Discarded = 0;

            base.Write(severity, augmentedTemplate, augmentedArgs);
            return;
        }

        base.Write(severity, messageTemplate, args);
    }
}
