// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Game;
using FoosVision.Protocol.Connectivity.Abstractions;

namespace FoosVision.Viewer.Composition.Modules;

public class GameModule
{
    public GameModule(IRecorderCommandClient commandClient)
    {
        CommandSender = new GameCommandSender(commandClient);
    }

    public GameCommandSender CommandSender { get; }
}
