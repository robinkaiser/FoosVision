// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Connectivity;

public class RecorderRuntimeStateControllerTests
{
    [Fact]
    public async Task Training_transitions_are_monotonic_deduplicated_and_preserve_active_session_on_rejection()
    {
        IRecorderEventPublisher publisher = Substitute.For<IRecorderEventPublisher>();
        RecorderRuntimeStateController runtime = new(publisher);
        Guid session = Guid.NewGuid();
        await runtime.PublishIfChanged(RecorderRuntimeMode.TrainingRunning, session, RecorderStateChangeReason.CommandCompleted, "", TestContext.Current.CancellationToken);
        await runtime.PublishIfChanged(RecorderRuntimeMode.TrainingRunning, session, RecorderStateChangeReason.CommandCompleted, "", TestContext.Current.CancellationToken);
        await runtime.PublishIfChanged(RecorderRuntimeMode.Faulted, null, RecorderStateChangeReason.InternalError, "rejected command", TestContext.Current.CancellationToken, onlyWhenInactive: true);
        Assert.Equal(1, runtime.CurrentState.Sequence);
        Assert.Equal(session, runtime.CurrentState.ActiveSessionId);
        await runtime.PublishIfChanged(RecorderRuntimeMode.Idle, null, RecorderStateChangeReason.SourceStopped, "", TestContext.Current.CancellationToken);
        Assert.Equal(2, runtime.CurrentState.Sequence);
        Assert.Null(runtime.CurrentState.ActiveSessionId);
        await publisher.Received(2).PublishAsync(Arg.Any<RecorderRuntimeStateChanged>(), TestContext.Current.CancellationToken);
    }
}
