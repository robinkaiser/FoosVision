// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using System.Threading.Channels;
using FoosVision.Adapters.Recorder.Training;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.UseCases.Training;

namespace FoosVision.Adapters.Recorder.UnitTests.Training;

public class TrainingAnnouncementsTests
{
    [Theory]
    [InlineData(TrainingStrategyKind.SnakeShot, "Left ready", "Shoot!")]
    [InlineData(TrainingStrategyKind.PullShot, "Short ready", "Shoot!")]
    [InlineData(TrainingStrategyKind.TickTackPass, "Wall ready", "Shoot!")]
    [InlineData(TrainingStrategyKind.SnakeShot, "Snake ready", "Right")]
    [InlineData(TrainingStrategyKind.PullShot, "Pull ready", "Long")]
    [InlineData(TrainingStrategyKind.TickTackPass, "Pass ready", "Lane")]
    public async Task Request_speaks_core_text_for_both_modes_without_waiting_for_speech(
        TrainingStrategyKind strategy, string ready, string execution)
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt(strategy: strategy);
        testee.StartSession(attempt.SessionId);

        testee.Request(new(attempt, 1, TrainingAnnouncementKind.Ready, ready, null));
        SpeechCall first = await speech.NextCall();
        testee.Request(new(attempt, 2, TrainingAnnouncementKind.Shoot, execution, null));
        Assert.False(first.Completion.Task.IsCompleted);
        Assert.Single(speech.Calls);
        first.Completion.SetResult(SpeechOutputResult.Completed);

