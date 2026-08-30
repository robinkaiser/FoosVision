// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;

namespace FoosVision.Domain.Installation.Entities;

public enum ChangeKind
{
    UpdateTableConfigRequest,
}

public abstract record Change(ChangeKind Kind);
public record UpdateTableConfigRequest() : Change(ChangeKind.UpdateTableConfigRequest);

public class InstallSession
{
    private const long _TableConfigUpdateInterval_ns = 2000L * 1_000_000L;

    private static readonly Source _Log = new("InstallSession");

    private readonly Lock _Gate = new();

    private Frame _LastTableConfigUpdateFrame;
    private bool _TableConfigUpdateInProgress;

    public InstallSession(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

    public IReadOnlyList<Change> ApplyFrame(Frame frame)
    {
        lock (_Gate)
        {
            bool isDue = (frame.TimestampNs - _LastTableConfigUpdateFrame.TimestampNs) >= _TableConfigUpdateInterval_ns;
            if (!isDue) return [];

            if (_TableConfigUpdateInProgress)
            {
                _Log.Warning("ApplyFrame - Table config update is due but skipped because a table update is already running. FrameId={FrameId}", frame.Id);
                return [];
            }

            _LastTableConfigUpdateFrame = frame;
            _TableConfigUpdateInProgress = true;

            UpdateTableConfigRequest request = new();

            return [request];
        }
    }

    public void CompleteTableUpdate()
    {
        lock (_Gate)
        {
            _TableConfigUpdateInProgress = false;
        }
    }
}
