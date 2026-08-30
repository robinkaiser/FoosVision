// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;
using NSubstitute;

namespace FoosVision.Adapters.Viewer.UnitTests.Connectivity;

public class RecorderConnectionServiceTests
{
    private readonly IRecorderDiscovery _Discovery = Substitute.For<IRecorderDiscovery>();
    private readonly IRecorderDiscoverySession _DiscoverySession = Substitute.For<IRecorderDiscoverySession>();
    private readonly IHandshakeClient _HandshakeClient = Substitute.For<IHandshakeClient>();

    public RecorderConnectionServiceTests()
    {
        _Discovery.Start().Returns(_DiscoverySession);
    }

    [Fact]
    public async Task ConnectAsync_keeps_discovery_running_until_cancelled_when_discovery_is_empty()
    {
        using CancellationTokenSource cts = new();
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(_ =>
        {
            cts.Cancel();
            return [];
        });

        var sut = CreateSut();

        var result = await sut.ConnectAsync(cts.Token);

        Assert.False(result.Success);
        Assert.True(result.Connection.IsNone);
        Assert.True(result.Failure.IsSome);
        Assert.Equal(RecorderConnectionFailure.Cancelled, result.Failure.Value);
        _Discovery.Received(1).Start();
        _DiscoverySession.Received(1).Dispose();
    }

