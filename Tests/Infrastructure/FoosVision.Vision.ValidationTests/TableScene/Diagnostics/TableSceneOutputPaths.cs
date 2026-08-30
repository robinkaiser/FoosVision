// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public record TableSceneOutputPaths(
    string ColoredObjectIntervalSummary,
    string ColoredObjectIntervalOverlay,
    string EdgeScoreProfile,
    string ColorModelSummary,
    string ColorModelMosaic,
    string ObjectMaskOverlay,
    string BlackObjectIntervalSummary,
    string BlackSideBandHistogram,
    string BlackSideBandProfile,
    string BlackObjectIntervalOverlay,
    string BlackObjectMaskOverlay)
{
    private const string _OutputDirectory = "TableSceneTest";

    public static TableSceneOutputPaths Create(string relativeName)
    {
        Directory.CreateDirectory(_OutputDirectory);

        var fileName = Path.ChangeExtension(relativeName, null)
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');
        string outputBase = Path.Combine(_OutputDirectory, fileName);

        return new(
            $"{outputBase}_01ColoredObjectIntervals.txt",
            $"{outputBase}_02ColoredObjectIntervalsOverlay.png",
            $"{outputBase}_03EdgeScoreProfile.png",
            $"{outputBase}_04ColorModels.txt",
            $"{outputBase}_05ColorModelsMosaic.png",
            $"{outputBase}_06ObjectMaskOverlay.png",
            $"{outputBase}_07BlackObjectIntervals.txt",
            $"{outputBase}_09BlackSideBandHistogram.png",
            $"{outputBase}_08BlackSideBandProfile.png",
            $"{outputBase}_10BlackObjectIntervalsOverlay.png",
            $"{outputBase}_11BlackObjectMaskOverlay.png");
    }
}
