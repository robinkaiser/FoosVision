// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Adapters.Common.Live;
using FoosVision.Ports.Media;
using FoosVision.UseCases.Installation.Ports;
using FoosVision.UseCases.Installation.ProcessFrame;

namespace FoosVision.Adapters.Recorder.Installation.Live;

public class FrameProcessor : IFrameProcessor
{
    private readonly IProcessFrameInputPort _ProcessFrame;
    private readonly IProcessFrameOutputPort _OutputPort;
    private readonly IInstallSessionStore _SessionStore;

    public FrameProcessor(
        IProcessFrameInputPort processFrame,
        IProcessFrameOutputPort outputPort,
        IInstallSessionStore sessionStore)
    {
        _ProcessFrame = processFrame;
        _OutputPort = outputPort;
        _SessionStore = sessionStore;
    }

    public bool ShouldProcess => _SessionStore.HasActive;

    public async Task Process([NotNull] IFrameHandle frame, CancellationToken token)
    {
        var request = new ProcessFrameRequest(frame.Meta);

        await _ProcessFrame.Handle(request, _OutputPort, token);
    }
}
