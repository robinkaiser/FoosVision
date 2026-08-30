// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Util;
using FoosVision.Common.Logging;

namespace FoosVision.Media.Android.CameraFeed;

internal static class AndroidCameraSelector
{
    private const float _MinimumEquivalentFocalLength = 20.0f;
    private const float _MaximumEquivalentFocalLength = 30.0f;
    private const float _FullFrameSensorDiagonal = 43.266615f;

    private static readonly Source _Log = new("AndroidCameraSelector");

    // Heuristic: pick the "main" rear camera that supports constrained high-speed video,
    // and discard ultra-wide / tele lenses by their 35mm-equivalent focal length.
    // NOTE: the rest of the pipeline currently assumes 1920x1080 RGBA frames,
    // so we require 1920x1080@120fps to be available.
    public static AndroidHighSpeedProfile? SelectDefaultHighSpeedProfile(CameraManager cameraManager)
    {
        try
        {
            foreach (var id in cameraManager.GetCameraIdList())
            {
                CameraCharacteristics ch = cameraManager.GetCameraCharacteristics(id);

                var facing = (LensFacing)(int)ch.Get(CameraCharacteristics.LensFacing)!;
                if (facing != LensFacing.Back) continue;

                var caps = ch.Get(CameraCharacteristics.RequestAvailableCapabilities)!
                             .ToArray<RequestAvailableCapabilities>();

                if (!caps.Contains(RequestAvailableCapabilities.ConstrainedHighSpeedVideo)) continue;

                float[] focals = ch.Get(CameraCharacteristics.LensInfoAvailableFocalLengths)!
                                   .ToArray<float>()!;

                if (focals.Length == 0) continue;

                float focal = focals[0]; // no optical zoom => single value

                var physicalSize = (SizeF?)ch.Get(CameraCharacteristics.SensorInfoPhysicalSize);
                if (physicalSize is null)
                {
                    _Log.Warning("Discarding high-speed back cam {0} because sensor physical size is unavailable.", id);
                    continue;
                }

                float equivalentFocal = CalculateFullFrameEquivalentFocalLength(focal, physicalSize);

                if (equivalentFocal < _MinimumEquivalentFocalLength || equivalentFocal > _MaximumEquivalentFocalLength)
                {
                    _Log.Information("Discarding high-speed back cam {0} with {1}mm physical focal length / {2}mm equivalent focal length.", id, focal, equivalentFocal);
                    continue;
                }

                var map = (StreamConfigurationMap)ch.Get(CameraCharacteristics.ScalerStreamConfigurationMap)!;

                // We require 1920x1080@120fps.
                var wantedSize = new Size(1920, 1080);

                foreach (var fpsRange in map.GetHighSpeedVideoFpsRangesFor(wantedSize)!)
                {
                    if ((int)fpsRange.Lower! != (int)fpsRange.Upper!)
                    {   // Not a fixed frame-rate range
                        continue;
                    }

                    if ((int)fpsRange.Lower! == 120)
                    {
                        _Log.Information("Selected high-speed back cam {0} with {1}mm physical focal length / {2}mm equivalent focal length.", id, focal, equivalentFocal);

                        return new AndroidHighSpeedProfile(
                            CameraId: id,
                            Width: wantedSize.Width,
                            Height: wantedSize.Height,
                            PreviewFps: 30,
                            SlowMoFps: 120);
                    }
                }

                _Log.Warning("Camera {0} supports high-speed but not 1920x1080@120fps.", id);
            }

            return null;
        }
        catch (Exception ex)
        {
            _Log.Error("SelectDefaultHighSpeedProfile failed: {0}", ex);
            return null;
        }
    }

    private static float CalculateFullFrameEquivalentFocalLength(float focalLength, SizeF sensorPhysicalSize)
    {
        float sensorDiagonal = MathF.Sqrt(
            (sensorPhysicalSize.Width * sensorPhysicalSize.Width) +
            (sensorPhysicalSize.Height * sensorPhysicalSize.Height));

        return focalLength * _FullFrameSensorDiagonal / sensorDiagonal;
    }
}
