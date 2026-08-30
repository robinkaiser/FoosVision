// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingUiSink : IUiStateSink
{
    public List<SessionUiState> States { get; } = [];

    public void Update(SessionUiState uiState)
    {
        States.Add(uiState);
    }
}
