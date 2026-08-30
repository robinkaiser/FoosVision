// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Buffers;
using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.LiveAnalysis;
using MessagePack;
using NetMQ;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderLiveAnalysisPublisher : IRecorderLiveAnalysisPublisher, IDisposable
{
    private readonly PublisherSocket _Socket = new();
    private readonly Lock _SocketLock = new();

    public void Bind(string bindAddress)
    {
        _Socket.Bind(bindAddress);
    }

    public Task PublishReplayStarted(ReplayStartedMessage replayStarted, CancellationToken ct = default)
        => Publish(replayStarted, ct);

    public Task PublishReplay(ReplayMessage replay, CancellationToken ct = default)
        => Publish(replay, ct);

    public Task PublishVisionContext(VisionContextMessage visionContext, CancellationToken ct = default)
        => Publish(visionContext, ct);

    public Task PublishBallDetectionMask(BallDetectionMaskMessage ballDetectionMask, CancellationToken ct = default)
        => Publish(ballDetectionMask, ct);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _Socket.Dispose();
    }

    private Task Publish<TMessage>(TMessage message, CancellationToken ct)
    {
        LiveAnalysisMessageType type = ProtocolTypeRegistry.GetLiveAnalysisMessageType<TMessage>();

        byte[] payload = message switch
        {
            VisionContextMessage visionContext => SerializeVisionContextMessage(visionContext),
            BallDetectionMaskMessage ballDetectionMask => SerializeBallDetectionMaskMessage(ballDetectionMask),
            _ => MessagePackSerializer.Serialize(message, cancellationToken: ct),
        };

        lock (_SocketLock)
        {
            _Socket.SendMoreFrame([(byte)type]);
            _Socket.SendFrame(payload);
        }

        return Task.CompletedTask;
    }

    private static byte[] SerializeVisionContextMessage(VisionContextMessage message)
    {
        int length = message.Length > 0 ? message.Length : message.Buffer.Length;
        ArrayBufferWriter<byte> bufferWriter = new(length + 32);
        MessagePackWriter writer = new(bufferWriter);

        writer.WriteMapHeader(2);
        writer.Write(nameof(VisionContextMessage.Buffer));
        writer.WriteBinHeader(length);
        writer.WriteRaw(message.Buffer.AsSpan(0, length));
        writer.Write(nameof(VisionContextMessage.Length));
        writer.Write(length);
        writer.Flush();

        // TODO: Optimize for copy-free NetMQ/MessagePack-Buffer
        return bufferWriter.WrittenSpan.ToArray();
    }

    private static byte[] SerializeBallDetectionMaskMessage(BallDetectionMaskMessage message)
    {
        int length = message.Length > 0 ? message.Length : message.Buffer.Length;
        ArrayBufferWriter<byte> bufferWriter = new(length + 96);
        MessagePackWriter writer = new(bufferWriter);

        writer.WriteMapHeader(6);
        writer.Write(nameof(BallDetectionMaskMessage.FrameId));
        writer.Write(message.FrameId);
        writer.Write(nameof(BallDetectionMaskMessage.TimestampNs));
        writer.Write(message.TimestampNs);
        writer.Write(nameof(BallDetectionMaskMessage.Width));
        writer.Write(message.Width);
        writer.Write(nameof(BallDetectionMaskMessage.Height));
        writer.Write(message.Height);
        writer.Write(nameof(BallDetectionMaskMessage.Buffer));
        writer.WriteBinHeader(length);
        writer.WriteRaw(message.Buffer.AsSpan(0, length));
        writer.Write(nameof(BallDetectionMaskMessage.Length));
        writer.Write(length);
        writer.Flush();

        // TODO: Optimize for copy-free NetMQ/MessagePack-Buffer
        return bufferWriter.WrittenSpan.ToArray();
    }
}
