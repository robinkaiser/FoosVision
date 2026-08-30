// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class VisionContextTests
{
    [Fact]
    public void Vision_context_subscription_applies_context_to_vision()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        byte[] buffer = [1, 2, 3, 4];
        context.PublishVisionContext(new VisionContextMessage { Buffer = buffer, Length = buffer.Length });

        EncodedVisionContext applied = Assert.Single(context.VisionContextConsumer.Contexts);
        Assert.Same(buffer, applied.Buffer);
        Assert.Equal(buffer.Length, applied.Length);
    }
}
