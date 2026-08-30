// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;

namespace FfmpegDecoderProbe;

internal static unsafe class Program
{
    private static int Main(string[] args)
    {
        ProbeOptions options;

        try
        {
            options = ProbeOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(ProbeOptions.HelpText);
            return 1;
        }

        try
        {
            FfmpegBootstrap.EnsureInitialized();

            Console.WriteLine($"FFmpeg root: {ffmpeg.RootPath}");
            Console.WriteLine($"Codec      : {options.Codec}");
            Console.WriteLine($"Size       : {options.Width}x{options.Height}");

            AVCodecID codecId = options.Codec switch
            {
                ProbeCodec.H264 => AVCodecID.AV_CODEC_ID_H264,
                ProbeCodec.H265 => AVCodecID.AV_CODEC_ID_HEVC,
                _ => throw new NotSupportedException($"Unsupported codec '{options.Codec}'."),
            };

            Console.WriteLine($"Calling avcodec_find_decoder({codecId})...");
            AVCodec* codec = ffmpeg.avcodec_find_decoder(codecId);

            if (codec == null)
            {
                throw new InvalidOperationException($"Decoder not found for codec '{options.Codec}'.");
            }

            Console.WriteLine("Calling avcodec_alloc_context3...");
            AVCodecContext* codecContext = ffmpeg.avcodec_alloc_context3(codec);

            if (codecContext == null)
            {
                throw new InvalidOperationException("Failed to allocate codec context.");
            }

            try
            {
                codecContext->width = options.Width;
                codecContext->height = options.Height;
                codecContext->thread_count = 0;

                Console.WriteLine("Calling avcodec_open2...");
                int result = ffmpeg.avcodec_open2(codecContext, codec, null);
                ThrowOnError(result, "Failed to open decoder.");
                Console.WriteLine("avcodec_open2 returned successfully.");
                return 0;
            }
            finally
            {
                ffmpeg.avcodec_free_context(&codecContext);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void ThrowOnError(int result, string message)
    {
        if (result >= 0)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[1024];
        fixed (byte* bufferPointer = buffer)
        {
            ffmpeg.av_strerror(result, bufferPointer, (ulong)buffer.Length);
        }

        int zeroIndex = buffer.IndexOf((byte)0);
        string detail = zeroIndex >= 0
            ? System.Text.Encoding.UTF8.GetString(buffer[..zeroIndex])
            : System.Text.Encoding.UTF8.GetString(buffer);
        throw new InvalidOperationException($"{message} FFmpeg error {result}: {detail}");
    }
}
