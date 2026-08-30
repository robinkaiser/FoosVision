// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Recorder.UnitTests.Diagnostics;

public class VideoDumpOrchestratorTests
{
    [Fact]
    public void TryScheduleDump_returns_false_when_snapshot_is_not_available()
    {
        FakeSnapshotSource snapshotSource = new();
        FakeVideoDumpWriter writer = new();
        FakeBackgroundQueue queue = new();
        VideoDumpOrchestrator testee = new(snapshotSource, writer, queue);

        bool scheduled = testee.TryScheduleDump(VideoDumpSessionKind.Game);

        Assert.False(scheduled);
        Assert.Empty(queue.Work);
    }

    [Fact]
    public async Task TryScheduleDump_enqueues_writer_with_expected_request()
    {
        FakeSnapshotSource snapshotSource = new()
        {
            Snapshot = CreateSegment(10_000_000_000L, 70_000_000_000L),
        };
        FakeVideoDumpWriter writer = new();
        FakeBackgroundQueue queue = new();
        VideoDumpOrchestrator testee = new(
            snapshotSource,
            writer,
            queue,
            () => new DateTimeOffset(2026, 6, 10, 15, 30, 12, TimeSpan.Zero));

        bool scheduled = testee.TryScheduleDump(VideoDumpSessionKind.Game);
        await queue.RunNext(CancellationToken.None);

        Assert.True(scheduled);
        Assert.Single(writer.Requests);
        VideoDumpRequest request = writer.Requests[0];
        Assert.Equal(VideoDumpSessionKind.Game, request.SessionKind);
        Assert.Equal("recorder-game-20260610-153012-buffer-60s.mp4", request.FileName);
        Assert.Same(snapshotSource.Snapshot, request.Segment);
    }

    [Fact]
    public void TryScheduleDump_returns_false_when_snapshot_throws()
    {
        FakeSnapshotSource snapshotSource = new()
        {
            ThrowOnSnapshot = true,
        };
        FakeVideoDumpWriter writer = new();
        FakeBackgroundQueue queue = new();
        VideoDumpOrchestrator testee = new(snapshotSource, writer, queue);

        bool scheduled = testee.TryScheduleDump(VideoDumpSessionKind.Installation);

        Assert.False(scheduled);
        Assert.Empty(queue.Work);
    }

    [Fact]
    public async Task Queued_writer_failure_does_not_escape_background_work()
    {
        FakeSnapshotSource snapshotSource = new()
        {
            Snapshot = CreateSegment(0, 1),
        };
        FakeVideoDumpWriter writer = new()
        {
            ThrowOnWrite = true,
        };
        FakeBackgroundQueue queue = new();
        VideoDumpOrchestrator testee = new(snapshotSource, writer, queue);

        testee.TryScheduleDump(VideoDumpSessionKind.Installation);
        await queue.RunNext(CancellationToken.None);

        Assert.Single(writer.Requests);
    }

    private static EncodedReplaySegment CreateSegment(long startTimeNs, long endTimeNs)
    {
        return new EncodedReplaySegment(
            EncodedReplayCodec.H264,
            startTimeNs,
            endTimeNs,
            [],
            [new EncodedReplayAccessUnit(startTimeNs, true, true, [1, 2, 3])]);
    }

    private class FakeSnapshotSource : IEncodedReplaySnapshotSource
    {
        public EncodedReplaySegment? Snapshot { get; set; }

        public bool ThrowOnSnapshot { get; set; }

        public bool TryGetSnapshot(out EncodedReplaySegment segment)
        {
            if (ThrowOnSnapshot)
            {
                throw new InvalidOperationException("Snapshot failed.");
            }

            if (Snapshot is null)
            {
                segment = CreateSegment(0, 0);
                return false;
            }

            segment = Snapshot;
            return true;
        }
    }

    private class FakeVideoDumpWriter : IVideoDumpWriter
    {
        public List<VideoDumpRequest> Requests { get; } = [];

        public bool ThrowOnWrite { get; set; }

        public Task WriteAsync(VideoDumpRequest request, CancellationToken ct)
        {
            Requests.Add(request);

            if (ThrowOnWrite)
            {
                throw new InvalidOperationException("Write failed.");
            }

            return Task.CompletedTask;
        }
    }

    private class FakeBackgroundQueue : IVideoDumpBackgroundQueue
    {
        public List<Func<CancellationToken, Task>> Work { get; } = [];

        public void Enqueue(Func<CancellationToken, Task> work)
        {
            Work.Add(work);
        }

        public Task RunNext(CancellationToken ct)
        {
            Assert.NotEmpty(Work);
            Func<CancellationToken, Task> work = Work[0];
            Work.RemoveAt(0);
            return work(ct);
        }
    }
}
