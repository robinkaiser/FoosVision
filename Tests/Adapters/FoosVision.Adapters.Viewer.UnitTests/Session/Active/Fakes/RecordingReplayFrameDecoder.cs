// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingReplayFrameDecoder : IEncodedReplayFrameDecoder
{
    private readonly TaskCompletionSource _FirstDecodeBlocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _DecodeCallCount;

    public int DecodeCallCount => Volatile.Read(ref _DecodeCallCount);

    public EncodedReplayDecodeRequest? LastRequest { get; private set; }

    public bool BlockFirstDecode { get; set; }

    public bool WasFirstDecodeCanceled { get; private set; }

    public Task WaitUntilFirstDecodeBlocked()
    {
        return _FirstDecodeBlocked.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    public async IAsyncEnumerable<DecodedReplayFrame> Decode(
        EncodedReplayDecodeRequest replay,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        int decodeCall = Interlocked.Increment(ref _DecodeCallCount);
        LastRequest = replay;

        if (BlockFirstDecode &&
            decodeCall == 1)
        {
            _FirstDecodeBlocked.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                WasFirstDecodeCanceled = true;
                throw;
            }
        }

        foreach (EncodedReplayAccessUnit accessUnit in replay.AccessUnits)
        {
            ct.ThrowIfCancellationRequested();
            YuvPlaneLayout y = new(YuvPlaneKind.Y, 1, 1, 1, 1);
            YuvPlaneLayout u = new(YuvPlaneKind.U, 1, 1, 1, 1);
            YuvPlaneLayout v = new(YuvPlaneKind.V, 1, 1, 1, 1);
            yield return new DecodedReplayFrame(new TestYuvFrameHandle(
                new Frame(0, accessUnit.TimeNs),
                new YuvFrameLayout(1, 1, y, u, v)));
            await Task.Yield();
        }
    }
}
