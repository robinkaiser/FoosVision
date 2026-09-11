// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Setup.Entities;

namespace FoosVision.UseCases.Setup.Ports;

public interface ISetupSessionStore
{
    bool HasActive { get; }

    Option<SetupSession> LoadActive();

    void SaveActive(SetupSession session);

    void Clear();
}
