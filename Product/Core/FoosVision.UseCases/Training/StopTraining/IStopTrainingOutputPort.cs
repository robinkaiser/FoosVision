// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Training.StopTraining;

public record StopTrainingResponse(Guid SessionId, TrainingAnnouncementInvalidation? AnnouncementInvalidation);

public interface IStopTrainingOutputPort
{
    Task ReportStopped(StopTrainingResponse response);
    Task ReportStopFailed(string reason);
}
