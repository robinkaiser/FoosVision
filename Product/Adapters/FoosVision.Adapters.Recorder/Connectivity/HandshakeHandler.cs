// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Adapters.Recorder.Connectivity;

public class HandshakeHandler : IHandshakeHandler
{
    private static readonly Source _Log = new("Adapters.Recorder.Connectivity.HandshakeHandler");

    private readonly IRecorderVersionProvider _VersionProvider;
    private readonly IHandshakeDiagnosticsProvider _DiagnosticsProvider;
    private readonly IHandshakeViewerSettingsProvider _ViewerSettingsProvider;
    private readonly Func<HelloRequest, bool> _TryAcceptHello;

    public HandshakeHandler(
        IRecorderVersionProvider versionProvider,
        IHandshakeDiagnosticsProvider diagnosticsProvider,
        IHandshakeViewerSettingsProvider viewerSettingsProvider,
        Func<HelloRequest, bool> tryAcceptHello)
    {
        _VersionProvider = versionProvider;
        _DiagnosticsProvider = diagnosticsProvider;
        _ViewerSettingsProvider = viewerSettingsProvider;
        _TryAcceptHello = tryAcceptHello;
    }

    public HelloResponse Handle(HelloRequest request)
    {
        _Log.Information(
            "Recorder received handshake request. ViewerAddress={0} ProtocolVersion={1}",
            request.ViewerIpAddress,
            request.ProtocolVersion);

        if (!_TryAcceptHello(request))
        {
            var rejectedResponse = new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = _VersionProvider.GetAppVersion(),
                Accepted = false,
                RejectionReason = "RecorderBusy",
            };

            _Log.Warning(
                "Recorder rejected handshake request. ViewerAddress={0} RejectionReason={1}",
                request.ViewerIpAddress,
                rejectedResponse.RejectionReason);

            return rejectedResponse;
        }

        var response = new HelloResponse
        {
            ProtocolVersion = ProtocolVersions.Current,
            RecorderAppVersion = _VersionProvider.GetAppVersion(),
            Accepted = true,
            Diagnostics = _DiagnosticsProvider.GetDiagnosticsSettings(),
            Viewer = _ViewerSettingsProvider.GetViewerSettings(),
        };

        _Log.Information(
            "Recorder sending handshake response. ViewerAddress={0} ProtocolVersion={1} RecorderAppVersion={2} Accepted={3}",
            request.ViewerIpAddress,
            response.ProtocolVersion,
            response.RecorderAppVersion,
            response.Accepted);

        return response;
    }
}
