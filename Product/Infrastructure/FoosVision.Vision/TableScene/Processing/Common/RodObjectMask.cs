// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Vision.TableScene.Processing.Common;

public record RodObjectMask(BarType BarType, IReadOnlyList<Rectangle> Rectangles);
