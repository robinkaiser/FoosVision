// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using System.Net.Sockets;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Core.EncodedVideo.AnnexB;

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public class UdpRtpH264StreamSink : IEncodedVideoStreamSink
{
    private const int _RtpHeaderBytes = 12;
    private const int _MaxUdpPayloadBytes = 1200;
    private const int _MaxRtpPayloadBytes = _MaxUdpPayloadBytes - _RtpHeaderBytes;
    private const int _FuAHeaderBytes = 2;
    private const int _MaxFuAPayloadBytes = _MaxRtpPayloadBytes - _FuAHeaderBytes;
    private const byte _RtpVersion = 2;
    private const byte _PayloadTypeH264 = 96;
    private const byte _FuAType = 28;
    private const int _RtpClockRate = 90_000;
    private const uint _RestartTimestampGap = 3_000;
    private const int _SocketSendBufferBytes = 1 * 1024 * 1024;
    private const int _MaxQueuedPackets = 2048;

    private static readonly Source _Log = new("UdpRtpH264StreamSink");
    private static readonly SourceInterval _DropLog = new("UdpRtpH264StreamSink.Drop", TimeSpan.FromSeconds(1));

    private readonly IntervalMetric? _SendAccessUnitInterval;
    private readonly Lock _Gate = new();
    private readonly Queue<byte[]> _Queue = new();
    private readonly AutoResetEvent _Signal = new(false);
    private readonly CancellationTokenSource _Cts = new();
    private readonly AnnexBNalRange[] _ParsedNalRanges = new AnnexBNalRange[32];
    private readonly uint _Ssrc = unchecked((uint)Random.Shared.NextInt64(1, uint.MaxValue));

    private Socket? _Socket;
    private EndPoint? _Remote;
    private Thread? _Thread;
    private ushort _SequenceNumber;
    private long? _LastSourceTimeNs;
    private uint _TimestampBase;
    private uint _LastTimestamp;
    private volatile bool _Enabled;

    public UdpRtpH264StreamSink(RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        if (options.Enabled)
        {
            _SendAccessUnitInterval = new IntervalMetric(
                options.CreateMetricName("Recorder.RtpH264Sender.AccessUnitSendInterval"),
                _Log,
                options.GetReportInterval());
        }
    }

    public void Configure(string ipAddress, int port)
    {
        if (!IPAddress.TryParse(ipAddress, out IPAddress? ip))
        {
            _Log.Warning("Configure: invalid IP address '{Ip}'. RTP streaming disabled.", ipAddress);
            _Enabled = false;
            return;
        }

        if (port is < 1 or > 65535)
        {
            _Log.Warning("Configure: invalid port '{Port}'. RTP streaming disabled.", port);
            _Enabled = false;
            return;
        }

        lock (_Gate)
        {
            _Remote = new IPEndPoint(ip, port);

            _Socket ??= CreateSocket(ip);

            if (_Thread is null)
            {
                _Thread = new Thread(SendLoop)
                {
                    IsBackground = true,
                    Name = "FoosVision.UdpRtpH264StreamSink",
                };
                _Thread.Start();
            }

            _Enabled = true;
        }

        _Log.Information("Configured UDP RTP/H264 stream target: {Ip}:{Port}", ipAddress, port);
    }

    private static Socket CreateSocket(IPAddress remoteAddress)
    {
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = true,
            SendBufferSize = _SocketSendBufferBytes,
        };

        IPAddress? localAddress = LocalRtpEndpointResolver.PickLocalIPv4ForRemote(remoteAddress);
        if (localAddress is null)
        {
            _Log.Warning(
                "No local IPv4 bind address found for RTP target {RemoteIp}; using unbound UDP socket.",
                remoteAddress);
            return socket;
        }

        try
        {
            socket.Bind(new IPEndPoint(localAddress, 0));
            _Log.Information(
                "Bound UDP RTP/H264 stream socket to local address {LocalIp} for target {RemoteIp}.",
                localAddress,
                remoteAddress);
        }
        catch (SocketException ex)
        {
            _Log.Warning(
                "Failed to bind UDP RTP/H264 stream socket to local address {LocalIp} for target {RemoteIp}: {ErrorCode}",
                localAddress,
                remoteAddress,
                ex.SocketErrorCode);
        }

        return socket;
    }

    public void Enqueue(byte[] buffer, int offset, int length, long timeNs, bool markAsEndOfAccessUnit)
    {
        if (!_Enabled || length <= 0)
        {
            return;
        }

        if ((uint)offset >= (uint)buffer.Length || offset + length > buffer.Length)
        {
            return;
        }

        int count = AnnexBParser.FindNals(buffer, offset, offset + length, _ParsedNalRanges, _ParsedNalRanges.Length);
        if (count <= 0)
        {
            return;
        }

        uint timestamp = CreateTimestamp(timeNs);
        for (int i = 0; i < count; i++)
        {
            AnnexBNalRange nal = _ParsedNalRanges[i];
            int nalLength = nal.EndOffsetExclusive - nal.HeaderOffset;
            bool marker = markAsEndOfAccessUnit && i == count - 1;

            if (nalLength <= _MaxRtpPayloadBytes)
            {
                QueuePacket(CreateSingleNalPacket(buffer, nal.HeaderOffset, nalLength, timestamp, marker));
            }
            else
            {
                QueueFragmentedNalPackets(buffer, nal.HeaderOffset, nalLength, timestamp, marker);
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _Cts.Cancel();
        _Signal.Set();

        TryIgnore(() => _Thread?.Join(TimeSpan.FromSeconds(1)));

        lock (_Gate)
        {
            TryIgnore(() => _Socket?.Close());
            TryIgnore(() => _Socket?.Dispose());

            _Socket = null;
            _Remote = null;
            _Queue.Clear();
        }

        TryIgnore(_Signal.Dispose);
        TryIgnore(_Cts.Dispose);
    }

    private uint CreateTimestamp(long timeNs)
    {
        uint relativeTimestamp = unchecked((uint)((timeNs * _RtpClockRate) / 1_000_000_000L));

        lock (_Gate)
        {
            if (_LastSourceTimeNs.HasValue && timeNs < _LastSourceTimeNs.Value)
            {
                _TimestampBase = unchecked(_LastTimestamp + _RestartTimestampGap);
            }

            _LastSourceTimeNs = timeNs;
            uint timestamp = unchecked(_TimestampBase + relativeTimestamp);
            _LastTimestamp = timestamp;
            return timestamp;
        }
    }

    private byte[] CreateSingleNalPacket(byte[] sourceBuffer, int sourceOffset, int length, uint timestamp, bool marker)
    {
        byte[] packet = new byte[_RtpHeaderBytes + length];
        WriteRtpHeader(packet, timestamp, marker);
        Buffer.BlockCopy(sourceBuffer, sourceOffset, packet, _RtpHeaderBytes, length);
        return packet;
    }

    private void QueueFragmentedNalPackets(byte[] sourceBuffer, int sourceOffset, int length, uint timestamp, bool markLastFragment)
    {
        byte nalHeader = sourceBuffer[sourceOffset];
        byte fuIndicator = (byte)((nalHeader & 0xE0) | _FuAType);
        byte nalType = (byte)(nalHeader & 0x1F);
        int payloadOffset = sourceOffset + 1;
        int remaining = length - 1;
        bool isStart = true;

        while (remaining > 0)
        {
            int chunk = Math.Min(_MaxFuAPayloadBytes, remaining);
            bool isEnd = chunk == remaining;

            byte[] packet = new byte[_RtpHeaderBytes + _FuAHeaderBytes + chunk];
            WriteRtpHeader(packet, timestamp, markLastFragment && isEnd);

            packet[_RtpHeaderBytes] = fuIndicator;
            packet[_RtpHeaderBytes + 1] = (byte)(
                (isStart ? 0x80 : 0x00) |
                (isEnd ? 0x40 : 0x00) |
                nalType);

            Buffer.BlockCopy(sourceBuffer, payloadOffset, packet, _RtpHeaderBytes + _FuAHeaderBytes, chunk);
            QueuePacket(packet);

            payloadOffset += chunk;
            remaining -= chunk;
            isStart = false;
        }
    }

    private void WriteRtpHeader(byte[] packet, uint timestamp, bool marker)
    {
        packet[0] = (byte)(_RtpVersion << 6);
        packet[1] = (byte)((marker ? 0x80 : 0x00) | _PayloadTypeH264);

        ushort sequenceNumber = unchecked(++_SequenceNumber);
        packet[2] = (byte)(sequenceNumber >> 8);
        packet[3] = (byte)sequenceNumber;

        packet[4] = (byte)(timestamp >> 24);
        packet[5] = (byte)(timestamp >> 16);
        packet[6] = (byte)(timestamp >> 8);
        packet[7] = (byte)timestamp;

        packet[8] = (byte)(_Ssrc >> 24);
        packet[9] = (byte)(_Ssrc >> 16);
        packet[10] = (byte)(_Ssrc >> 8);
        packet[11] = (byte)_Ssrc;
    }

    private void QueuePacket(byte[] packet)
    {
        lock (_Gate)
        {
            _Queue.Enqueue(packet);

            while (_Queue.Count > _MaxQueuedPackets)
            {
                _Queue.Dequeue();
                _DropLog.Warning("Queue overflow: dropped oldest RTP packet");
            }
        }

        _Signal.Set();
    }

    private void SendLoop()
    {
        CancellationToken ct = _Cts.Token;

        while (!ct.IsCancellationRequested)
        {
            byte[]? packet = null;

            lock (_Gate)
            {
                if (_Enabled &&
                    _Socket != null &&
                    _Remote != null &&
                    _Queue.Count > 0)
                {
                    packet = _Queue.Dequeue();
                }
            }

            if (packet != null)
            {
                try
                {
                    SendPacket(packet);
                }
                catch (SocketException ex)
                {
                    _DropLog.Warning("RTP send failed: {ErrorCode}", ex.SocketErrorCode);
                }
                catch (Exception ex)
                {
                    _DropLog.Warning("RTP send failed: {Error}", ex.GetType().Name);
                }

                continue;
            }

            _Signal.WaitOne();
        }
    }

    private void SendPacket(byte[] packet)
    {
        Socket? socket;
        EndPoint? remote;

        lock (_Gate)
        {
            socket = _Socket;
            remote = _Remote;
        }

        if (socket == null || remote == null)
        {
            return;
        }

        socket.SendTo(packet, 0, packet.Length, SocketFlags.None, remote);

        if (IsAccessUnitEndPacket(packet))
        {
            _SendAccessUnitInterval?.Record();
        }
    }

    private static bool IsAccessUnitEndPacket(byte[] packet)
    {
        return packet.Length > 1 && (packet[1] & 0x80) != 0;
    }

    private static void TryIgnore(Action action)
    {
        try
        {
            action();
        }
        catch
        {
        }
    }
}
