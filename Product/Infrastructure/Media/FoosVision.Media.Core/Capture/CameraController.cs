// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Core.EncodedVideoStreaming;
using FoosVision.Ports.Media;
using FoosVision.UseCases.Dependencies.Video;

namespace FoosVision.Media.Core.Capture;

public class CameraController :
    IDisposable,
    IFrameFeed,
    IFrameSource,
    IEncodedReplayBuffer,
    IEncodedReplaySnapshotSource,
    ILiveVideoStreamController
{
    private const int _FrameWidth = 1920;
    private const int _FrameHeight = 1080;

    private const int _Pool_Size = 5;

    // No idea how to figure this out, so we choose a very conservative value here
    private const int _EncodedUnitBufferMaxChunkSize = 4_194_304;

    // Rough estimate based on Pixel 7, 1920 x 1080 x 120 fps for one minute:
    private const int _EncodedUnitBufferCapacity = (1 * 60 * 1_000_000) + _EncodedUnitBufferMaxChunkSize;

    private readonly ICameraFeed _CameraFeed;
    private readonly FramePool _FramePool;
    private readonly EncodedAccessUnitBuffer _EncodedUnitBuffer;
    private readonly IVideoStreamSessionManager _VideoStreamSessionManager;
    private bool _AwaitingInitialKeyFrame;

    public CameraController(ICameraFeed cameraFeed, IVideoStreamSessionManager videoStreamSessionManager)
    {
        _CameraFeed = cameraFeed;
        _VideoStreamSessionManager = videoStreamSessionManager;

        FrameLayout layout = new(FrameByteFormat.RGBA8888, _FrameWidth, _FrameHeight, _FrameWidth * 4);
        _FramePool = new(_Pool_Size, layout);
        _EncodedUnitBuffer = new(_EncodedUnitBufferCapacity, _EncodedUnitBufferMaxChunkSize);

        _FramePool.SetFrameReadyNotificationSink(OnFrameReady);
        _EncodedUnitBuffer.SetEncodedUnitReadyNotificationSink(OnEncodedUnitReady);
    }

    public void ConfigureUdpVideoStream(string ipAddress, int port)
    {
        _VideoStreamSessionManager.Configure(ipAddress, port);
    }

    // IFrameFeed

    public event Action<IFrameHandle>? FrameReady;

    public bool TryAcquireById(ulong id, [NotNullWhen(true)] out IFrameHandle? handle)
    {
        return _FramePool.TryAcquireById(id, out handle);
    }

    // IFrameSource

    public async Task<FrameSourceResult> Configure(CancellationToken ct)
    {
        _EncodedUnitBuffer.Reset();
        var isSuccess = await _CameraFeed.Configure();

        return isSuccess ?
            FrameSourceResult.Success :
            FrameSourceResult.Failure;
    }

    public async Task<FrameSourceResult> Start(CancellationToken ct)
    {
        _VideoStreamSessionManager.StartSession();
        _AwaitingInitialKeyFrame = true;
        var isSuccess = await _CameraFeed.Start(_FramePool, _EncodedUnitBuffer);

        if (!isSuccess)
        {
            _VideoStreamSessionManager.StopSession();
            _AwaitingInitialKeyFrame = false;
        }

        return isSuccess ?
            FrameSourceResult.Success :
            FrameSourceResult.Failure;
    }

    public async Task Stop(CancellationToken ct)
    {
        await _CameraFeed.Stop();
        _AwaitingInitialKeyFrame = false;
        _VideoStreamSessionManager.StopSession();
    }

    // IEncodedReplayBuffer

    public bool TryGetReplaySegment(long startTimeNs, long endTimeNs, out EncodedReplaySegment segment)
    {
        return _EncodedUnitBuffer.TryGetReplaySegment(startTimeNs, endTimeNs, out segment);
    }

    // IEncodedReplaySnapshotSource

    public bool TryGetSnapshot(out EncodedReplaySegment segment)
    {
        return _EncodedUnitBuffer.TryGetSnapshot(out segment);
    }

    // ILiveVideoStreamController

    public void PauseLiveVideoStream()
    {
        _VideoStreamSessionManager.StopSession();
    }

    public void ResumeLiveVideoStream()
    {
        _AwaitingInitialKeyFrame = true;
        _VideoStreamSessionManager.StartSession();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            _VideoStreamSessionManager.Dispose();
        }
        catch
        {
        }
    }

    private void OnFrameReady(FrameLease lease)
    {
        if (FrameReady == null)
        {
            lease.Release();
            return;
        }

        FrameReady?.Invoke(lease);
    }

    private void OnEncodedUnitReady(EncodedAccessUnit unit)
    {
        if (_AwaitingInitialKeyFrame)
        {
            if (!CanStartStreamFrom(unit)) return;

            _AwaitingInitialKeyFrame = false;
        }

        // Ensure the decoder receives required parameter sets before keyframes
        // when the current access unit does not already contain them.
        if (unit.IsKeyFrame &&
            !unit.ContainsAllRequiredParameterSets &&
            _EncodedUnitBuffer.HasHeader)
        {
            if (_EncodedUnitBuffer.Codec == CodecType.H265)
            {
                var vps = _EncodedUnitBuffer.Header.First(h => h.Type == ParameterSetType.VPS);
                _VideoStreamSessionManager.Enqueue(vps.Buffer, 0, vps.Buffer.Length, unit.TimeNs, false);
            }

            var sps = _EncodedUnitBuffer.Header.First(h => h.Type == ParameterSetType.SPS);
            _VideoStreamSessionManager.Enqueue(sps.Buffer, 0, sps.Buffer.Length, unit.TimeNs, false);

            var pps = _EncodedUnitBuffer.Header.First(h => h.Type == ParameterSetType.PPS);
            _VideoStreamSessionManager.Enqueue(pps.Buffer, 0, pps.Buffer.Length, unit.TimeNs, false);
        }

        // Send the actual unit directly from the encoded unit ring buffer (zero-copy).
        _VideoStreamSessionManager.Enqueue(_EncodedUnitBuffer.Buffer, unit.Offset, unit.Size, unit.TimeNs, true);
    }

    private bool CanStartStreamFrom(EncodedAccessUnit unit)
    {
        if (!unit.IsKeyFrame)
        {
            return false;
        }

        if (unit.ContainsAllRequiredParameterSets)
        {
            return true;
        }

        return _EncodedUnitBuffer.HasHeader;
    }
}
