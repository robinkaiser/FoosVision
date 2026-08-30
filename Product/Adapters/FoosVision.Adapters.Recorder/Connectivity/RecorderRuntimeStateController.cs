// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;

namespace FoosVision.Adapters.Recorder.Connectivity;

public class RecorderRuntimeStateController
{
    private readonly Lock _Gate = new();
    private readonly IRecorderEventPublisher _EventPublisher;
    private RecorderRuntimeStateChanged _CurrentState = new()
    {
        Sequence = 0,
        Mode = RecorderRuntimeMode.Idle,
        ActiveSessionId = null,
        Reason = RecorderStateChangeReason.None,
        Detail = string.Empty,
    };

    public RecorderRuntimeStateController(IRecorderEventPublisher eventPublisher)
    {
        _EventPublisher = eventPublisher;
    }

    public event Action<RecorderRuntimeStateChanged>? StateChanged;

    public RecorderRuntimeStateChanged CurrentState
    {
        get
        {
            lock (_Gate)
            {
                return _CurrentState;
            }
        }
    }

    public Task PublishIfChanged(
        RecorderRuntimeMode mode,
        Guid? activeSessionId,
        RecorderStateChangeReason reason,
        string detail,
        CancellationToken ct)
    {
        RecorderRuntimeStateChanged nextState;

        lock (_Gate)
        {
            // The recorder is the only source of truth for runtime state. Identical transitions are not republished.
            if (_CurrentState.Mode == mode &&
                _CurrentState.ActiveSessionId == activeSessionId &&
                _CurrentState.Reason == reason &&
                _CurrentState.Detail == detail)
            {
                return Task.CompletedTask;
            }

            nextState = new RecorderRuntimeStateChanged
            {
                Sequence = _CurrentState.Sequence + 1,
                Mode = mode,
                ActiveSessionId = activeSessionId,
                Reason = reason,
                Detail = detail,
            };

            _CurrentState = nextState;
        }

        StateChanged?.Invoke(nextState);

        return _EventPublisher.PublishAsync(nextState, ct);
    }
}
