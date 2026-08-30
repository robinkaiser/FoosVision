// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public class VideoStreamSessionManager : IVideoStreamSessionManager
{
    private static readonly Source _Log = new("VideoStreamSessionManager");

    private readonly Lock _Gate = new();
    private readonly IEncodedVideoStreamSinkFactory _SinkFactory;
    private IEncodedVideoStreamSink? _CurrentSink;
    private string? _IpAddress;
    private int? _Port;
    private bool _SessionActive;

    public VideoStreamSessionManager(IEncodedVideoStreamSinkFactory sinkFactory)
    {
        _SinkFactory = sinkFactory;
    }

    public void Configure(string ipAddress, int port)
    {
        lock (_Gate)
        {
            if (_SessionActive)
            {
                _Log.Warning("Configure - Not allowed during active session!");
                return;
            }

            _IpAddress = ipAddress;
            _Port = port;
        }
    }

    public void StartSession()
    {
        IEncodedVideoStreamSink? sinkToDispose;

        lock (_Gate)
        {
            sinkToDispose = _CurrentSink;

            if (HasConfiguredTarget())
            {
                _SessionActive = true;
                _CurrentSink = CreateConfiguredSink();
            }
            else
            {
                _SessionActive = false;
                _CurrentSink = null;
            }
        }

        sinkToDispose?.Dispose();
    }

    public void StopSession()
    {
        IEncodedVideoStreamSink? sinkToDispose;

        lock (_Gate)
        {
            sinkToDispose = _CurrentSink;
            _SessionActive = false;
            _CurrentSink = null;
        }

        sinkToDispose?.Dispose();
    }

    public void Enqueue(byte[] buffer, int offset, int length, long timeNs, bool markAsEndOfAccessUnit)
    {
        IEncodedVideoStreamSink? sink;

        lock (_Gate)
        {
            sink = _CurrentSink;
        }

        sink?.Enqueue(buffer, offset, length, timeNs, markAsEndOfAccessUnit);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        StopSession();
    }

    private bool HasConfiguredTarget() =>
        !string.IsNullOrWhiteSpace(_IpAddress) &&
        _Port.HasValue;

    private IEncodedVideoStreamSink CreateConfiguredSink()
    {
        IEncodedVideoStreamSink sink = _SinkFactory.Create();
        sink.Configure(_IpAddress!, _Port!.Value);
        return sink;
    }
}
