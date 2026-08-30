// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Common.Types;
using FoosVision.Ports.Media;
using NSubstitute;

namespace FoosVision.Adapters.Common.UnitTests.Live;

public class FrameProcessingLoopTests
{
    private readonly IFrameFeed _FrameFeed;
    private readonly IFrameHandle _FrameHandle;
    private readonly IFrameProcessor _FrameProcessor;

    private readonly FrameProcessingLoop _Testee;

    public FrameProcessingLoopTests()
    {
        _FrameFeed = Substitute.For<IFrameFeed>();
        _FrameHandle = Substitute.For<IFrameHandle>();
        _FrameHandle.Meta.Returns(new Frame(42, 42_000));
        _FrameProcessor = Substitute.For<IFrameProcessor>();

        _Testee = new FrameProcessingLoop(_FrameFeed, _FrameProcessor);
    }

    [Fact]
    public void Releases_frame_immediately_if_no_active_session()
    {
        _FrameProcessor.ShouldProcess.Returns(false);

        _Testee.Start();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(_FrameHandle);

        _FrameHandle.Received(1).Release();
        _FrameProcessor.DidNotReceiveWithAnyArgs().Process(default!, TestContext.Current.CancellationToken);

        _Testee.Stop();
    }

    [Fact]
    public async Task Processes_and_releases_once()
    {
        _FrameProcessor.ShouldProcess.Returns(true);

        var processed = new ManualResetEventSlim();
        _FrameProcessor
            .Process(Arg.Any<IFrameHandle>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                processed.Set();
                return Task.CompletedTask;
            });

        _Testee.Start();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(_FrameHandle);

        Assert.True(processed.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

        await _FrameProcessor.Received().Process(
            Arg.Is<IFrameHandle>(f => f.Meta.Id == 42 && f.Meta.TimestampNs == 42_000),
            Arg.Any<CancellationToken>());

        _FrameHandle.Received(1).Release();

        _Testee.Stop();
    }

    [Fact]
    public async Task Releases_even_when_interactor_throws()
    {
        _FrameProcessor.ShouldProcess.Returns(true);

        var released = new ManualResetEventSlim();
        _FrameHandle
            .When(x => x.Release())
            .Do(_ => released.Set());

        _FrameProcessor
            .Process(Arg.Any<IFrameHandle>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        _Testee.Start();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(_FrameHandle);

        Assert.True(released.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

        await _FrameProcessor.Received().Process(
          Arg.Is<IFrameHandle>(f => f.Meta.Id == 42 && f.Meta.TimestampNs == 42_000),
          Arg.Any<CancellationToken>());

        _FrameHandle.Received(1).Release();

        _Testee.Stop();
    }

    [Fact]
    public async Task Continues_processing_after_interactor_throws()
    {
        _FrameProcessor.ShouldProcess.Returns(true);

        IFrameHandle frameHandle2 = Substitute.For<IFrameHandle>();
        frameHandle2.Meta.Returns(new Frame(43, 43_000));

        var secondProcessed = new ManualResetEventSlim();
        _FrameProcessor
            .Process(Arg.Any<IFrameHandle>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("boom"),
                _ =>
                {
                    secondProcessed.Set();
                    return Task.CompletedTask;
                });

        _Testee.Start();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(_FrameHandle);
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(frameHandle2);

        Assert.True(secondProcessed.Wait(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));

        await _FrameProcessor.Received().Process(
            Arg.Is<IFrameHandle>(f => f.Meta.Id == 43 && f.Meta.TimestampNs == 43_000),
            Arg.Any<CancellationToken>());

        _FrameHandle.Received(1).Release();
        frameHandle2.Received(1).Release();

        _Testee.Stop();
    }

    [Fact]
    public void Releases_all_leftovers()
    {
        _FrameProcessor.ShouldProcess.Returns(true);

        var processed = new ManualResetEventSlim();
        _FrameProcessor
            .Process(Arg.Any<IFrameHandle>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(100);
            });

        _Testee.Start();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(_FrameHandle);
        var frameHandle2 = Substitute.For<IFrameHandle>();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(frameHandle2);
        var frameHandle3 = Substitute.For<IFrameHandle>();
        _FrameFeed.FrameReady += Raise.Event<Action<IFrameHandle>>(frameHandle3);

        _Testee.Stop();

        _FrameHandle.Received(1).Release();
        frameHandle2.Received(1).Release();
        frameHandle3.Received(1).Release();
    }
}