    [Fact]
    public async Task ConnectAsync_returns_connected_result_when_handshake_succeeds()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
        [
            new RecorderDiscoveryCandidate("192.168.178.10", "1.2.3-test", ProtocolVersions.Current),
        ]);

        _HandshakeClient.HelloAsync(Arg.Any<string>(), Arg.Any<HelloRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "1.2.3-recorder",
                Diagnostics = new HandshakeDiagnosticsSettings
                {
                    Seq = new HandshakeSeqLoggingSettings
                    {
                        Enabled = true,
                        ServerUrl = "http://seq.local:5341",
                        MinimumLevel = "Debug",
                        SendTestEventOnStartup = true,
                    },
                    RuntimeMetrics = new HandshakeRuntimeMetricsSettings
                    {
                        Enabled = true,
                        ReportIntervalSeconds = 7,
                    },
                },
                Viewer = new HandshakeViewerSettings
                {
                    LiveVideo = new HandshakeViewerLiveVideoSettings
                    {
                        PlaybackBufferMilliseconds = 50,
                        MaxPlaybackBufferMilliseconds = 200,
                        DecoderLowLatency = false,
                        UdpReceiveBufferBytes = 1048576,
                    },
                },
            }));

        var sut = CreateSut();

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.10", result.Connection.Value.RecorderIpAddress);
        Assert.Equal("1.2.3-recorder", result.Connection.Value.RecorderAppVersion);
        Assert.Equal(ProtocolVersions.Current, result.Connection.Value.ProtocolVersion);
        Assert.True(result.Connection.Value.Diagnostics.Seq.Enabled);
        Assert.Equal("http://seq.local:5341", result.Connection.Value.Diagnostics.Seq.ServerUrl);
        Assert.Equal("Debug", result.Connection.Value.Diagnostics.Seq.MinimumLevel);
        Assert.True(result.Connection.Value.Diagnostics.Seq.SendTestEventOnStartup);
        Assert.True(result.Connection.Value.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(7, result.Connection.Value.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
        Assert.Equal(50, result.Connection.Value.Viewer.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(200, result.Connection.Value.Viewer.LiveVideo.MaxPlaybackBufferMilliseconds);
        Assert.False(result.Connection.Value.Viewer.LiveVideo.DecoderLowLatency);
        Assert.Equal(1048576, result.Connection.Value.Viewer.LiveVideo.UdpReceiveBufferBytes);
        Assert.True(result.Failure.IsNone);
    }

    [Fact]
    public async Task ConnectAsync_tries_next_candidate_after_protocol_mismatch()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
        [
            new RecorderDiscoveryCandidate("192.168.178.10", "1.2.3-test", ProtocolVersions.Current),
            new RecorderDiscoveryCandidate("192.168.178.11", "1.2.3-test", ProtocolVersions.Current),
        ]);

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.10:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current + 1,
                RecorderAppVersion = "future",
            }));

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.11:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "working",
            }));

        var sut = CreateSut();

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.11", result.Connection.Value.RecorderIpAddress);
    }

    [Fact]
    public async Task ConnectAsync_tries_next_candidate_when_candidate_rejects_handshake()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
        [
            new RecorderDiscoveryCandidate("192.168.178.10", "1.2.3-test", ProtocolVersions.Current),
            new RecorderDiscoveryCandidate("192.168.178.11", "1.2.3-test", ProtocolVersions.Current),
        ]);

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.10:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "busy",
                Accepted = false,
                RejectionReason = "RecorderBusy",
            }));

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.11:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "working",
            }));

        var sut = CreateSut();

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.11", result.Connection.Value.RecorderIpAddress);
    }

    [Fact]
    public async Task ConnectAsync_tries_next_candidate_when_candidate_times_out()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
        [
            new RecorderDiscoveryCandidate("192.168.178.10", "1.2.3-test", ProtocolVersions.Current),
            new RecorderDiscoveryCandidate("192.168.178.11", "1.2.3-test", ProtocolVersions.Current),
        ]);

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.10:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<HelloResponse>>(_ => throw new TimeoutException());

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.11:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "working",
            }));

        var sut = CreateSut();

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.11", result.Connection.Value.RecorderIpAddress);
    }

    [Fact]
    public async Task ConnectAsync_returns_cancelled_when_token_is_already_cancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sut = CreateSut();

        var result = await sut.ConnectAsync(cts.Token);

        Assert.False(result.Success);
        Assert.True(result.Connection.IsNone);
        Assert.Equal(RecorderConnectionFailure.Cancelled, result.Failure.Value);
    }

    [Fact]
    public async Task ConnectAsync_tries_next_candidate_when_first_handshake_fails()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
        [
            new RecorderDiscoveryCandidate("192.168.178.10", "broken", ProtocolVersions.Current),
            new RecorderDiscoveryCandidate("192.168.178.11", "working", ProtocolVersions.Current),
        ]);

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.10:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<HelloResponse>>(_ => throw new TimeoutException());

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.11:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "working",
            }));

        var sut = CreateSut();

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.11", result.Connection.Value.RecorderIpAddress);
    }

    [Fact]
    public async Task ConnectAsync_finds_candidate_while_discovery_is_running()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
            [],
            [new RecorderDiscoveryCandidate("192.168.178.11", "working", ProtocolVersions.Current)]);

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.11:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "working",
            }));

        var sut = CreateSut();

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.11", result.Connection.Value.RecorderIpAddress);
    }

    [Fact]
    public async Task ConnectAsync_uses_fallback_candidate_when_discovery_is_empty()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns([]);

        _HandshakeClient.HelloAsync(
                "tcp://192.168.178.12:5555",
                Arg.Any<HelloRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HelloResponse
            {
                ProtocolVersion = ProtocolVersions.Current,
                RecorderAppVersion = "working",
            }));

        var sut = CreateSut(new StaticFallbackCandidateSource(
        [
            new RecorderDiscoveryCandidate("192.168.178.12", "direct-probe", ProtocolVersions.Current),
        ]));

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.12", result.Connection.Value.RecorderIpAddress);
    }

    [Fact]
    public async Task ConnectAsync_retries_after_global_pairing_budget_expires_during_handshake()
    {
        _DiscoverySession.GetCandidatesRankedSnapshot().Returns(
        [
            new RecorderDiscoveryCandidate("192.168.178.10", "late", ProtocolVersions.Current),
        ]);

        int handshakeAttempts = 0;
        _HandshakeClient.HelloAsync(Arg.Any<string>(), Arg.Any<HelloRequest>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                handshakeAttempts++;
                if (handshakeAttempts > 1)
                {
                    return new HelloResponse
                    {
                        ProtocolVersion = ProtocolVersions.Current,
                        RecorderAppVersion = "working",
                    };
                }

                var token = callInfo.ArgAt<CancellationToken>(2);
                await Task.Delay(TimeSpan.FromSeconds(5), token);
                return new HelloResponse();
            });

        var sut = new RecorderConnectionService(
            _Discovery,
            _HandshakeClient,
            new RecorderConnectionOptions(
                GracePeriod: TimeSpan.Zero,
                MaxDiscoverAndPairTime: TimeSpan.FromMilliseconds(20),
                PollInterval: TimeSpan.FromMilliseconds(1),
                PerCandidateHandshakeTimeout: TimeSpan.FromSeconds(5)),
            new EmptyFallbackCandidateSource());

        var result = await sut.ConnectAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Connection.IsSome);
        Assert.Equal("192.168.178.10", result.Connection.Value.RecorderIpAddress);
        Assert.Equal(2, handshakeAttempts);
    }

    private RecorderConnectionService CreateSut()
        => CreateSut(new EmptyFallbackCandidateSource());

    private RecorderConnectionService CreateSut(IRecorderFallbackCandidateSource fallbackCandidateSource)
    {
        return new RecorderConnectionService(
            _Discovery,
            _HandshakeClient,
            new RecorderConnectionOptions(
                GracePeriod: TimeSpan.Zero,
                MaxDiscoverAndPairTime: TimeSpan.FromMilliseconds(200),
                PollInterval: TimeSpan.FromMilliseconds(1),
                PerCandidateHandshakeTimeout: TimeSpan.FromSeconds(1)),
            fallbackCandidateSource);
    }

    private sealed class EmptyFallbackCandidateSource : IRecorderFallbackCandidateSource
    {
        public Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<RecorderDiscoveryCandidate>>([]);
        }
    }

    private sealed class StaticFallbackCandidateSource : IRecorderFallbackCandidateSource
    {
        private readonly IReadOnlyList<RecorderDiscoveryCandidate> _Candidates;

        public StaticFallbackCandidateSource(IReadOnlyList<RecorderDiscoveryCandidate> candidates)
        {
            _Candidates = candidates;
        }

        public Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(CancellationToken ct)
        {
            return Task.FromResult(_Candidates);
        }
    }
}
