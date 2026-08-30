// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Game.CompleteTableSceneUpdate;

public record CompleteTableSceneUpdateRequest();

public interface ICompleteTableSceneUpdateInputPort
{
    Task Handle(CompleteTableSceneUpdateRequest request, CancellationToken ct);
}
