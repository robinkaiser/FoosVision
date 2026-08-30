// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.ValidationTests.TableScene.Diagnostics;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.TableScene;

public record TableSceneTestContext(
    TestCase TestCase,
    string RelativeName,
    Rgba8888ImageData Image,
    TableSceneOutputPaths OutputPaths);
