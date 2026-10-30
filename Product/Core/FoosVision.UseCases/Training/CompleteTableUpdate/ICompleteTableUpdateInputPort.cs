// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Training.CompleteTableUpdate;

public record CompleteTableUpdateRequest(Guid SessionId);

public interface ICompleteTableUpdateInputPort
{
    Task Handle(CompleteTableUpdateRequest request, CancellationToken ct);
}
