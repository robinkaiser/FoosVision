// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.Views;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Android.Decoding;
using FoosVision.Media.Core.EncodedVideo;

namespace MediaCodecRgbaDecodeProbe;

[global::Android.App.Activity(
    Label = "RGBA Decode Probe",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Density)]
public class MainActivity : global::Android.App.Activity
{
    private const string _AssetName = "H.264.mp4";
    private const int _DefaultMaxSampleSize = 4 * 1024 * 1024;

    private TextView? _StatusText;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _StatusText = new TextView(this)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent),
            TextSize = 16,
            Gravity = GravityFlags.Center,
        };
        _StatusText.SetTextColor(Color.White);
        _StatusText.SetBackgroundColor(Color.Black);
        _StatusText.SetPadding(32, 32, 32, 32);
        SetContentView(_StatusText);

        SetStatus("Waiting for decode probe...");
        _ = Task.Run(RunProbeAsync);
    }

    private async Task RunProbeAsync()
    {
        try
        {
            string filePath = await CopyAssetToCacheAsync().ConfigureAwait(false);
            SetStatus($"Decoding '{_AssetName}'...");

            DecodeResult rgbaResult = DecodeRgbaFile(filePath);
            DecodeResult yuvResult = DecodeYuvFile(filePath);
            string message =
                FormatResult("RGBA8888", rgbaResult) +
                "\n\n" +
                FormatResult("YUV420", yuvResult);

            Debug.WriteLine(message);
            SetStatus(message);
        }
        catch (Java.IO.FileNotFoundException)
        {
            SetStatus(GetMissingAssetMessage());
        }
        catch (FileNotFoundException)
        {
            SetStatus(GetMissingAssetMessage());
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            SetStatus(ex.ToString());
        }
    }

    private async Task<string> CopyAssetToCacheAsync()
    {
        string rootPath = CacheDir?.AbsolutePath ?? FilesDir?.AbsolutePath ?? string.Empty;
        string cachedPath = System.IO.Path.Combine(rootPath, _AssetName);

        await using System.IO.Stream source = Assets!.Open(_AssetName);
        await using FileStream destination = File.Create(cachedPath);
        await source.CopyToAsync(destination).ConfigureAwait(false);

        return cachedPath;
    }

    private static string GetMissingAssetMessage()
        => $"Missing asset '{_AssetName}'. Restore FoosVision.Integration/FileCapture/{_AssetName} before running this probe.";

    private static DecodeResult DecodeRgbaFile(string filePath)
    {
        using MediaExtractor extractor = new();
        extractor.SetDataSource(filePath);

        int trackIndex = SelectVideoTrack(extractor);
        extractor.SelectTrack(trackIndex);

        MediaFormat format = extractor.GetTrackFormat(trackIndex);
        string mimeType = format.GetString(MediaFormat.KeyMime) ?? string.Empty;
        CodecType codec = GetCodecType(mimeType);
        int width = format.GetInteger(MediaFormat.KeyWidth);
        int height = format.GetInteger(MediaFormat.KeyHeight);

        using AndroidVideoDecoder decoder = new();
        decoder.Configure(new AndroidVideoDecoderOptions(codec, width, height));

        int maxSampleSize = TryGetInteger(format, MediaFormat.KeyMaxInputSize) ?? _DefaultMaxSampleSize;
        Java.Nio.ByteBuffer sampleBuffer = Java.Nio.ByteBuffer.AllocateDirect(maxSampleSize);
        byte[] annexBBuffer = new byte[maxSampleSize + 4096];

        long startTime = Stopwatch.GetTimestamp();
        PushCodecSpecificData(format, decoder, annexBBuffer);

        int sampleCount = 0;
        int decodedFrameCount = 0;

        while (true)
        {
            sampleBuffer.Clear();
            int sampleSize = extractor.ReadSampleData(sampleBuffer, 0);
            if (sampleSize < 0)
            {
                break;
            }

            if (sampleSize > annexBBuffer.Length)
            {
                annexBBuffer = new byte[sampleSize + 4096];
            }

            long timestampNs = extractor.SampleTime * 1000L;
            bool isKeyFrame = (extractor.SampleFlags & MediaExtractorSampleFlags.Sync) == MediaExtractorSampleFlags.Sync;

            MediaCodec.BufferInfo info = new();
            info.Set(0, sampleSize, extractor.SampleTime, MediaCodecBufferFlags.None);
            int annexBSize = AnnexBConverter.WriteAccessUnit(sampleBuffer, info, annexBBuffer, 0);
            if (annexBSize > 0)
            {
                decoder.PushAccessUnit(annexBBuffer.AsSpan(0, annexBSize), timestampNs, isKeyFrame);
                decodedFrameCount += DrainFrames(decoder);
            }

            sampleCount++;
            extractor.Advance();
        }

        decoder.Flush();
        decodedFrameCount += DrainRemainingFrames(decoder);
        TimeSpan elapsedTime = Stopwatch.GetElapsedTime(startTime);

        return new DecodeResult(codec, width, height, sampleCount, decodedFrameCount, elapsedTime);
    }

    private static DecodeResult DecodeYuvFile(string filePath)
    {
        using MediaExtractor extractor = new();
        extractor.SetDataSource(filePath);

        int trackIndex = SelectVideoTrack(extractor);
        extractor.SelectTrack(trackIndex);

        MediaFormat format = extractor.GetTrackFormat(trackIndex);
        string mimeType = format.GetString(MediaFormat.KeyMime) ?? string.Empty;
        CodecType codec = GetCodecType(mimeType);
        int width = format.GetInteger(MediaFormat.KeyWidth);
        int height = format.GetInteger(MediaFormat.KeyHeight);

        using AndroidYuvVideoDecoder decoder = new();
        decoder.Configure(new AndroidVideoDecoderOptions(codec, width, height));

        int maxSampleSize = TryGetInteger(format, MediaFormat.KeyMaxInputSize) ?? _DefaultMaxSampleSize;
        Java.Nio.ByteBuffer sampleBuffer = Java.Nio.ByteBuffer.AllocateDirect(maxSampleSize);
        byte[] annexBBuffer = new byte[maxSampleSize + 4096];

        long startTime = Stopwatch.GetTimestamp();
        PushCodecSpecificData(format, decoder, annexBBuffer);

        int sampleCount = 0;
        int decodedFrameCount = 0;

        while (true)
        {
            sampleBuffer.Clear();
            int sampleSize = extractor.ReadSampleData(sampleBuffer, 0);
            if (sampleSize < 0)
            {
                break;
            }

            if (sampleSize > annexBBuffer.Length)
            {
                annexBBuffer = new byte[sampleSize + 4096];
            }

            long timestampNs = extractor.SampleTime * 1000L;
            bool isKeyFrame = (extractor.SampleFlags & MediaExtractorSampleFlags.Sync) == MediaExtractorSampleFlags.Sync;

            MediaCodec.BufferInfo info = new();
            info.Set(0, sampleSize, extractor.SampleTime, MediaCodecBufferFlags.None);
            int annexBSize = AnnexBConverter.WriteAccessUnit(sampleBuffer, info, annexBBuffer, 0);
            if (annexBSize > 0)
            {
                decoder.PushAccessUnit(annexBBuffer.AsSpan(0, annexBSize), timestampNs, isKeyFrame);
                decodedFrameCount += DrainFrames(decoder);
            }

            sampleCount++;
            extractor.Advance();
        }

        decoder.Flush();
        decodedFrameCount += DrainRemainingFrames(decoder);
        TimeSpan elapsedTime = Stopwatch.GetElapsedTime(startTime);

        return new DecodeResult(codec, width, height, sampleCount, decodedFrameCount, elapsedTime);
    }

    private static void PushCodecSpecificData(MediaFormat format, AndroidVideoDecoder decoder, byte[] annexBBuffer)
    {
        for (int i = 0; i < 8; i++)
        {
            Java.Nio.ByteBuffer? csd = TryGetByteBuffer(format, $"csd-{i}");
            if (csd == null)
            {
                continue;
            }

            int written = AnnexBConverter.WriteCsd(csd, annexBBuffer, 0);
            if (written > 0)
            {
                decoder.PushAccessUnit(annexBBuffer.AsSpan(0, written), timeNs: 0, isKeyFrame: false, queueDecodedFrames: false);
            }
        }
    }

    private static void PushCodecSpecificData(MediaFormat format, AndroidYuvVideoDecoder decoder, byte[] annexBBuffer)
    {
        for (int i = 0; i < 8; i++)
        {
            Java.Nio.ByteBuffer? csd = TryGetByteBuffer(format, $"csd-{i}");
            if (csd == null)
            {
                continue;
            }

            int written = AnnexBConverter.WriteCsd(csd, annexBBuffer, 0);
            if (written > 0)
            {
                decoder.PushAccessUnit(annexBBuffer.AsSpan(0, written), timeNs: 0, isKeyFrame: false, queueDecodedFrames: false);
            }
        }
    }

    private static int DrainFrames(AndroidVideoDecoder decoder)
    {
        int count = 0;

        while (decoder.TryDequeueFrame(out AndroidDecodedFrame? frame))
        {
            using (frame)
            {
                count++;
            }
        }

        return count;
    }

    private static int DrainFrames(AndroidYuvVideoDecoder decoder)
    {
        int count = 0;

        while (decoder.TryDequeueFrame(out AndroidYuvDecodedFrame? frame))
        {
            frame.Release();
            count++;
        }

        return count;
    }

    private static int DrainRemainingFrames(AndroidVideoDecoder decoder)
    {
        int count = 0;
        long quietStartTime = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(quietStartTime).TotalMilliseconds < 500)
        {
            int drained = DrainFrames(decoder);
            if (drained > 0)
            {
                count += drained;
                quietStartTime = Stopwatch.GetTimestamp();
                continue;
            }

            Thread.Sleep(10);
        }

        return count;
    }

    private static int DrainRemainingFrames(AndroidYuvVideoDecoder decoder)
    {
        int count = 0;
        long quietStartTime = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(quietStartTime).TotalMilliseconds < 500)
        {
            int drained = DrainFrames(decoder);
            if (drained > 0)
            {
                count += drained;
                quietStartTime = Stopwatch.GetTimestamp();
                continue;
            }

            Thread.Sleep(10);
        }

        return count;
    }

    private static string FormatResult(string label, DecodeResult result)
    {
        string message =
            $"{label}: {result.DecodedFrameCount} frames from {result.SampleCount} access units\n" +
            $"Video: {result.Codec}, {result.Width}x{result.Height}\n" +
            $"Elapsed: {result.Elapsed.TotalSeconds:0.000}s\n" +
            $"Throughput: {result.FramesPerSecond:0.0} fps";

        return message;
    }

    private static int SelectVideoTrack(MediaExtractor extractor)
    {
        for (int i = 0; i < extractor.TrackCount; i++)
        {
            MediaFormat format = extractor.GetTrackFormat(i);
            string? mimeType = format.GetString(MediaFormat.KeyMime);
            if (mimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
            {
                return i;
            }
        }

        throw new InvalidOperationException("No video track found.");
    }

    private static CodecType GetCodecType(string mimeType)
        => mimeType switch
        {
            MediaFormat.MimetypeVideoAvc => CodecType.H264,
            MediaFormat.MimetypeVideoHevc => CodecType.H265,
            _ => throw new NotSupportedException($"Video MIME type '{mimeType}' is not supported."),
        };

    private static int? TryGetInteger(MediaFormat format, string key)
    {
        try
        {
            return format.ContainsKey(key) ? format.GetInteger(key) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Java.Nio.ByteBuffer? TryGetByteBuffer(MediaFormat format, string key)
    {
        try
        {
            return format.ContainsKey(key) ? format.GetByteBuffer(key) : null;
        }
        catch
        {
            return null;
        }
    }

    private void SetStatus(string text)
    {
        RunOnUiThread(() =>
        {
            _StatusText?.Text = text;
        });
    }

    private readonly record struct DecodeResult(
        CodecType Codec,
        int Width,
        int Height,
        int SampleCount,
        int DecodedFrameCount,
        TimeSpan Elapsed)
    {
        public double FramesPerSecond => DecodedFrameCount / Math.Max(Elapsed.TotalSeconds, 0.001);
    }
}
