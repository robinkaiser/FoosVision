// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public record ColoredPlayerMaskDetection(IReadOnlyList<RodColoredPlayerMasks> Rods);

public record RodColoredPlayerMasks(BarType BarType, IReadOnlyList<Rectangle> Rectangles);

public readonly record struct RodColoredPlayerMaskRange(BarType BarType, int StartIndex, int Count);
