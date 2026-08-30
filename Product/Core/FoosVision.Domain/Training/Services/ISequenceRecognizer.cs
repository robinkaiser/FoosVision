// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public enum SequenceState
{
    /// <summary>
    /// No Sequence running, awaiting setup
    /// </summary>
    Idle,

    /// <summary>
    /// Training setup stationary 5 row completed
    /// </summary>
    PassSetupCompleted,

    /// <summary>
    /// Training setup stationary 3 row completed
    /// </summary>
    ShotSetupCompleted,

    /// <summary>
    /// Pass sequence is running
    /// </summary>
    PassSequenceRunning,

    /// <summary>
    /// Shot sequence is running
    /// </summary>
    ShotSequenceRunning,

    /// <summary>
    /// Pass or shot sequence was completed by leaving the setup Area.
    /// End state. Reset must be called to return to Idle
    /// </summary>
    SequenceCompleted,
}

public interface ISequenceRecognizer
{
    /// <summary>
    /// Sets the table configuration.
    /// </summary>
    TableConfiguration TableConfig { set; }

    /// <summary>
    /// Reset to Idle.
    /// </summary>
    void Reset();

    /// <summary>
    /// Advance trainings sequence based on given tracked ball and possession.
    /// </summary>
    /// <param name="trackedBall">Tracked ball.</param>
    /// <param name="possession">Current possession.</param>
    /// <returns>State of training sequence.</returns>
    SequenceState Process(TrackedBall trackedBall, BallPossession possession);
}
