// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Vision;

public readonly record struct EncodedVisionContext(byte[] Buffer, int Length);

public interface IEncodedVisionContextProvider
{
    bool TryGetEncodedVisionContext(out EncodedVisionContext context);
}

public interface IEncodedVisionContextConsumer
{
    bool TryApplyEncodedVisionContext(EncodedVisionContext context);
}
