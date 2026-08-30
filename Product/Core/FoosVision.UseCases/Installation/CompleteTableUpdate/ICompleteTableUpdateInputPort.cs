// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Installation.CompleteTableUpdate;

public record CompleteTableUpdateRequest();

public interface ICompleteTableUpdateInputPort
{
    Task Handle(CompleteTableUpdateRequest request, CancellationToken ct);
}
