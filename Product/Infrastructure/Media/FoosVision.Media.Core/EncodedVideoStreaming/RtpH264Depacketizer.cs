// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Buffers.Binary;

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public class RtpH264Depacketizer
{
    private const int _RtpHeaderBytes = 12;
    private const byte _RtpVersion = 2;
    private const byte _PayloadTypeH264 = 96;
    private const byte _NalTypeMask = 0x1F;
    private const byte _NalHeaderNriMask = 0x60;
    private const byte _FuAType = 28;
    private const byte _FuStartBit = 0x80;
    private const byte _FuEndBit = 0x40;
    private const int _RtpClockRate = 90_000;
    private static readonly byte[] _StartCode = [0x00, 0x00, 0x00, 0x01];

    private readonly MemoryStream _AccessUnitBuffer = new();
    private readonly MemoryStream _FragmentBuffer = new();

    private uint? _FirstTimestamp;
    private uint? _AccessUnitTimestamp;
    private ushort? _LastSequenceNumber;
    private bool _FragmentStarted;
    private bool _AccessUnitIsKeyFrame;

    public bool TryPushPacket(ReadOnlySpan<byte> packet, out RtpH264AccessUnit accessUnit)
    {
        accessUnit = default;

        if (!TryParsePacket(packet, out RtpPacket rtpPacket))
        {
            return false;
        }

        if (_LastSequenceNumber.HasValue &&
            unchecked((ushort)(_LastSequenceNumber.Value + 1)) != rtpPacket.SequenceNumber)
        {
            ResetAccessUnit();
        }

        _LastSequenceNumber = rtpPacket.SequenceNumber;
        _FirstTimestamp ??= rtpPacket.Timestamp;
        _AccessUnitTimestamp ??= rtpPacket.Timestamp;

        if (!AppendPayload(rtpPacket.Payload))
        {
            ResetAccessUnit();
            return false;
        }

        if (!rtpPacket.Marker)
        {
            return false;
        }

        if (_AccessUnitBuffer.Length == 0)
        {
            ResetAccessUnit();
            return false;
        }

        accessUnit = new RtpH264AccessUnit(
            _AccessUnitBuffer.ToArray(),
            ToTimeNs(_AccessUnitTimestamp!.Value),
            _AccessUnitIsKeyFrame);
        ResetAccessUnit();
        return true;
    }

    public void Reset()
    {
        _FirstTimestamp = null;
        _LastSequenceNumber = null;
        ResetAccessUnit();
    }

    private bool AppendPayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0)
        {
            return false;
        }

        byte nalHeader = payload[0];
        byte nalType = (byte)(nalHeader & _NalTypeMask);
        return nalType == _FuAType
            ? AppendFuA(payload)
            : AppendSingleNal(payload, nalType);
    }

    private bool AppendSingleNal(ReadOnlySpan<byte> payload, byte nalType)
    {
        if (_FragmentStarted)
        {
            return false;
        }

        WriteStartCode(_AccessUnitBuffer);
        _AccessUnitBuffer.Write(payload);
        _AccessUnitIsKeyFrame |= nalType == 5;
        return true;
    }

    private bool AppendFuA(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 3)
        {
            return false;
        }

        byte fuIndicator = payload[0];
        byte fuHeader = payload[1];
        bool isStart = (fuHeader & _FuStartBit) != 0;
        bool isEnd = (fuHeader & _FuEndBit) != 0;
        byte nalType = (byte)(fuHeader & _NalTypeMask);

        if (isStart)
        {
            _FragmentBuffer.SetLength(0);
            byte reconstructedHeader = (byte)((fuIndicator & 0x80) | (fuIndicator & _NalHeaderNriMask) | nalType);
            _FragmentBuffer.WriteByte(reconstructedHeader);
            _FragmentStarted = true;
        }
        else if (!_FragmentStarted)
        {
            return false;
        }

        _FragmentBuffer.Write(payload[2..]);

        if (!isEnd)
        {
            return true;
        }

        WriteStartCode(_AccessUnitBuffer);
        _FragmentBuffer.Position = 0;
        _FragmentBuffer.CopyTo(_AccessUnitBuffer);
        _FragmentBuffer.SetLength(0);
        _FragmentStarted = false;
        _AccessUnitIsKeyFrame |= nalType == 5;
        return true;
    }

    private static bool TryParsePacket(ReadOnlySpan<byte> packet, out RtpPacket rtpPacket)
    {
        rtpPacket = default;

        if (packet.Length < _RtpHeaderBytes)
        {
            return false;
        }

        byte version = (byte)(packet[0] >> 6);
        bool hasPadding = (packet[0] & 0x20) != 0;
        bool hasExtension = (packet[0] & 0x10) != 0;
        int csrcCount = packet[0] & 0x0F;
        byte payloadType = (byte)(packet[1] & 0x7F);
        if (version != _RtpVersion || hasPadding || hasExtension || payloadType != _PayloadTypeH264)
        {
            return false;
        }

        int headerBytes = _RtpHeaderBytes + (csrcCount * 4);
        if (packet.Length <= headerBytes)
        {
            return false;
        }

        rtpPacket = new RtpPacket(
            (packet[1] & 0x80) != 0,
            BinaryPrimitives.ReadUInt16BigEndian(packet[2..4]),
            BinaryPrimitives.ReadUInt32BigEndian(packet[4..8]),
            packet[headerBytes..]);
        return true;
    }

    private long ToTimeNs(uint timestamp)
    {
        uint relativeTimestamp = unchecked(timestamp - _FirstTimestamp!.Value);
        return (relativeTimestamp * 1_000_000_000L) / _RtpClockRate;
    }

    private void ResetAccessUnit()
    {
        _AccessUnitBuffer.SetLength(0);
        _FragmentBuffer.SetLength(0);
        _AccessUnitTimestamp = null;
        _FragmentStarted = false;
        _AccessUnitIsKeyFrame = false;
    }

    private static void WriteStartCode(Stream stream)
    {
        stream.Write(_StartCode);
    }

    private readonly ref struct RtpPacket(
        bool marker,
        ushort sequenceNumber,
        uint timestamp,
        ReadOnlySpan<byte> payload)
    {
        public bool Marker { get; } = marker;

        public ushort SequenceNumber { get; } = sequenceNumber;

        public uint Timestamp { get; } = timestamp;

        public ReadOnlySpan<byte> Payload { get; } = payload;
    }
}
