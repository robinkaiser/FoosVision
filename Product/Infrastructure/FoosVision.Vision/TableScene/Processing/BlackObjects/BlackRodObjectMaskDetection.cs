// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Vision.TableScene.Processing.BlackObjects;

public record BlackRodObjectMaskDetection(IReadOnlyList<RodBlackObjectMasks> Rods);

public record RodBlackObjectMasks(BarType BarType, IReadOnlyList<Rectangle> Rectangles);

public readonly record struct RodBlackObjectMaskRange(BarType BarType, int StartIndex, int Count);
