// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Training.Services;

public interface ITrainingRandom
{
    /// <summary>Returns a finite value in [0, 1).</summary>
    double NextUnit();
}
