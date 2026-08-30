// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Installation;
using FoosVision.Protocol.Connectivity.Abstractions;

namespace FoosVision.Viewer.Composition.Modules;

public class InstallationModule
{
    public InstallationModule(IRecorderCommandClient commandClient)
    {
        CommandSender = new InstallCommandSender(commandClient);
    }

    public InstallCommandSender CommandSender { get; }
}
