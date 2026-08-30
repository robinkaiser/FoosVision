// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Recorder.Game;

public class VisionContextUpdatePublisher : IDisposable
{
    private static readonly Source _Log = new("VisionContextUpdatePublisher");
    private static readonly TimeSpan _DefaultInterval = TimeSpan.FromSeconds(5);

    private readonly IEncodedVisionContextProvider _VisionContextProvider;
    private readonly IRecorderLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly Func<bool> _IsEnabled;
    private readonly TimeSpan _Interval;
    private readonly CancellationTokenSource _Cts = new();
    private readonly Task _Worker;

    public VisionContextUpdatePublisher(
        IEncodedVisionContextProvider visionContextProvider,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher,
        Func<bool> isEnabled,
        TimeSpan? interval = null)
    {
        _VisionContextProvider = visionContextProvider;
        _LiveAnalysisPublisher = liveAnalysisPublisher;
        _IsEnabled = isEnabled;
        _Interval = interval ?? _DefaultInterval;
        _Worker = Task.Run(() => PublishLoop(_Cts.Token));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _Cts.Cancel();

        try
        {
            _Worker.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _Cts.Dispose();
    }

    private async Task PublishLoop(CancellationToken ct)
    {
        using PeriodicTimer timer = new(_Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await TryPublish(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task TryPublish(CancellationToken ct)
    {
        if (!_IsEnabled())
        {
            return;
        }

        try
        {
            if (!_VisionContextProvider.TryGetEncodedVisionContext(out EncodedVisionContext context))
            {
                return;
            }

            VisionContextMessage message = new()
            {
                Buffer = context.Buffer,
                Length = context.Length,
            };

            await _LiveAnalysisPublisher.PublishVisionContext(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _Log.Warning("Vision context update failed: {0}", ex);
        }
    }
}
