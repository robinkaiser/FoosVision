// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class ColoredPlayerMaskDetectorTests
{
    private const int _Width = 32;
    private const int _Height = 32;

    [Fact]
    public void DetectBar_Finds_Matching_Player_Color_Rectangles()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 2, 6, 7, 3, 20, 160, 40);
        SetRectangle(image, 2, 20, 7, 3, 20, 160, 40);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks result = detector.DetectBar(image, bar, CreateCalibration());

        Assert.Equal(2, result.Rectangles.Count);
        Assert.Equal(new Rectangle(1, 0, 9, 15), result.Rectangles[0]);
        Assert.Equal(new Rectangle(1, 14, 9, 15), result.Rectangles[1]);
    }

    [Fact]
    public void DetectBar_Uses_Rod_Team_Color_Model()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 2, 6, 7, 3, 20, 40, 220);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks result = detector.DetectBar(image, bar, CreateCalibration());

        Assert.Empty(result.Rectangles);
    }

    [Fact]
    public void DetectBar_Applies_Rod_Color_Model_Radius_Scale()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 2, 6, 7, 3, 20, 160, 40);
        ColorFeature green = ColorFeature.FromRgb(20, 160, 40);
        ChromaticColorModel model = new(green.Cb + 6, green.Cr, 10, 10, 10);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector fullRadiusDetector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            RodColorModelRadiusScale: 1,
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));
        ColoredPlayerMaskDetector reducedRadiusDetector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            RodColorModelRadiusScale: 0.5,
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks fullRadiusResult = fullRadiusDetector.DetectBar(image, bar, CreateCalibration(model));
        RodColoredPlayerMasks reducedRadiusResult = reducedRadiusDetector.DetectBar(image, bar, CreateCalibration(model));

        Assert.Single(fullRadiusResult.Rectangles);
        Assert.Empty(reducedRadiusResult.Rectangles);
    }

    [Fact]
    public void DetectBar_Merges_Short_Gaps_Along_Rod()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 2, 6, 7, 2, 20, 160, 40);
        SetRectangle(image, 2, 10, 7, 2, 20, 160, 40);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 1,
            AlongRodAllowedMisses: 2,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks result = detector.DetectBar(image, bar, CreateCalibration());

        Assert.Single(result.Rectangles);
        Assert.Equal(new Rectangle(1, 0, 9, 18), result.Rectangles[0]);
    }

    [Fact]
    public void DetectBar_Expands_Cross_Rod_To_Matching_Color_Runs()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 1, 2, 5, 8, 20, 160, 40);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 0,
            AlongRodExpansionMargin: 0,
            AlongRodAllowedMisses: 1,
            CrossRodAllowedEmptyScans: 1,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks result = detector.DetectBar(image, bar, CreateCalibration());

        Assert.Single(result.Rectangles);
        Assert.Equal(new Rectangle(1, 2, 5, 8), result.Rectangles[0]);
    }

    [Fact]
    public void DetectBar_Expands_Along_Rod_When_Cross_Rod_Matches_Drift()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 4, 5, 3, 1, 20, 160, 40);
        SetRectangle(image, 3, 4, 1, 1, 20, 160, 40);
        SetRectangle(image, 2, 3, 1, 1, 20, 160, 40);
        SetRectangle(image, 1, 2, 1, 1, 20, 160, 40);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector fixedWindowDetector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 0,
            AlongRodExpansionMargin: 1,
            AlongRodAdaptiveExpansionMaxExtra: 0,
            AlongRodAllowedMisses: 0,
            CrossRodAllowedEmptyScans: 0,
            MinimumCrossSectionMatches: 2));
        ColoredPlayerMaskDetector adaptiveWindowDetector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 0,
            AlongRodExpansionMargin: 1,
            AlongRodAdaptiveExpansionStep: 2,
            AlongRodAdaptiveExpansionEdgeDistance: 0,
            AlongRodAdaptiveExpansionMaxExtra: 4,
            AlongRodAllowedMisses: 0,
            CrossRodAllowedEmptyScans: 0,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks fixedWindowResult = fixedWindowDetector.DetectBar(image, bar, CreateCalibration());
        RodColoredPlayerMasks adaptiveWindowResult = adaptiveWindowDetector.DetectBar(image, bar, CreateCalibration());

        Assert.Single(fixedWindowResult.Rectangles);
        Assert.Single(adaptiveWindowResult.Rectangles);
        Assert.Equal(new Rectangle(3, 4, 4, 3), fixedWindowResult.Rectangles[0]);
        Assert.Equal(new Rectangle(1, 2, 6, 5), adaptiveWindowResult.Rectangles[0]);
    }

    [Fact]
    public void DetectBar_Applies_Expansion_Color_Model_Radius_Scale()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 3, 6, 5, 3, 20, 160, 40);
        SetRectangle(image, 2, 6, 1, 3, 30, 150, 40);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector reducedExpansionDetector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            RodColorModelRadiusScale: 0.5,
            ExpansionColorModelRadiusScale: 0.5,
            MaskMargin: 0,
            AlongRodExpansionMargin: 0,
            AlongRodAllowedMisses: 1,
            CrossRodAllowedEmptyScans: 0,
            MinimumCrossSectionMatches: 2));
        ColoredPlayerMaskDetector expandedRadiusDetector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            RodColorModelRadiusScale: 0.5,
            ExpansionColorModelRadiusScale: 1.5,
            MaskMargin: 0,
            AlongRodExpansionMargin: 0,
            AlongRodAllowedMisses: 1,
            CrossRodAllowedEmptyScans: 0,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks reducedExpansionResult = reducedExpansionDetector.DetectBar(image, bar, CreateCalibration());
        RodColoredPlayerMasks expandedRadiusResult = expandedRadiusDetector.DetectBar(image, bar, CreateCalibration());

        Assert.Single(reducedExpansionResult.Rectangles);
        Assert.Single(expandedRadiusResult.Rectangles);
        Assert.Equal(new Rectangle(3, 6, 5, 3), reducedExpansionResult.Rectangles[0]);
        Assert.Equal(new Rectangle(2, 6, 6, 3), expandedRadiusResult.Rectangles[0]);
    }

    [Fact]
    public void DetectBar_Does_Not_Start_From_Color_Outside_Rod()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 0, 6, 2, 3, 20, 160, 40);
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));

        RodColoredPlayerMasks result = detector.DetectBar(image, bar, CreateCalibration());

        Assert.Empty(result.Rectangles);
    }

    [Fact]
    public void DetectBarRectangles_Fills_Caller_Output_Buffer()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 2, 6, 7, 3, 20, 160, 40);
        Rectangle[] rectangles = new Rectangle[4];
        Bar bar = CreateVerticalBar(BarType.A2);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));

        int count = detector.DetectBarRectangles(
            image,
            bar,
            CreateCalibration(),
            1,
            rectangles);

        Assert.Equal(1, count);
        Assert.Equal(default, rectangles[0]);
        Assert.Equal(new Rectangle(1, 0, 9, 15), rectangles[1]);
    }

    [Fact]
    public void DetectRectangles_Fills_Caller_Field_Output_Buffers()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 2, 6, 7, 3, 20, 160, 40);
        Rectangle[] rectangles = new Rectangle[8];
        RodColoredPlayerMaskRange[] rodRanges = new RodColoredPlayerMaskRange[8];
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 1,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));
        PlayingField field = CreatePlayingField(
            [],
            new(
                new Point(0, 0),
                new Point(_Width - 1, 0),
                new Point(0, _Height - 1),
                new Point(_Width - 1, _Height - 1)),
            CreateVerticalBar(BarType.A1, 20),
            CreateVerticalBar(BarType.A2),
            CreateVerticalBar(BarType.B3, 20),
            CreateVerticalBar(BarType.A5, 20),
            CreateVerticalBar(BarType.B5, 20),
            CreateVerticalBar(BarType.A3, 20),
            CreateVerticalBar(BarType.B2, 20),
            CreateVerticalBar(BarType.B1, 20));

        int count = detector.DetectRectangles(
            image,
            field,
            CreateCalibration(),
            rectangles,
            rodRanges);

        Assert.Equal(1, count);
        Assert.Equal(new RodColoredPlayerMaskRange(BarType.A1, 0, 0), rodRanges[0]);
        Assert.Equal(new RodColoredPlayerMaskRange(BarType.A2, 0, 1), rodRanges[1]);
        Assert.Equal(new RodColoredPlayerMaskRange(BarType.B3, 1, 0), rodRanges[2]);
        Assert.Equal(new Rectangle(1, 0, 9, 15), rectangles[0]);
    }

    [Fact]
    public void Detect_Field_Ignores_Occlusions()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 3, 2, 5, 8, 20, 160, 40);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 0,
            AlongRodAllowedMisses: 2,
            MinimumCrossSectionMatches: 2));
        PlayingField field = CreatePlayingField(
            [
                new(
                    new Point(0, 5),
                    new Point(_Width - 1, 5),
                    new Point(0, 5),
                    new Point(_Width - 1, 5)),
            ]);

        ColoredPlayerMaskDetection result = detector.Detect(image, field, CreateCalibration());

        Assert.Single(result.Rods[1].Rectangles);
        Assert.Equal(new Rectangle(3, 0, 5, 15), result.Rods[1].Rectangles[0]);
    }

    [Fact]
    public void Detect_Field_Clips_Rectangles_To_Field_Bounds()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 3, 2, 5, 8, 20, 160, 40);
        ColoredPlayerMaskDetector detector = new(_Width, _Height, new ColoredPlayerMaskDetectionOptions(
            MaskMargin: 0,
            AlongRodAllowedMisses: 1,
            MinimumCrossSectionMatches: 2));
        PlayingField field = CreatePlayingField(
            [],
            new(
                new Point(4, 3),
                new Point(6, 3),
                new Point(4, 8),
                new Point(6, 8)));

        ColoredPlayerMaskDetection result = detector.Detect(image, field, CreateCalibration());

        Assert.Single(result.Rods[1].Rectangles);
        Assert.Equal(new Rectangle(4, 3, 3, 6), result.Rods[1].Rectangles[0]);
    }

    private static ColoredPlayerColorCalibration CreateCalibration(ChromaticColorModel? teamAModel = null)
    {
        ColorFeature green = ColorFeature.FromRgb(20, 160, 40);
        ColorFeature blue = ColorFeature.FromRgb(20, 40, 220);

        return new(
            new TeamColorCalibration(Team.A, 5, 10, teamAModel ?? new(green.Cb, green.Cr, 10, 10, 10)),
            new TeamColorCalibration(Team.B, 5, 10, new(blue.Cb, blue.Cr, 10, 10, 10)));
    }

    private static byte[] CreateImage()
    {
        byte[] image = new byte[_Width * _Height * 4];

        for (int y = 0; y < _Height; y++)
        {
            for (int x = 0; x < _Width; x++)
            {
                SetPixel(image, x, y, 80, 80, 80);
            }
        }

        return image;
    }

    private static void SetRectangle(byte[] image, int x0, int y0, int width, int height, byte r, byte g, byte b)
    {
        for (int y = y0; y < y0 + height; y++)
        {
            for (int x = x0; x < x0 + width; x++)
            {
                SetPixel(image, x, y, r, g, b);
            }
        }
    }

    private static void SetPixel(byte[] image, int x, int y, byte r, byte g, byte b)
    {
        int offset = ((y * _Width) + x) * 4;

        image[offset + 0] = r;
        image[offset + 1] = g;
        image[offset + 2] = b;
        image[offset + 3] = byte.MaxValue;
    }

    private static PlayingField CreatePlayingField(IReadOnlyList<Trapezium> occlusions)
        => CreatePlayingField(
            occlusions,
            new(
                new Point(0, 0),
                new Point(_Width - 1, 0),
                new Point(0, _Height - 1),
                new Point(_Width - 1, _Height - 1)));

    private static PlayingField CreatePlayingField(IReadOnlyList<Trapezium> occlusions, Trapezium boundary)
        => CreatePlayingField(
            occlusions,
            boundary,
            CreateVerticalBar(BarType.A1),
            CreateVerticalBar(BarType.A2),
            CreateVerticalBar(BarType.B3),
            CreateVerticalBar(BarType.A5),
            CreateVerticalBar(BarType.B5),
            CreateVerticalBar(BarType.A3),
            CreateVerticalBar(BarType.B2),
            CreateVerticalBar(BarType.B1));

    private static PlayingField CreatePlayingField(
        IReadOnlyList<Trapezium> occlusions,
        Trapezium boundary,
        Bar a1,
        Bar a2,
        Bar b3,
        Bar a5,
        Bar b5,
        Bar a3,
        Bar b2,
        Bar b1)
        => new(
            boundary,
            new(
                a1,
                a2,
                b3,
                a5,
                b5,
                a3,
                b2,
                b1),
            occlusions);

    private static Bar CreateVerticalBar(BarType type)
        => CreateVerticalBar(type, 5);

    private static Bar CreateVerticalBar(BarType type, int centerX)
        => new(
            type,
            new Line(new Point(centerX - 2, 0), new Point(centerX - 2, _Height - 1)),
            new Line(new Point(centerX, 0), new Point(centerX, _Height - 1)),
            new Line(new Point(centerX + 2, 0), new Point(centerX + 2, _Height - 1)));
}
