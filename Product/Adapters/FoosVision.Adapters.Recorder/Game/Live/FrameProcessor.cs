// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Adapters.Common.Live;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Game.Ports;
using FoosVision.UseCases.Game.ProcessFrame;

namespace FoosVision.Adapters.Recorder.Game.Live;

public class FrameProcessor : IFrameProcessor
{
    private static readonly Source _MetricsLog = new("Recorder.Game.Live.FrameProcessor");

    private readonly IProcessFrameInputPort _ProcessFrame;
    private readonly IProcessFrameOutputPort _OutputPort;
    private readonly IGameSessionStore _SessionStore;
    private readonly IBallFinder _BallFinder;
    private readonly DurationMetric? _DetectBallsDuration;

    public FrameProcessor(
        IProcessFrameInputPort processFrame,
        IProcessFrameOutputPort outputPort,
        IGameSessionStore sessionStore,
        IBallFinder ballFinder,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _ProcessFrame = processFrame;
        _OutputPort = outputPort;
        _SessionStore = sessionStore;
        _BallFinder = ballFinder;

        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        if (options.Enabled)
        {
            _DetectBallsDuration = new DurationMetric(
                options.CreateMetricName("Recorder.Vision.DetectBallsDuration"),
                _MetricsLog,
                options.GetReportInterval());
        }
    }

    public bool ShouldProcess => _SessionStore.HasActive;

    public async Task Process([NotNull] IFrameHandle frame, CancellationToken token)
    {
        var visionOps = new FrameVisionOps(_BallFinder, frame, _DetectBallsDuration);
        var request = new ProcessFrameRequest(frame.Meta, visionOps);

        await _ProcessFrame.Handle(request, _OutputPort, token);
    }
}
