// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Training.ValueObjects;

public record TrainingVariant(string Id, string Text, double Weight);
