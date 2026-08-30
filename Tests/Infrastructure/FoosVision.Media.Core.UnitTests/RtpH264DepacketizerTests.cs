// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideoStreaming;

namespace FoosVision.Media.Core.UnitTests;

public class RtpH264DepacketizerTests
{
    [Fact]
    public void TryPushPacket_ignores_non_h264_payload_type()
    {
        RtpH264Depacketizer depacketizer = new();
        byte[] packet = CreatePacket(payloadType: 97, marker: true, sequenceNumber: 1, timestamp: 0, [0x65, 0xAA]);

        bool completed = depacketizer.TryPushPacket(packet, out _);

        Assert.False(completed);
    }

    [Fact]
    public void TryPushPacket_returns_single_nal_access_unit_on_marker()
    {
        RtpH264Depacketizer depacketizer = new();
        byte[] packet = CreatePacket(payloadType: 96, marker: true, sequenceNumber: 1, timestamp: 90_000, [0x65, 0xAA, 0xBB]);

        bool completed = depacketizer.TryPushPacket(packet, out RtpH264AccessUnit accessUnit);

        Assert.True(completed);
        Assert.True(accessUnit.IsKeyFrame);
        Assert.Equal(0, accessUnit.TimeNs);
        Assert.Equal([0x00, 0x00, 0x00, 0x01, 0x65, 0xAA, 0xBB], accessUnit.Buffer);
    }

    [Fact]
    public void TryPushPacket_combines_single_nals_until_marker()
    {
        RtpH264Depacketizer depacketizer = new();

        Assert.False(depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: false, sequenceNumber: 1, timestamp: 90_000, [0x67, 0xAA]),
            out _));

        bool completed = depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: true, sequenceNumber: 2, timestamp: 90_000, [0x68, 0xBB]),
            out RtpH264AccessUnit accessUnit);

        Assert.True(completed);
        Assert.False(accessUnit.IsKeyFrame);
        Assert.Equal([0x00, 0x00, 0x00, 0x01, 0x67, 0xAA, 0x00, 0x00, 0x00, 0x01, 0x68, 0xBB], accessUnit.Buffer);
    }

    [Fact]
    public void TryPushPacket_reassembles_fu_a_access_unit()
    {
        RtpH264Depacketizer depacketizer = new();

        Assert.False(depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: false, sequenceNumber: 1, timestamp: 90_000, [0x7C, 0x85, 0xAA, 0xBB]),
            out _));

        bool completed = depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: true, sequenceNumber: 2, timestamp: 90_000, [0x7C, 0x45, 0xCC, 0xDD]),
            out RtpH264AccessUnit accessUnit);

        Assert.True(completed);
        Assert.True(accessUnit.IsKeyFrame);
        Assert.Equal([0x00, 0x00, 0x00, 0x01, 0x65, 0xAA, 0xBB, 0xCC, 0xDD], accessUnit.Buffer);
    }

    [Fact]
    public void TryPushPacket_converts_rtp_timestamp_to_relative_time_ns()
    {
        RtpH264Depacketizer depacketizer = new();

        Assert.True(depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: true, sequenceNumber: 1, timestamp: 90_000, [0x61, 0xAA]),
            out RtpH264AccessUnit firstAccessUnit));

        Assert.True(depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: true, sequenceNumber: 2, timestamp: 99_000, [0x61, 0xBB]),
            out RtpH264AccessUnit secondAccessUnit));

        Assert.Equal(0, firstAccessUnit.TimeNs);
        Assert.Equal(100_000_000, secondAccessUnit.TimeNs);
    }

    [Fact]
    public void TryPushPacket_drops_partial_unit_after_sequence_gap()
    {
        RtpH264Depacketizer depacketizer = new();

        Assert.False(depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: false, sequenceNumber: 1, timestamp: 90_000, [0x67, 0xAA]),
            out _));

        Assert.True(depacketizer.TryPushPacket(
            CreatePacket(payloadType: 96, marker: true, sequenceNumber: 3, timestamp: 90_000, [0x68, 0xBB]),
            out RtpH264AccessUnit accessUnit));

        Assert.Equal([0x00, 0x00, 0x00, 0x01, 0x68, 0xBB], accessUnit.Buffer);
    }

    private static byte[] CreatePacket(byte payloadType, bool marker, ushort sequenceNumber, uint timestamp, byte[] payload)
    {
        byte[] packet = new byte[12 + payload.Length];
        packet[0] = 0x80;
        packet[1] = (byte)((marker ? 0x80 : 0x00) | payloadType);
        packet[2] = (byte)(sequenceNumber >> 8);
        packet[3] = (byte)sequenceNumber;
        packet[4] = (byte)(timestamp >> 24);
        packet[5] = (byte)(timestamp >> 16);
        packet[6] = (byte)(timestamp >> 8);
        packet[7] = (byte)timestamp;
        payload.CopyTo(packet.AsSpan(12));
        return packet;
    }
}
