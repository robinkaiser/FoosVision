// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.TableScene;
using FoosVision.Vision.ValidationTests.TableScene.Diagnostics;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.TableScene;

public class TableSceneCalibrationTests
{
    private static readonly string _FilesDirectory = @"D:\Projects\FoosVision.Validation\Background";
    private readonly ITestOutputHelper _Output;

    public TableSceneCalibrationTests(ITestOutputHelper output)
    {
        _Output = output;
    }

    public static TheoryData<TestCase> TestCases
    {
        get
        {
            var data = new TheoryData<TestCase>();
            if (LocalValidationData.ShouldSkipDirectory(_FilesDirectory))
            {
                data.Add(new TestCase(_FilesDirectory));
                return data;
            }

            var files = Directory.EnumerateFiles(_FilesDirectory, "*.png", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                data.Add(new TestCase(file));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Calibration(TestCase testCase)
    {
        Assert.SkipWhen(LocalValidationData.ShouldSkipDirectory(_FilesDirectory), $"Local validation directory '{_FilesDirectory}' is not available in public test runs.");

        TableSceneTestContext context = LoadContext(testCase);
        _Output.WriteLine(context.RelativeName);

        FieldDetector fieldDetector = new(context.Image.Width, context.Image.Height);
        var fieldResult = fieldDetector.Detect(context.Image.Buffer);

        if (!fieldResult.HasValue)
        {
            TableSceneDiagnosticsWriter.WriteFieldDetectionFailed(context);
            return;
        }

        PlayingField field = fieldResult.Value;
        TableSceneCalibrator calibrator = new(context.Image.Width, context.Image.Height);
        var calibration = calibrator.Calibrate(context.Image.Buffer, field);
        Assert.Equal(8, calibration.ColoredObjectIntervals.Rods.Count);
        TableSceneDiagnosticsWriter.WriteColoredObjectIntervals(context, field, calibration.ColoredObjectIntervals);

        TableSceneDiagnosticsWriter.WriteColorModels(context, calibration.ColoredPlayerColorCalibration);

        Assert.Equal(8, calibration.ColoredPlayerMasks.Rods.Count);
        TableSceneDiagnosticsWriter.WriteObjectMask(context, calibration.ColoredPlayerMasks);

        Assert.Equal(8, calibration.BlackObjectIntervals.Rods.Count);
        TableSceneDiagnosticsWriter.WriteBlackObjectIntervals(context, field, calibration.BlackObjectIntervals);

        Assert.Equal(8, calibration.BlackObjectMasks.Rods.Count);
        TableSceneDiagnosticsWriter.WriteBlackObjectMasks(context, calibration.BlackObjectMasks);
    }

    private static TableSceneTestContext LoadContext(TestCase testCase)
    {
        string relativeName = Path.GetRelativePath(_FilesDirectory, testCase.PngPath);
        Rgba8888ImageData imageData = ValidationImageUtils.ReadRGBA8888ImageFromFile(testCase.PngPath);

        Assert.Equal(1920, imageData.Width);
        Assert.Equal(1080, imageData.Height);

        return new(
            testCase,
            relativeName,
            imageData,
            TableSceneOutputPaths.Create(relativeName));
    }
}
