// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Overlays;

internal static class TableOverlayStateMapper
{
    private const float _FrameWidth = 1920f;
    private const float _FrameHeight = 1080f;

    public static TableOverlayState Map(TableUpdateMessage message)
    {
        PlayerColors playerColors = new(
            message.TableConfiguration.TeamAPlayerColorArgb,
            message.TableConfiguration.TeamBPlayerColorArgb);

        return new TableOverlayState(
            Boundary: new TrapeziumOverlay(
                UpperLeft: CreatePoint(message.TableConfiguration.Boundary.UpperLeft),
                UpperRight: CreatePoint(message.TableConfiguration.Boundary.UpperRight),
                LowerLeft: CreatePoint(message.TableConfiguration.Boundary.LowerLeft),
                LowerRight: CreatePoint(message.TableConfiguration.Boundary.LowerRight)),
            Bars: [.. message.TableConfiguration.Bars.Select(bar => CreateBar(bar, playerColors))],
            Occlusions: [.. message.TableConfiguration.Occlusions.Select(CreateTrapezium)]);
    }

    private static TrapeziumOverlay CreateTrapezium(TrapeziumMessage message)
    {
        return new TrapeziumOverlay(
            UpperLeft: CreatePoint(message.UpperLeft),
            UpperRight: CreatePoint(message.UpperRight),
            LowerLeft: CreatePoint(message.LowerLeft),
            LowerRight: CreatePoint(message.LowerRight));
    }

    private static TableBarOverlay CreateBar(BarMessage message, PlayerColors playerColors)
    {
        return new TableBarOverlay(
            Type: message.Type,
            Center: CreateLine(message.Center),
            TeamArgb: GetTeamColorArgb(message.Type, playerColors));
    }

    private static LineOverlay CreateLine(LineMessage message)
    {
        return new LineOverlay(
            P0: CreatePoint(message.P0),
            P1: CreatePoint(message.P1));
    }

    private static OverlayPoint CreatePoint(PointMessage message)
    {
        return new OverlayPoint(
            X: Math.Clamp((float)message.X / _FrameWidth, 0f, 1f),
            Y: Math.Clamp((float)message.Y / _FrameHeight, 0f, 1f));
    }

    private static uint GetTeamColorArgb(BarTypeMessage barType, PlayerColors playerColors)
    {
        bool isTeamA = barType switch
        {
            BarTypeMessage.A1 => true,
            BarTypeMessage.A2 => true,
            BarTypeMessage.A5 => true,
            BarTypeMessage.A3 => true,
            _ => false,
        };

        return isTeamA ? playerColors.TeamAArgb : playerColors.TeamBArgb;
    }
}
