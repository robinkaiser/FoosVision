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

        try
        {
            await Task.Delay(_Options.GracePeriod, ct);
        }
        catch (OperationCanceledException)
        {
            return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
        }

        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
            }

            RecorderConnectionResult result = await TryConnectWithinPairingBudget(discoverySession, ct);
            if (result.Success ||
                result.Failure.Value == RecorderConnectionFailure.Cancelled)
            {
                return result;
            }

            _Log.Warning(
                "Recorder discovery cycle ended without connection. Failure={0}",
                result.Failure.Value);
        }
    }

    private async Task<RecorderConnectionResult> TryConnectWithinPairingBudget(
        IRecorderDiscoverySession discoverySession,
        CancellationToken ct)
    {
        var lastFailure = RecorderConnectionFailure.NoCandidateFound;
        var triedRecorderAddresses = new HashSet<string>(StringComparer.Ordinal);

        using var discoverAndPairCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        discoverAndPairCts.CancelAfter(_Options.MaxDiscoverAndPairTime);

        while (true)
        {
            if (ct.IsCancellationRequested)
            {
                return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
            }

            if (discoverAndPairCts.IsCancellationRequested)
            {
                return RecorderConnectionResult.Failed(lastFailure);
            }

            IReadOnlyList<RecorderDiscoveryCandidate> candidates;
            try
            {
                candidates = await GetCandidatesAsync(
                    discoverySession,
                    triedRecorderAddresses,
                    discoverAndPairCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
                }

                return RecorderConnectionResult.Failed(lastFailure);
            }

            var candidate = candidates.FirstOrDefault(x => !triedRecorderAddresses.Contains(x.RecorderIpAddress));
            if (candidate is null)
            {
                try
                {
                    await Task.Delay(_Options.PollInterval, discoverAndPairCts.Token);
                    continue;
                }
                catch (OperationCanceledException)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
                    }

                    return RecorderConnectionResult.Failed(lastFailure);
                }
            }

            triedRecorderAddresses.Add(candidate.RecorderIpAddress);

            var endpoint = $"tcp://{candidate.RecorderIpAddress}:{DefaultPorts.HandshakeReqRepTcp}";
            _Log.Information(
                "Trying discovered recorder. RecorderIp={0} DiscoveryAppVersion={1} ProtocolVersion={2}",
                candidate.RecorderIpAddress,
                candidate.RecorderAppVersion,
                candidate.ProtocolVersion);

            try
            {
                using var perCandidateCts = CancellationTokenSource.CreateLinkedTokenSource(discoverAndPairCts.Token);
                perCandidateCts.CancelAfter(_Options.PerCandidateHandshakeTimeout);

                var viewerIpAddress = LocalIpPicker.PickLocalIPv4ForRemote(IPAddress.Parse(candidate.RecorderIpAddress));
                var request = new HelloRequest
                {
                    ViewerIpAddress = viewerIpAddress.ToString(),
                };

                var response = await _HandshakeClient.HelloAsync(endpoint, request, perCandidateCts.Token);
                if (!response.Accepted)
                {
                    lastFailure = response.RejectionReason == "RecorderBusy"
                        ? RecorderConnectionFailure.RecorderBusy
                        : RecorderConnectionFailure.HandshakeFailed;
                    continue;
                }

                if (response.ProtocolVersion != ProtocolVersions.Current)
                {
                    lastFailure = RecorderConnectionFailure.ProtocolMismatch;
                    continue;
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
                if (ct.IsCancellationRequested)
                {
                    return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
                }

                if (discoverAndPairCts.IsCancellationRequested)
                {
                    return RecorderConnectionResult.Failed(lastFailure);
                }

                lastFailure = RecorderConnectionFailure.HandshakeTimeout;
            }
            catch (TimeoutException)
            {
                lastFailure = RecorderConnectionFailure.HandshakeTimeout;
            }
            catch (SocketException)
            {
                lastFailure = RecorderConnectionFailure.LocalNetworkError;
            }
            catch (FormatException)
            {
                lastFailure = RecorderConnectionFailure.HandshakeFailed;
            }
            catch
            {
                lastFailure = RecorderConnectionFailure.HandshakeFailed;
            }
        }
    }

    private async Task<IReadOnlyList<RecorderDiscoveryCandidate>> GetCandidatesAsync(
        IRecorderDiscoverySession discoverySession,
        HashSet<string> triedRecorderAddresses,
        CancellationToken discoverAndPairToken)
    {
        IReadOnlyList<RecorderDiscoveryCandidate> discoveredCandidates = discoverySession.GetCandidatesRankedSnapshot();
        if (discoveredCandidates.Any(x => !triedRecorderAddresses.Contains(x.RecorderIpAddress)))
        {
            return discoveredCandidates;
        }

        IReadOnlyList<RecorderDiscoveryCandidate> fallbackCandidates =
            await _FallbackCandidateSource.GetCandidatesAsync(discoverAndPairToken);

        return [.. discoveredCandidates
            .Concat(fallbackCandidates)
            .GroupBy(x => x.RecorderIpAddress, StringComparer.Ordinal)
            .Select(x => x.First())];
    }
}
