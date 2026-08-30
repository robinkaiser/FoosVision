// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Globalization;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.FieldDetection;

public class FieldDetectionTests
{
    private const string _OutputDirectory = "FieldDetectionTest";
    private static readonly string _FilesDirectory = @"D:\Projects\FoosVision.Validation\FieldDetector";
    private readonly ITestOutputHelper _Output;

    public FieldDetectionTests(ITestOutputHelper output)
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
                data.Add(new TestCase(_FilesDirectory, _FilesDirectory));
                return data;
            }

            var files = Directory.EnumerateFiles(_FilesDirectory, "*.png", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                data.Add(new TestCase(file, Path.ChangeExtension(file, ".json")));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TestCases))]
    public void Test(TestCase testCase)
    {
        Assert.SkipWhen(LocalValidationData.ShouldSkipDirectory(_FilesDirectory), $"Local validation directory '{_FilesDirectory}' is not available in public test runs.");

        (byte[] buffer, int width, int height) = ValidationImageUtils.ReadRGBA8888ImageFromFile(testCase.PngPath);

        FieldDetector detector = new(width, height);

        var field = detector.Detect(buffer);
        Assert.True(field.HasValue);

        if (!File.Exists(testCase.JsonPath))
        {
            GroundTruthParser.WriteInitial(testCase.JsonPath, field.Value);
            _Output.WriteLine($"Created initial ground truth file: {testCase.JsonPath}");
            return;
        }

        var truth = GroundTruthParser.Read(testCase.JsonPath);
        WriteRoughBarCandidateDiagnostics(testCase, detector, truth);

        var bounds = field.Value.Boundary;
        var bars = field.Value.Bars;

        List<LineData> lines = [];

        foreach (var bar in truth.Bars)
        {
            lines.Add(new(bar.Left.P0X, bar.Left.P0Y, bar.Left.P1X, bar.Left.P1Y, ColorType.Blue, StyleType.Solid));
            lines.Add(new(bar.Right.P0X, bar.Right.P0Y, bar.Right.P1X, bar.Right.P1Y, ColorType.Blue, StyleType.Solid));
        }

        var barA1 = truth.Bars.First(b => b.Type == BarType.A1);
        var barB1 = truth.Bars.First(b => b.Type == BarType.B1);

        lines.Add(new(barA1.Right.P0X, barA1.Right.P0Y, barB1.Left.P0X, barB1.Left.P0Y, ColorType.Blue, StyleType.Solid));
        lines.Add(new(barA1.Right.P1X, barA1.Right.P1Y, barB1.Left.P1X, barB1.Left.P1Y, ColorType.Blue, StyleType.Solid));

        foreach (var occlusion in truth.Occlusions)
        {
            AddTrapezium(lines, occlusion, ColorType.Blue, StyleType.Solid);
        }

        foreach (var bar in bars.All)
        {
            lines.Add(new(bar.Left.P0.X, bar.Left.P0.Y, bar.Left.P1.X, bar.Left.P1.Y, ColorType.Red, StyleType.Dot));
            lines.Add(new(bar.Right.P0.X, bar.Right.P0.Y, bar.Right.P1.X, bar.Right.P1.Y, ColorType.Red, StyleType.Dot));
        }

        lines.Add(new(bounds.UpperLeft.X, bounds.UpperLeft.Y, bounds.UpperRight.X, bounds.UpperRight.Y, ColorType.Red, StyleType.Dot));
        lines.Add(new(bounds.LowerLeft.X, bounds.LowerLeft.Y, bounds.LowerRight.X, bounds.LowerRight.Y, ColorType.Red, StyleType.Dot));

        foreach (var occlusion in field.Value.Occlusions)
        {
            AddTrapezium(lines, occlusion, ColorType.Red, StyleType.Dot);
        }

        WriteDiagnosticImage(testCase, new(buffer, width, height), lines);

        Assert.Equal(bars[BarType.A1].Right.P0, bounds.UpperLeft);
        Assert.Equal(bars[BarType.B1].Left.P0, bounds.UpperRight);
        Assert.Equal(bars[BarType.A1].Right.P1, bounds.LowerLeft);
        Assert.Equal(bars[BarType.B1].Left.P1, bounds.LowerRight);

        const double maxBarPointDiff = 6.0;

        for (int r = 0; r < bars.All.Count(); r++)
        {
            var trueBar = truth.Bars.ElementAt(r);
            var bar = bars.All.ElementAt(r);

            Assert.Equal(trueBar.Left.P0X, bar.Left.P0.X, maxBarPointDiff);
            Assert.Equal(trueBar.Left.P0Y, bar.Left.P0.Y, maxBarPointDiff);
            Assert.Equal(trueBar.Left.P1X, bar.Left.P1.X, maxBarPointDiff);
            Assert.Equal(trueBar.Left.P1Y, bar.Left.P1.Y, maxBarPointDiff);

            Assert.Equal(trueBar.Right.P0X, bar.Right.P0.X, maxBarPointDiff);
            Assert.Equal(trueBar.Right.P0Y, bar.Right.P0.Y, maxBarPointDiff);
            Assert.Equal(trueBar.Right.P1X, bar.Right.P1.X, maxBarPointDiff);
            Assert.Equal(trueBar.Right.P1Y, bar.Right.P1.Y, maxBarPointDiff);
        }

        const double maxOcclusionPointDiff = 6.0;

        Assert.Equal(truth.Occlusions.Count(), field.Value.Occlusions.Count);

        for (int i = 0; i < field.Value.Occlusions.Count; i++)
        {
            var trueOcclusion = truth.Occlusions.ElementAt(i);
            var occlusion = field.Value.Occlusions[i];

            Assert.Equal(trueOcclusion.UpperLeft.X, occlusion.UpperLeft.X, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.UpperLeft.Y, occlusion.UpperLeft.Y, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.UpperRight.X, occlusion.UpperRight.X, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.UpperRight.Y, occlusion.UpperRight.Y, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.LowerRight.X, occlusion.LowerRight.X, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.LowerRight.Y, occlusion.LowerRight.Y, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.LowerLeft.X, occlusion.LowerLeft.X, maxOcclusionPointDiff);
            Assert.Equal(trueOcclusion.LowerLeft.Y, occlusion.LowerLeft.Y, maxOcclusionPointDiff);
        }
    }

    private void WriteRoughBarCandidateDiagnostics(TestCase testCase, FieldDetector detector, TableConfig truth)
    {
        var relativeName = Path.GetRelativePath(_FilesDirectory, testCase.PngPath);
        List<string> diagnosticLines = [relativeName];

        _Output.WriteLine(relativeName);

        foreach (var candidate in detector.LastRoughBarCandidates)
        {
            double x = (candidate.Line.P0.X + candidate.Line.P1.X) / 2.0;
            string selected = candidate.Selected ? "*" : " ";
            var nearestBar = truth.Bars
                .Select(bar => new
                {
                    bar.Type,
                    Distance = Math.Abs(GetGroundTruthBarCenterX(bar) - x),
                })
                .OrderBy(bar => bar.Distance)
                .First();

            string diagnosticLine = string.Format(
                CultureInfo.InvariantCulture,
                "{0} #{1:000} x0={2:0.0} x1={3:0.0} angle={4:0.0} acc={5} coverage={6:0.000} longest={7:0.000} bins={8}/{9} edgePixels={10}",
                selected,
                candidate.Index,
                candidate.Line.P0.X,
                candidate.Line.P1.X,
                candidate.Line.Angle,
                candidate.Line.Accumulator,
                candidate.CoverageScore.Coverage,
                candidate.CoverageScore.LongestRunCoverage,
                candidate.CoverageScore.SupportedBins,
                candidate.CoverageScore.BinCount,
                candidate.CoverageScore.EdgePixelCount);

            diagnosticLine = string.Format(
                CultureInfo.InvariantCulture,
                "{0} nearestTruth={1} truthDistance={2:0.0}",
                diagnosticLine,
                nearestBar.Type,
                nearestBar.Distance);

            diagnosticLines.Add(diagnosticLine);
            _Output.WriteLine(diagnosticLine);
        }

        var diagnosticFileName = GetOutputPath(testCase, ".rough-bars.txt");

        File.WriteAllLines(diagnosticFileName, diagnosticLines);
    }

    private static void WriteDiagnosticImage(TestCase testCase, Rgba8888ImageData imageData, IEnumerable<LineData> lines)
    {
        var diagnosticFileName = GetOutputPath(testCase, ".png");

        ValidationImageUtils.WriteRGBA8888ImageWithLinesToFile(imageData, lines, diagnosticFileName);
    }

    private static string GetOutputPath(TestCase testCase, string extension)
    {
        Directory.CreateDirectory(_OutputDirectory);

        var relativeName = Path.GetRelativePath(_FilesDirectory, testCase.PngPath);
        var fileName = Path.ChangeExtension(
            relativeName
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_'),
            extension);

        return Path.Combine(_OutputDirectory, fileName);
    }

    private static double GetGroundTruthBarCenterX(TableBar bar)
    {
        double leftCenterX = (bar.Left.P0X + bar.Left.P1X) / 2.0;
        double rightCenterX = (bar.Right.P0X + bar.Right.P1X) / 2.0;
        return (leftCenterX + rightCenterX) / 2.0;
    }

    private static void AddTrapezium(List<LineData> lines, TableOcclusion occlusion, ColorType color, StyleType style)
    {
        lines.Add(new(occlusion.UpperLeft.X, occlusion.UpperLeft.Y, occlusion.UpperRight.X, occlusion.UpperRight.Y, color, style));
        lines.Add(new(occlusion.UpperRight.X, occlusion.UpperRight.Y, occlusion.LowerRight.X, occlusion.LowerRight.Y, color, style));
        lines.Add(new(occlusion.LowerRight.X, occlusion.LowerRight.Y, occlusion.LowerLeft.X, occlusion.LowerLeft.Y, color, style));
        lines.Add(new(occlusion.LowerLeft.X, occlusion.LowerLeft.Y, occlusion.UpperLeft.X, occlusion.UpperLeft.Y, color, style));
    }

    private static void AddTrapezium(List<LineData> lines, Trapezium occlusion, ColorType color, StyleType style)
    {
        lines.Add(new(occlusion.UpperLeft.X, occlusion.UpperLeft.Y, occlusion.UpperRight.X, occlusion.UpperRight.Y, color, style));
        lines.Add(new(occlusion.UpperRight.X, occlusion.UpperRight.Y, occlusion.LowerRight.X, occlusion.LowerRight.Y, color, style));
        lines.Add(new(occlusion.LowerRight.X, occlusion.LowerRight.Y, occlusion.LowerLeft.X, occlusion.LowerLeft.Y, color, style));
        lines.Add(new(occlusion.LowerLeft.X, occlusion.LowerLeft.Y, occlusion.UpperLeft.X, occlusion.UpperLeft.Y, color, style));
    }
}
