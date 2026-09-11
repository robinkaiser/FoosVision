// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Common.Logging;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Recorder.Diagnostics;

public class FrameProcessingRatePublisher : IDisposable
{
    private static readonly Source _Log = new("Recorder.Diagnostics.FrameProcessingRatePublisher");

    private readonly FrameProcessingLoop _FrameLoop;
    private readonly IRecorderLiveDataPublisher _LiveDataPublisher;

    public FrameProcessingRatePublisher(
        FrameProcessingLoop frameLoop,
        IRecorderLiveDataPublisher liveDataPublisher)
    {
        _FrameLoop = frameLoop;
        _LiveDataPublisher = liveDataPublisher;

        _FrameLoop.ProcessFramesPerSecondChanged += OnProcessFramesPerSecondChanged;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _FrameLoop.ProcessFramesPerSecondChanged -= OnProcessFramesPerSecondChanged;
    }

    private void OnProcessFramesPerSecondChanged(double? framesPerSecond)
    {
        try
        {
            ProcessFrameRateMessage message = new()
            {
                FramesPerSecond = framesPerSecond,
            };

            _ = _LiveDataPublisher.PublishProcessFrameRate(message);
        }
        catch (Exception ex)
        {
            _Log.Warning("Process frame rate publish failed. Ex={Exception}", ex.ToString());
        }
    }
}
