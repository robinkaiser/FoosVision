// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public enum SpeechOutputResult
{
    Ready,
    Completed,
    Cancelled,
    Unavailable,
    LanguageUnavailable,
    AudioFocusDenied,
    Failed,
}

/// <summary>Local speech output. Implementations log technical failures at their source.</summary>
public interface ISpeechOutput
{
    /// <summary>Output was interrupted externally; discard pending speech rather than resuming it.</summary>
    event Action? Interrupted;

    /// <summary>Whether new output is currently allowed, independently of engine preparation.</summary>
    bool IsOutputAllowed { get; }

    Task<SpeechOutputResult> PrepareAsync(CancellationToken ct);

    /// <summary>Completes when speech ends. Cancellation must also stop audible output.</summary>
    Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken ct);
}
