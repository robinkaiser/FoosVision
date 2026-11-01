// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Common;

/// <summary>
/// Reserves exclusive access for one session: a single run of an application mode,
/// from startup through execution to completed teardown, for example from
/// <c>StartGame</c> to completion of <c>StopGame</c>. Starting the mode again
/// creates a new session with a new ID.
/// </summary>
/// <remarks>
/// Use cases sharing this gate cannot start overlapping sessions, even for different modes.
/// The gate tracks only the reservation and its session ID; it does not know specific modes
/// or session objects. A failed start releases its uncommitted reservation on disposal.
/// A committed reservation remains held until explicitly released after session teardown.
/// </remarks>
public class SessionGate
{
    private readonly Lock _Gate = new();
    private Reservation? _Active;

    public Reservation? TryReserve()
    {
        lock (_Gate)
        {
            if (_Active is not null) return null;
            _Active = new Reservation(this);
            return _Active;
        }
    }

    public void Release(Guid sessionId)
    {
        lock (_Gate)
        {
            if (_Active?.SessionId == sessionId) _Active = null;
        }
    }

    public sealed class Reservation : IDisposable
    {
        private readonly SessionGate _Owner;
        private bool _Committed;

        internal Reservation(SessionGate owner) => _Owner = owner;
        public Guid SessionId { get; } = Guid.NewGuid();

        public void Commit()
        {
            lock (_Owner._Gate)
            {
                _Committed = true;
            }
        }

        public void Dispose()
        {
            lock (_Owner._Gate)
            {
                if (!_Committed && ReferenceEquals(_Owner._Active, this)) _Owner._Active = null;
            }
        }
    }
}
