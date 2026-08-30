// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Messages.Commands;

public interface ICommand
{
    Guid CommandId { get; }
}
