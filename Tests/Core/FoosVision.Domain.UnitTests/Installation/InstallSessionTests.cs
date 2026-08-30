// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Installation.Entities;

namespace FoosVision.Domain.UnitTests.Installation;

public class InstallSessionTests
{
    private const long _1s = 1000 * 1_000_000L;

    private readonly InstallSession _Testee = new(Guid.Empty);

    [Fact]
    public void Requests_table_config_update_when_due()
    {
        Frame frame = CreateFrame(1, 2);

        IReadOnlyList<Change> changes = _Testee.ApplyFrame(frame);

        Assert.Single(changes);
        Assert.IsType<UpdateTableConfigRequest>(changes.Single());
    }

    [Fact]
    public void Does_not_request_table_config_update_while_update_is_in_progress()
    {
        Frame frame1 = CreateFrame(1, 2);
        Frame frame2 = CreateFrame(2, 4);

        _ = _Testee.ApplyFrame(frame1);
        IReadOnlyList<Change> changes = _Testee.ApplyFrame(frame2);

        Assert.Empty(changes);
    }

    [Fact]
    public void Requests_next_table_config_update_after_previous_update_completed_and_interval_elapsed()
    {
        Frame frame1 = CreateFrame(1, 2);
        Frame frame2 = CreateFrame(2, 3);
        Frame frame3 = CreateFrame(3, 4);

        _ = _Testee.ApplyFrame(frame1);
        _Testee.CompleteTableUpdate();
        IReadOnlyList<Change> changesBeforeIntervalElapsed = _Testee.ApplyFrame(frame2);
        IReadOnlyList<Change> changesAfterIntervalElapsed = _Testee.ApplyFrame(frame3);

        Assert.Empty(changesBeforeIntervalElapsed);
        Assert.Single(changesAfterIntervalElapsed);
        Assert.IsType<UpdateTableConfigRequest>(changesAfterIntervalElapsed.Single());
    }

    private static Frame CreateFrame(ulong id, long seconds)
    {
        return new Frame(id, seconds * _1s);
    }
}
