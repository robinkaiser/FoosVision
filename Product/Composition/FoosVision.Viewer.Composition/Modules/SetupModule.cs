// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Setup;
using FoosVision.Protocol.Connectivity.Abstractions;

namespace FoosVision.Viewer.Composition.Modules;

public class SetupModule
{
    public SetupModule(IRecorderCommandClient commandClient)
    {
        CommandSender = new SetupCommandSender(commandClient);
    }

    public SetupCommandSender CommandSender { get; }
}
