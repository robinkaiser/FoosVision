// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Training;
using FoosVision.Protocol.Connectivity.Abstractions;

namespace FoosVision.Viewer.Composition.Modules;

public class TrainingModule
{
    public TrainingModule(IRecorderCommandClient commandClient)
    {
        CommandSender = new TrainingCommandSender(commandClient);
    }

    public TrainingCommandSender CommandSender { get; }
}
