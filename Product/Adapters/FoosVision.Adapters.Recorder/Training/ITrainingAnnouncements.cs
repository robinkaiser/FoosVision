// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.UseCases.Training;

namespace FoosVision.Adapters.Recorder.Training;

/// <summary>Local, non-blocking handoff to recorder speech coordination. Never sent to the viewer.</summary>
public interface ITrainingAnnouncements
{
    void StartSession(Guid sessionId);
    void Invalidate(TrainingAnnouncementInvalidation invalidation);
    void Request(TrainingAnnouncementRequest announcement);
    /// <summary>Discards pending announcements and cancels ongoing speech for this session.</summary>
    void StopSession(Guid sessionId);
}

/// <summary>For hosts without local speech output, such as offline tools.</summary>
public class NullTrainingAnnouncements : ITrainingAnnouncements
{
    public void StartSession(Guid sessionId) { }
    public void Invalidate(TrainingAnnouncementInvalidation invalidation) { }
    public void Request(TrainingAnnouncementRequest announcement) { }
    public void StopSession(Guid sessionId) { }
}
