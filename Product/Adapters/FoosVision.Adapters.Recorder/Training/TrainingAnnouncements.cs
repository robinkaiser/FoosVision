// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Threading.Channels;
using FoosVision.Common.Logging;
using FoosVision.Ports.Media;
using FoosVision.UseCases.Training;

namespace FoosVision.Adapters.Recorder.Training;

/// <summary>Owns speech work for the active training session, outside frame processing.</summary>
public class TrainingAnnouncements : ITrainingAnnouncements, IDisposable
{
    private static readonly Source _Log = new("Recorder.Training.Speech");
    private readonly ISpeechOutput _Speech;
    private readonly TaskScheduler _WorkerScheduler;
    private readonly Lock _Gate = new();
    private Session? _Session;

    public TrainingAnnouncements(ISpeechOutput speech, TaskScheduler? workerScheduler = null)
    {
        _Speech = speech;
        _WorkerScheduler = workerScheduler ?? TaskScheduler.Default;
        _Speech.Interrupted += OnInterrupted;
    }

    public void StartSession(Guid sessionId)
    {
        lock (_Gate)
        {
            StopCurrentSession();
            Session session = new(sessionId);
            _Session = session;
            _ = Task.Factory.StartNew(() => ProcessRequests(session), CancellationToken.None,
                TaskCreationOptions.DenyChildAttach, _WorkerScheduler).Unwrap();
        }
    }

    public void Request(TrainingAnnouncementRequest announcement)
    {
        lock (_Gate)
        {
            if (_Session is not { } session || session.Id != announcement.Attempt.SessionId ||
                announcement.Attempt.AttemptId < session.AttemptId ||
                announcement.AnnouncementId <= Math.Max(session.LastAnnouncementId, session.InvalidatedThrough))
            {
                return;
            }

            if (announcement.Attempt.AttemptId > session.AttemptId)
            {
                DiscardPending(session);
            }

            session.AttemptId = announcement.Attempt.AttemptId;
            // Core announcement IDs increase throughout a session, including invalidated outputs.
            session.LastAnnouncementId = announcement.AnnouncementId;
            if (!_Speech.IsOutputAllowed) return;
            session.Requests.Writer.TryWrite(announcement);
        }
    }

    public void Invalidate(TrainingAnnouncementInvalidation invalidation)
    {
        lock (_Gate)
        {
            if (_Session is not { } session || session.Id != invalidation.Attempt.SessionId)
            {
                return;
            }

            session.InvalidatedThrough = Math.Max(session.InvalidatedThrough, invalidation.ThroughAnnouncementId);
            if (session.Requests.Reader.TryPeek(out TrainingAnnouncementRequest? pending) &&
                pending.AnnouncementId <= invalidation.ThroughAnnouncementId)
            {
                session.Requests.Reader.TryRead(out _);
            }

            if (session.Current is { } current && current.AnnouncementId <= invalidation.ThroughAnnouncementId)
            {
                session.SpeechCancellation?.Cancel();
            }
        }
    }

    public void StopSession(Guid sessionId)
    {
        lock (_Gate)
        {
            if (_Session?.Id == sessionId)
            {
                StopCurrentSession();
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _Speech.Interrupted -= OnInterrupted;
        lock (_Gate)
        {
            StopCurrentSession();
        }
    }

    private void OnInterrupted()
    {
        lock (_Gate)
        {
            if (_Session is { } session)
            {
                DiscardPending(session);
            }
        }
    }

    private static void DiscardPending(Session session)
    {
        session.InvalidatedThrough = Math.Max(session.InvalidatedThrough, session.LastAnnouncementId);
        session.Requests.Reader.TryRead(out _);
        session.SpeechCancellation?.Cancel();
    }

    private void StopCurrentSession()
    {
        if (_Session is not { } session) return;

        _Session = null;
        DiscardPending(session);
        session.Cancellation.Cancel();
        session.Requests.Writer.TryComplete();
    }

    private async Task ProcessRequests(Session session)
    {
        CancellationToken ct = session.Cancellation.Token;
        try
        {
            SpeechOutputResult prepared = await _Speech.PrepareAsync(ct).ConfigureAwait(false);
            await foreach (TrainingAnnouncementRequest request in session.Requests.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                CancellationTokenSource speechCancellation;
                lock (_Gate)
                {
                    if (_Session != session || request.AnnouncementId <= session.InvalidatedThrough ||
                        request.AnnouncementId < session.LastAnnouncementId ||
                        request.Attempt.AttemptId < session.AttemptId)
                    {
                        continue;
                    }

                    if (prepared != SpeechOutputResult.Ready) continue;

                    speechCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    session.Current = request;
                    session.SpeechCancellation = speechCancellation;
                }

                try
                {
                    await _Speech.SpeakAsync(request.Text, speechCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (speechCancellation.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    _Log.Error("Speech output failed. SessionId={SessionId} AttemptId={AttemptId} AnnouncementId={AnnouncementId} Ex={Exception}",
                        session.Id, request.Attempt.AttemptId, request.AnnouncementId, ex.ToString());
                }
                finally
                {
                    lock (_Gate)
                    {
                        session.Current = null;
                        session.SpeechCancellation = null;
                        speechCancellation.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _Log.Error("Speech preparation failed. SessionId={SessionId} Ex={Exception}", session.Id, ex.ToString());
        }
        finally
        {
            // Stop/start and worker completion share ownership of the session cancellation source.
            lock (_Gate)
            {
                if (_Session == session) _Session = null;
                session.Cancellation.Dispose();
            }
        }
    }

    private sealed class Session(Guid id)
    {
        public Guid Id { get; } = id;
        public long AttemptId { get; set; }
        public long LastAnnouncementId { get; set; }
        public long InvalidatedThrough { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public CancellationTokenSource? SpeechCancellation { get; set; }
        public TrainingAnnouncementRequest? Current { get; set; }
        public Channel<TrainingAnnouncementRequest> Requests { get; } = Channel.CreateBounded<TrainingAnnouncementRequest>(
            new BoundedChannelOptions(1)
            {
                SingleReader = false, // Invalidation also removes pending requests.
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            });
    }
}
