// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Runtime;
using Android.Util;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using FoosVision.Vision.Benchmarks.Android.Benchmarks;

namespace FoosVision.Vision.Benchmarks.Android;

[Instrumentation(Name = "com.dotnet.MainInstrumentation")]
public class MainInstrumentation : Instrumentation
{
    private const string _Tag = "DOTNET";

    protected MainInstrumentation(IntPtr handle, JniHandleOwnership transfer)
           : base(handle, transfer)
    {
    }

    public override void OnCreate(Bundle? arguments)
    {
        base.OnCreate(arguments);

        Start();
    }

    public async override void OnStart()
    {
        base.OnStart();

        Log.Debug(_Tag, $"OnStart - Benchmark started.");
        var success = await Task.Factory.StartNew(Run);
        Log.Debug(_Tag, $"OnStart - Benchmark complete, success: {success}");
        Finish(success ? Result.Ok : Result.Canceled, new Bundle());
    }

    private static bool Run()
    {
        bool success = false;

        try
        {
            var config = ManualConfig.CreateMinimumViable()
                .AddJob(Job.Default.WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(10), logOutput: true)))
                .AddDiagnoser(MemoryDiagnoser.Default)
                .WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest, MethodOrderPolicy.Alphabetical));

            var types = new[]
            {
                typeof(FieldDetectorBenchmarks),
                typeof(TableSceneCalibratorBenchmarks),
                typeof(TableSceneUpdaterBenchmarks),
                typeof(VisionContextBenchmarks),
                typeof(BallFinderBenchmarks),
                typeof(BlobFinderBenchmarks),
            };

            foreach (var type in types)
            {
                BenchmarkRunner.Run(type, config.WithOptions(ConfigOptions.DisableLogFile));
            }

            success = true;
        }
        catch (Exception ex)
        {
            Log.Error(_Tag, $"Error: {ex}");
        }

        return success;
    }
}
