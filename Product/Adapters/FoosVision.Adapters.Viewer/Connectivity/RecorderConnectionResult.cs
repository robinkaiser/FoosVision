// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Adapters.Viewer.Connectivity;

public enum RecorderConnectionFailure
{
    NoCandidateFound = 0,
    HandshakeTimeout = 1,
    ProtocolMismatch = 2,
    HandshakeFailed = 3,
    LocalNetworkError = 4,
    Cancelled = 5,
    RecorderBusy = 6,
}

public record RecorderConnection(
    string RecorderIpAddress,
    string RecorderAppVersion,
    int ProtocolVersion,
    HandshakeDiagnosticsSettings Diagnostics,
    HandshakeViewerSettings Viewer);

public record RecorderConnectionResult
{
    public bool Success { get; init; }

    public Option<RecorderConnection> Connection { get; init; } = Option<RecorderConnection>.None();

    public Option<RecorderConnectionFailure> Failure { get; init; } = Option<RecorderConnectionFailure>.None();

    public static RecorderConnectionResult Connected(RecorderConnection connection)
    {
        return new RecorderConnectionResult
        {
            Success = true,
            Connection = Option<RecorderConnection>.Some(connection),
        };
    }

    public static RecorderConnectionResult Failed(RecorderConnectionFailure failure)
    {
        return new RecorderConnectionResult
        {
            Success = false,
            Failure = Option<RecorderConnectionFailure>.Some(failure),
        };
    }
}
