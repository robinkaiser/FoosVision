// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

public enum CommandMessageType : byte
{
    StartInstall = 1,
    StopInstall = 2,
    StartGame = 10,
    StopGame = 11,
}
