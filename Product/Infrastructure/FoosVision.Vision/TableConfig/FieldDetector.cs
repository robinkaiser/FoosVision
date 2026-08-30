// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Common.Processing;
using FoosVision.Vision.TableConfig.Processing;

namespace FoosVision.Vision.TableConfig;

public class FieldDetector
{
    private static readonly Source _Log = new("Vision.FieldDetector");

    private readonly int _Width;
    private readonly Rectangle _FullImageRectangle;
    private readonly CannyEdgeDetector _CannyEdgeDetector;
    private readonly BarFinder _BarFinder;
    private readonly BoundaryFinder _BoundaryFinder;
    private readonly LightOcclusionFinder _LightOcclusionFinder;
    private readonly byte[] _Y8ImageBuffer;
    private readonly byte[] _Y8CannyBuffer;

    public FieldDetector(int width, int height)
    {
        _Width = width;
        _FullImageRectangle = new(0, 0, width, height);
        _CannyEdgeDetector = new CannyEdgeDetector(width, height);
        _BarFinder = new(width, height);
        _BoundaryFinder = new(width, height);
        _LightOcclusionFinder = new(width, height);
        _Y8ImageBuffer = new byte[width * height];
        _Y8CannyBuffer = new byte[width * height];
    }

    public IReadOnlyList<RoughBarCandidateDiagnostic> LastRoughBarCandidates => _BarFinder.LastRoughBarCandidates;

    public Option<PlayingField> Detect(byte[] frameBufferRGBA8888)
    {
        try
        {
            ImageTransform.ConvertRGBA8888ToGray8(_Width, frameBufferRGBA8888, _Y8ImageBuffer, _FullImageRectangle);
            _CannyEdgeDetector.Process(_Y8ImageBuffer, _Y8CannyBuffer, _FullImageRectangle);

            IReadOnlyList<Bar> bars = _BarFinder.Find(_Y8CannyBuffer);

            if (bars.Count != Enum.GetValues<BarType>().Length)
            {
                _Log.Warning("Detect failed: expected {ExpectedBarCount} bars, found {ActualBarCount}.", Enum.GetValues<BarType>().Length, bars.Count);
                return Option<PlayingField>.None();
            }

            var boundaryResult = _BoundaryFinder.Find(_Y8CannyBuffer, _Y8ImageBuffer, bars);

            if (boundaryResult is null)
            {
                _Log.Warning("Detect failed: boundary not found.");
                return Option<PlayingField>.None();
            }

            var (upperHoughLine, lowerHoughLine) = boundaryResult.Value;
            Line upperLine = new(upperHoughLine.P0, upperHoughLine.P1);
            Line lowerLine = new(lowerHoughLine.P0, lowerHoughLine.P1);

            TableBars tableBars = TableBarsFactory.From(bars, upperLine, lowerLine);

            Trapezium boundary = new(
                tableBars.A1.Right.P0,
                tableBars.B1.Left.P0,
                tableBars.A1.Right.P1,
                tableBars.B1.Left.P1);

            IReadOnlyList<Trapezium> occlusions = [];

            if (_LightOcclusionFinder.TryGetSearchRectangle(boundary, tableBars, out Rectangle lightSearchRect))
            {
                occlusions = _LightOcclusionFinder.Find(frameBufferRGBA8888, _Y8CannyBuffer, boundary, lightSearchRect);
            }

            return new PlayingField(boundary, tableBars, occlusions);
        }
        catch (Exception ex)
        {
            _Log.Error("Detect failed with exception: {Exception}", ex.ToString());
            return Option<PlayingField>.None();
        }
    }
}
