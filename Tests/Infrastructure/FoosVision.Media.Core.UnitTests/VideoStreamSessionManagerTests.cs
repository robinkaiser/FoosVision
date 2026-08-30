// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideoStreaming;

namespace FoosVision.Media.Core.UnitTests;

public class VideoStreamSessionManagerTests
{
    [Fact]
    public void StartSession_creates_fresh_sink_for_each_run()
    {
        FakeEncodedVideoStreamSinkFactory factory = new();
        using VideoStreamSessionManager manager = new(factory);
        manager.Configure("127.0.0.1", 5560);

        manager.StartSession();
        FakeEncodedVideoStreamSink firstSink = factory.CreatedSinks.Single();

        manager.StartSession();
        FakeEncodedVideoStreamSink secondSink = Assert.Single(factory.CreatedSinks.Skip(1));

        Assert.True(firstSink.Disposed);
        Assert.NotSame(firstSink, secondSink);
        Assert.Equal(2, factory.CreatedSinks.Count);
    }

    [Fact]
    public void Configure_after_session_start_is_ignored()
    {
        FakeEncodedVideoStreamSinkFactory factory = new();
        using VideoStreamSessionManager manager = new(factory);

        manager.StartSession();
        Assert.Empty(factory.CreatedSinks);

        manager.Configure("127.0.0.1", 5560);

        Assert.Empty(factory.CreatedSinks);
    }

    [Fact]
    public void StopSession_disposes_active_sink()
    {
        FakeEncodedVideoStreamSinkFactory factory = new();
        using VideoStreamSessionManager manager = new(factory);
        manager.Configure("127.0.0.1", 5560);
        manager.StartSession();

        FakeEncodedVideoStreamSink sink = Assert.Single(factory.CreatedSinks);

        manager.StopSession();

        Assert.True(sink.Disposed);
    }

    private class FakeEncodedVideoStreamSinkFactory : IEncodedVideoStreamSinkFactory
    {
        public List<FakeEncodedVideoStreamSink> CreatedSinks { get; } = [];

        public IEncodedVideoStreamSink Create()
        {
            FakeEncodedVideoStreamSink sink = new();
            CreatedSinks.Add(sink);
            return sink;
        }
    }

    private class FakeEncodedVideoStreamSink : IEncodedVideoStreamSink
    {
        public (string IpAddress, int Port)? LastConfigureCall { get; private set; }

        public bool Disposed { get; private set; }

        public void Configure(string ipAddress, int port)
        {
            LastConfigureCall = (ipAddress, port);
        }

        public void Enqueue(byte[] buffer, int offset, int length, long timeNs, bool markAsEndOfAccessUnit)
        {
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
