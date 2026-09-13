// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using System.Net.Sockets;
using FoosVision.Common.Logging;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Connectivity.Discovery;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Adapters.Viewer.Connectivity;

public class RecorderConnectionService : IRecorderConnectionService
{
    private static readonly Source _Log = new("Adapters.Viewer.Connectivity.RecorderConnectionService");

    private readonly IRecorderDiscovery _Discovery;
    private readonly IHandshakeClient _HandshakeClient;
    private readonly IRecorderFallbackCandidateSource _FallbackCandidateSource;
    private readonly RecorderConnectionOptions _Options;

    public RecorderConnectionService(
        IRecorderDiscovery discovery,
        IHandshakeClient handshakeClient,
        RecorderConnectionOptions? options = null,
        IRecorderFallbackCandidateSource? fallbackCandidateSource = null)
    {
        _Discovery = discovery;
        _HandshakeClient = handshakeClient;
        _FallbackCandidateSource = fallbackCandidateSource ?? new LocalSubnetRecorderFallbackCandidateSource();
        _Options = options ?? RecorderConnectionOptions.Default;
    }

    public async Task<RecorderConnectionResult> ConnectAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
        }

        _Log.Information(
            "Starting recorder discovery. ExpectedIdentity={0}",
            DiscoveryIdentity.DescribeRecorderSearchIdentity(ProtocolVersions.Current));

        using var discoverySession = _Discovery.Start();
        FallbackProbeSchedule fallbackProbeSchedule = new(DateTime.UtcNow + _Options.GracePeriod);

        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
            }

            IReadOnlyList<RecorderDiscoveryCandidate> candidates;
            try
            {
                candidates = await GetCandidatesAsync(discoverySession, fallbackProbeSchedule, ct);
            }
            catch (OperationCanceledException)
            {
                return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
            }

            if (candidates.Count == 0)
            {
                try
                {
                    await Task.Delay(_Options.PollInterval, ct);
                    continue;
                }
                catch (OperationCanceledException)
                {
                    return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
                }
            }

            foreach (RecorderDiscoveryCandidate candidate in candidates)
            {
                if (ct.IsCancellationRequested)
                {
                    return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
                }

                RecorderConnectionResult result = await TryConnectCandidateAsync(candidate, ct);
                if (result.Success ||
                    result.Failure.Value == RecorderConnectionFailure.Cancelled)
                {
                    return result;
                }

                discoverySession.RemoveCandidate(candidate.RecorderIpAddress);
            }
        }
    }

    private async Task<RecorderConnectionResult> TryConnectCandidateAsync(
        RecorderDiscoveryCandidate candidate,
        CancellationToken ct)
    {
        var endpoint = $"tcp://{candidate.RecorderIpAddress}:{DefaultPorts.HandshakeReqRepTcp}";
        _Log.Information(
            "Trying discovered recorder. RecorderIp={0} DiscoveryAppVersion={1} ProtocolVersion={2}",
            candidate.RecorderIpAddress,
            candidate.RecorderAppVersion,
            candidate.ProtocolVersion);

        try
        {
            using var perCandidateCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCandidateCts.CancelAfter(_Options.PerCandidateHandshakeTimeout);

            var viewerIpAddress = LocalIpPicker.PickLocalIPv4ForRemote(IPAddress.Parse(candidate.RecorderIpAddress));
            var request = new HelloRequest
            {
                ViewerIpAddress = viewerIpAddress.ToString(),
            };

            var response = await _HandshakeClient.HelloAsync(endpoint, request, perCandidateCts.Token);
            if (!response.Accepted)
            {
                RecorderConnectionFailure failure = response.RejectionReason == "RecorderBusy"
                    ? RecorderConnectionFailure.RecorderBusy
                    : RecorderConnectionFailure.HandshakeFailed;

                return RecorderConnectionResult.Failed(failure);
            }

            if (response.ProtocolVersion != ProtocolVersions.Current)
            {
                return RecorderConnectionResult.Failed(RecorderConnectionFailure.ProtocolMismatch);
            }

            return RecorderConnectionResult.Connected(
                new RecorderConnection(
                    RecorderIpAddress: candidate.RecorderIpAddress,
                    RecorderAppVersion: response.RecorderAppVersion,
                    ProtocolVersion: response.ProtocolVersion,
                    Diagnostics: response.Diagnostics,
                    Viewer: response.Viewer));
        }
        catch (OperationCanceledException)
        {
            return ct.IsCancellationRequested
                ? RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled)
                : RecorderConnectionResult.Failed(RecorderConnectionFailure.HandshakeTimeout);
        }
        catch (TimeoutException)
        {
            return RecorderConnectionResult.Failed(RecorderConnectionFailure.HandshakeTimeout);
        }
        catch (SocketException)
        {
            return RecorderConnectionResult.Failed(RecorderConnectionFailure.LocalNetworkError);
        }
        catch (FormatException)
        {
            return RecorderConnectionResult.Failed(RecorderConnectionFailure.HandshakeFailed);
        }
        catch
        {
            return RecorderConnectionResult.Failed(RecorderConnectionFailure.HandshakeFailed);
        }
    }

    private async Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(
        IRecorderDiscoverySession discoverySession,
        FallbackProbeSchedule fallbackProbeSchedule,
        CancellationToken ct)
    {
        IReadOnlyList<RecorderDiscoveryCandidate> discoveredCandidates = discoverySession.GetCandidatesRankedSnapshot();
        if (discoveredCandidates.Count > 0)
        {
            return discoveredCandidates;
        }

        if (DateTime.UtcNow < fallbackProbeSchedule.NextProbeAt)
        {
            return discoveredCandidates;
        }

        fallbackProbeSchedule.NextProbeAt = DateTime.UtcNow + _Options.GracePeriod;

        IReadOnlyList<RecorderDiscoveryCandidate> fallbackCandidates =
            await _FallbackCandidateSource.GetCandidatesAsync(ct);

        return [.. discoveredCandidates
            .Concat(fallbackCandidates)
            .GroupBy(x => x.RecorderIpAddress, StringComparer.Ordinal)
            .Select(x => x.First())];
    }

    private sealed class FallbackProbeSchedule
    {
        public FallbackProbeSchedule(DateTime nextProbeAt)
        {
            NextProbeAt = nextProbeAt;
        }

        public DateTime NextProbeAt { get; set; }
    }
}
