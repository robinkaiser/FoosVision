// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Ports.Vision;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingVisionContextConsumer : IEncodedVisionContextConsumer
{
    public List<EncodedVisionContext> Contexts { get; } = [];

    public bool TryApplyEncodedVisionContext(EncodedVisionContext context)
    {
        Contexts.Add(context);
        return true;
    }
}
