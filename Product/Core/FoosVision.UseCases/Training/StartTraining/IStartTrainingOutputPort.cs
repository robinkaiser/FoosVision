// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Training.StartTraining;

public record StartTrainingResponse(Guid SessionId);

public interface IStartTrainingOutputPort
{
    Task ReportStarted(StartTrainingResponse response);
    Task ReportStartFailed(string reason);
}
