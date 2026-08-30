// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.NetDiscovery;
using FoosVision.NetMq;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.Live;
using NetMQ;

Console.WriteLine("FoosVision Viewer Protocol Tester");

using var handshakeClient = new HandshakeClient();
var recorderDiscovery = new UdpRecorderDiscovery();

var recorderConnectionService = new RecorderConnectionService(
    recorderDiscovery,
    handshakeClient,
    RecorderConnectionOptions.Default);

var selection = await recorderConnectionService.ConnectAsync(CancellationToken.None);

if (!selection.Success)
{
    Console.WriteLine(GetConnectionFailureMessage(selection.Failure.Value));
    NetMQConfig.Cleanup();
    return;
}

var connection = selection.Connection.Value;

Console.WriteLine();
Console.WriteLine($"Selected recorder: {connection.RecorderIpAddress}");
Console.WriteLine($"ProtocolVersion  : {connection.ProtocolVersion}");
Console.WriteLine($"AppVersion       : {connection.RecorderAppVersion}");
Console.WriteLine();

var commandsAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.CommandsReqRepTcp}";
var eventsAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.EventsPubSubTcp}";
var liveDataAddress = $"tcp://{connection.RecorderIpAddress}:{DefaultPorts.LiveDataPubSubTcp}";

using var commandClient = new RecorderCommandClient(commandsAddress);
using var eventSubscriber = new RecorderEventSubscriber(eventsAddress);
using var liveDataSubscriber = new RecorderLiveDataSubscriber(liveDataAddress);

Console.WriteLine($"Commands REQ connect: {commandsAddress}");
Console.WriteLine($"Events   SUB connect: {eventsAddress}");
Console.WriteLine($"Live     SUB connect: {liveDataAddress}");
Console.WriteLine();

WireEventPrinting(eventSubscriber);
WireLivePrinting(liveDataSubscriber);
PrintCommandMenu();

using var cts = new CancellationTokenSource();
var ctrlCCount = 0;

Console.CancelKeyPress += (_, e) =>
{
    ctrlCCount++;

    if (ctrlCCount == 1)
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine("Stopping (Ctrl+C pressed).");
        return;
    }

    e.Cancel = false;
};

try
{
    while (!cts.IsCancellationRequested)
    {
        Console.Write("> ");
        var line = await ReadLineAsync(cts.Token);
        if (line is null) break;

        line = line.Trim();
        if (line.Length == 0) continue;

        if (line.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("h", StringComparison.OrdinalIgnoreCase))
        {
            PrintCommandMenu();
            continue;
        }

        if (!int.TryParse(line, out var choice))
        {
            Console.WriteLine("Invalid input. Enter a number or 'help'.");
            continue;
        }

        var cmdId = Guid.NewGuid();

        var actions = new Dictionary<int, Func<Guid, CancellationToken, Task>>
        {
            [1] = (id, ct) => Send(new StartInstallCommand { CommandId = id }, ct),
            [2] = (id, ct) => Send(new StopInstallCommand { CommandId = id }, ct),
            [10] = (id, ct) => Send(new StartGameCommand { CommandId = id }, ct),
            [11] = (id, ct) => Send(new StopGameCommand { CommandId = id }, ct),
        };

        if (!actions.TryGetValue(choice, out var action))
        {
            Console.WriteLine("Unknown command number. Type 'help' to see the list.");
            continue;
        }

        await action(cmdId, cts.Token);
    }
}
catch (OperationCanceledException)
{
    // expected
}
finally
{
    NetMQConfig.Cleanup();
}

Console.WriteLine("Viewer stopped.");

static void PrintCommandMenu()
{
    Console.WriteLine("Send commands (enter number):");
    Console.WriteLine("  1  StartInstall");
    Console.WriteLine("  2  StopInstall");
    Console.WriteLine(" 10  StartGame");
    Console.WriteLine(" 11  StopGame");
    Console.WriteLine("Type 'help' to show this menu again.");
    Console.WriteLine();
}

static string GetConnectionFailureMessage(RecorderConnectionFailure failure)
{
    return failure switch
    {
        RecorderConnectionFailure.NoCandidateFound => "No candidate responded to handshake.",
        RecorderConnectionFailure.HandshakeTimeout => "Recorder pairing timed out during handshake.",
        RecorderConnectionFailure.ProtocolMismatch => "No recorder matched the required protocol version.",
        RecorderConnectionFailure.HandshakeFailed => "Recorder pairing failed during handshake.",
        RecorderConnectionFailure.LocalNetworkError => "Recorder pairing failed because the local network route could not be resolved.",
        RecorderConnectionFailure.Cancelled => "Recorder pairing was cancelled.",
        _ => $"Recorder pairing failed: {failure}",
    };
}

static void PrintResponse(FoosVision.Protocol.Messages.Common.CommandResponse resp)
{
    if (resp.Accepted)
        Console.WriteLine($"[REP] Accepted CommandId={resp.CommandId}");
    else
        Console.WriteLine($"[REP] Rejected CommandId={resp.CommandId} Error='{resp.Error}'");
}

static Task<string?> ReadLineAsync(CancellationToken ct)
    => Task.Run(Console.ReadLine, ct);

static void WireEventPrinting(RecorderEventSubscriber sub)
{
    SubscribeEvent<RecorderRuntimeStateChanged>(
        sub,
        e => $"Sequence={e.Sequence} Mode={e.Mode} SessionId={e.ActiveSessionId} Reason='{e.Reason}' Detail='{e.Detail}'");
}

static void WireLivePrinting(RecorderLiveDataSubscriber sub)
{
    sub.Subscribe<TrackingFrameMessage>(m =>
    {
        var ball = m.BallPosition is null ? "none" : $"({m.BallPosition.X:F1}, {m.BallPosition.Y:F1})";
        Console.WriteLine($"[LIVE] {DateTime.Now:HH:mm:ss.fff} TrackingFrame FrameId={m.FrameId} Ts={m.TimestampNs} BallFound={m.IsBallFound} Ball={ball} BallCandidates={m.BallCandidates.Count} Observations={m.Observations.Count} Possession='{m.Possession}'");
    });

    sub.Subscribe<TableUpdateMessage>(m =>
    {
        Console.WriteLine($"[LIVE] {DateTime.Now:HH:mm:ss.fff} TableUpdate Bars={m.TableConfiguration.Bars.Count} TeamA=0x{m.TableConfiguration.TeamAPlayerColorArgb:X8} TeamB=0x{m.TableConfiguration.TeamBPlayerColorArgb:X8}");
    });
}

static void SubscribeEvent<TEvent>(RecorderEventSubscriber sub, Func<TEvent, string> details)
{
    sub.Subscribe<TEvent>(e => Console.WriteLine($"[EVT] {DateTime.Now:HH:mm:ss.fff} {typeof(TEvent).Name} {details(e)}"));
}

async Task Send<TCommand>(TCommand cmd, CancellationToken ct)
{
    var resp = await commandClient.SendAsync(cmd, ct);
    PrintResponse(resp);
}