        SpeechCall second = await speech.NextCall();
        Assert.Equal(ready, first.Text);
        Assert.Equal(execution, second.Text);
    }

    [Fact]
    public void Request_deduplicates_and_rejects_older_ids_before_the_worker_runs()
    {
        FakeSpeech speech = new();
        ManualTaskScheduler scheduler = new();
        using TrainingAnnouncements testee = new(speech, scheduler);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 2, "original"));
        testee.Request(Request(attempt, 2, "duplicate"));
        testee.Request(Request(attempt, 1, "late"));
        scheduler.RunNext();

        Assert.Equal("original", Assert.Single(speech.Calls).Text);
    }

    [Fact]
    public async Task Request_keeps_only_the_newest_pending_announcement()
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Ready"));
        SpeechCall first = await speech.NextCall();
        testee.Request(Request(attempt, 2, "old pending"));
        testee.Request(Request(attempt, 3, "new pending"));
        first.Completion.SetResult(SpeechOutputResult.Completed);

        Assert.Equal("new pending", (await speech.NextCall()).Text);
        Assert.Equal(2, speech.Calls.Count);
    }

    [Fact]
    public async Task Invalidate_cancels_current_and_pending_but_preserves_the_new_aborted_output()
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Ready"));
        SpeechCall first = await speech.NextCall();
        testee.Request(Request(attempt, 2, "Shoot!"));
        testee.Invalidate(new(attempt, 2));
        testee.Request(Request(attempt, 2, "late duplicate"));
        testee.Request(new(attempt, 3, TrainingAnnouncementKind.Aborted, "Aborted", null));

        Assert.True(first.Token.IsCancellationRequested);
        Assert.Equal("Aborted", (await speech.NextCall()).Text);
        first.Completion.SetResult(SpeechOutputResult.Completed); // Late platform callback.
        Assert.Equal(2, speech.Calls.Count);
    }

    [Fact]
    public async Task New_attempt_cancels_old_aborted_output_and_ignores_old_attempt_requests()
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(new(attempt, 1, TrainingAnnouncementKind.Aborted, "Aborted", null));
        SpeechCall first = await speech.NextCall();
        testee.Request(Request(attempt with { AttemptId = 2 }, 2, "New ready"));
        testee.Request(Request(attempt, 3, "Old attempt"));

        Assert.True(first.Token.IsCancellationRequested);
        Assert.Equal("New ready", (await speech.NextCall()).Text);
    }

    [Fact]
    public void Invalidate_during_preparation_discards_late_output_without_retry()
    {
        FakeSpeech speech = new() { Preparation = new() };
        ManualTaskScheduler scheduler = new();
        using TrainingAnnouncements testee = new(speech, scheduler);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Old ready"));
        scheduler.RunNext();
        testee.Invalidate(new(attempt, 1));
        speech.Preparation.SetResult(SpeechOutputResult.Ready);
        testee.StopSession(attempt.SessionId);

        Assert.Empty(speech.Calls);
        Assert.Equal(1, speech.PrepareCount);
    }

    [Theory]
    [InlineData(SpeechOutputResult.LanguageUnavailable)]
    [InlineData(SpeechOutputResult.Unavailable)]
    [InlineData(SpeechOutputResult.Failed)]
    public void Preparation_failure_drops_requests_without_retry(SpeechOutputResult result)
    {
        FakeSpeech speech = new() { PreparationResult = result };
        ManualTaskScheduler scheduler = new();
        using TrainingAnnouncements testee = new(speech, scheduler);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Ready"));
        scheduler.RunNext();

        Assert.Empty(speech.Calls);
        Assert.Equal(1, speech.PrepareCount);
    }

    [Theory]
    [InlineData(SpeechOutputResult.Failed)]
    [InlineData(SpeechOutputResult.AudioFocusDenied)]
    [InlineData(SpeechOutputResult.LanguageUnavailable)]
    public async Task Speech_failure_does_not_retry_or_prevent_the_next_core_output(SpeechOutputResult result)
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Ready"));
        SpeechCall first = await speech.NextCall();
        testee.Request(Request(attempt, 2, "Shoot!"));
        first.Completion.SetResult(result);

        Assert.Equal("Shoot!", (await speech.NextCall()).Text);
        Assert.Equal(2, speech.Calls.Count);
    }

    [Fact]
    public async Task External_interruption_discards_pending_and_does_not_replay_old_outputs()
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Ready"));
        SpeechCall first = await speech.NextCall();
        testee.Request(Request(attempt, 2, "pending"));
        speech.Interrupt();
        Assert.True(first.Token.IsCancellationRequested);
        testee.Request(Request(attempt, 2, "late pending"));
        testee.Request(Request(attempt, 3, "Fresh output"));

        Assert.Equal("Fresh output", (await speech.NextCall()).Text);
        Assert.Equal(2, speech.Calls.Count);
    }

    [Fact]
    public void Background_requests_are_discarded_even_when_the_worker_runs_after_foreground_resume()
    {
        FakeSpeech speech = new();
        ManualTaskScheduler scheduler = new();
        using TrainingAnnouncements testee = new(speech, scheduler);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        speech.IsOutputAllowed = false;
        testee.Request(Request(attempt, 1, "Background ready"));
        speech.IsOutputAllowed = true;
        testee.Request(Request(attempt, 1, "Late duplicate"));
        scheduler.RunNext();

        Assert.Empty(speech.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_restart_discards_old_work_and_old_callbacks_cannot_stop_new_speech(bool explicitStop)
    {
        FakeSpeech speech = new();
        using TrainingAnnouncements testee = new(speech);
        TrainingAttempt old = Attempt();
        testee.StartSession(old.SessionId);
        testee.Request(Request(old, 1, "Old ready"));
        SpeechCall first = await speech.NextCall();
        testee.Request(Request(old, 2, "Old pending"));
        if (explicitStop) testee.StopSession(old.SessionId);
        TrainingAttempt next = Attempt();
        testee.StartSession(next.SessionId);
        testee.Request(Request(old, 3, "Late old request"));
        testee.Request(Request(next, 1, "New ready"));
        SpeechCall current = await speech.NextCall();
        testee.StopSession(old.SessionId);
        testee.Invalidate(new(old, 100));
        first.Completion.SetResult(SpeechOutputResult.Failed);

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(current.Token.IsCancellationRequested);
        Assert.Equal("New ready", current.Text);
        Assert.Equal(2, speech.Calls.Count);
    }

    [Fact]
    public async Task Dispose_stops_speech_and_unsubscribes_external_interruptions()
    {
        FakeSpeech speech = new();
        TrainingAnnouncements testee = new(speech);
        TrainingAttempt attempt = Attempt();
        testee.StartSession(attempt.SessionId);
        testee.Request(Request(attempt, 1, "Ready"));
        SpeechCall call = await speech.NextCall();
        testee.Dispose();
        speech.Interrupt();
        testee.Request(Request(attempt, 2, "Late"));

        Assert.True(call.Token.IsCancellationRequested);
        Assert.Equal(0, speech.Subscribers);
        Assert.Single(speech.Calls);
    }

    private static TrainingAnnouncementRequest Request(TrainingAttempt attempt, long id, string text)
        => new(attempt, id, TrainingAnnouncementKind.Ready, text, null);

    private static TrainingAttempt Attempt(TrainingStrategyKind strategy = TrainingStrategyKind.SnakeShot)
        => new(Guid.NewGuid(), 1, strategy, Team.A, default,
            new TrainingAnchor(new Frame(1, 100), new Point(100, 200), BallPossession.None));

    private sealed record SpeechCall(string Text, CancellationToken Token)
    {
        public TaskCompletionSource<SpeechOutputResult> Completion { get; } = new();
    }

    private sealed class FakeSpeech : ISpeechOutput
    {
        private readonly Channel<SpeechCall> _Started = Channel.CreateUnbounded<SpeechCall>();
        private Action? _Interrupted;
        public int Subscribers { get; private set; }
        public event Action? Interrupted
        {
            add { _Interrupted += value; Subscribers++; }
            remove { _Interrupted -= value; Subscribers--; }
        }

        public TaskCompletionSource<SpeechOutputResult>? Preparation { get; init; }
        public bool IsOutputAllowed { get; set; } = true;
        public SpeechOutputResult PreparationResult { get; init; } = SpeechOutputResult.Ready;
        public int PrepareCount { get; private set; }
        public ConcurrentQueue<SpeechCall> Calls { get; } = new();

        public Task<SpeechOutputResult> PrepareAsync(CancellationToken ct)
        {
            PrepareCount++;
            return Preparation?.Task.WaitAsync(ct) ?? Task.FromResult(PreparationResult);
        }

        public Task<SpeechOutputResult> SpeakAsync(string text, CancellationToken ct)
        {
            SpeechCall call = new(text, ct);
            Calls.Enqueue(call);
            _Started.Writer.TryWrite(call);
            return call.Completion.Task.WaitAsync(ct);
        }

        public void Interrupt() => _Interrupted?.Invoke();
        public async Task<SpeechCall> NextCall() => await _Started.Reader.ReadAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ManualTaskScheduler : TaskScheduler
    {
        private readonly Queue<Task> _Tasks = new();
        public void RunNext() => TryExecuteTask(_Tasks.Dequeue());
        protected override void QueueTask(Task task) => _Tasks.Enqueue(task);
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
        protected override IEnumerable<Task> GetScheduledTasks() => _Tasks.ToArray();
    }
}
