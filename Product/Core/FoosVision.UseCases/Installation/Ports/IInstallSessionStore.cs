// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Installation.Entities;

namespace FoosVision.UseCases.Installation.Ports;

public interface IInstallSessionStore
{
    bool HasActive { get; }

    Option<InstallSession> LoadActive();

    void SaveActive(InstallSession session);

    void Clear();
}
