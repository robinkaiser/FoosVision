// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;

namespace FoosVision.Adapters.Viewer.Session.Active;

/// <summary>Connection-owned replay correlation; no mode-specific decisions or device clocks.</summary>
internal class ReplayMessageReceiver
{
    public Guid? SessionId { get; private set; }
    private ReplayId? _Latest;
    private bool _Started;
    private bool _ClipReceived;
    private bool _Finished;

    public bool ReturnRequested { get; private set; }

    public bool SetSession(Guid? sessionId)
    {
        if (SessionId == sessionId)
        {
            return false;
        }

        SessionId = sessionId;
        _Latest = null;
        ResetReplay();
        return true;
    }

    public bool AcceptStarted(Guid sessionId, ReplayId replayId)
    {
        if (!SelectReplay(sessionId, replayId) || _Started || _ClipReceived)
        {
            return false;
        }

        _Started = true;
        return true;
    }

    public bool AcceptReplay(Guid sessionId, ReplayId replayId)
    {
        if (!SelectReplay(sessionId, replayId) || _ClipReceived)
        {
            return false;
        }

        _ClipReceived = true;
        return true;
    }

    public bool AcceptReturn(Guid sessionId, ReplayId replayId)
    {
        if (!SelectReplay(sessionId, replayId) || ReturnRequested)
        {
            return false;
        }

        ReturnRequested = true;
        return true;
    }

    public bool IsReturnRequested(ReplayId replayId) => _Latest == replayId && ReturnRequested;

    public void CompleteReplay(ReplayId replayId)
    {
        if (_Latest == replayId)
        {
            _Finished = true;
        }
    }

    private bool SelectReplay(Guid sessionId, ReplayId replayId)
    {
        if (sessionId != SessionId)
        {
            return false;
        }

        if (_Latest is ReplayId latest)
        {
            if (replayId == latest)
            {
                return !_Finished;
            }

            // Both values are recorder timestamps, never compared with the viewer clock.
            if (replayId.TriggerTimestampNs <= latest.TriggerTimestampNs)
            {
                return false;
            }
        }

        _Latest = replayId;
        ResetReplay();
        return true;
    }

    private void ResetReplay()
    {
        _Started = false;
        _ClipReceived = false;
        _Finished = false;
        ReturnRequested = false;
    }
}
