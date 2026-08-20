namespace GooGalaxy.Runtime.Match.Models
{
    /// <remarks>
    /// One player's catch-up timer: Idle while their unit count is healthy, Active for the authored bonus
    /// window once a deficit opens it, and Cooling for the authored lockout afterward. Ticked once per frame of
    /// play by <see cref="CatchUpTracker" />, which owns two of these — one per player.
    /// <para>
    /// <b>Idle activates the instant a deficit appears</b> and returns active on that same tick, at the full
    /// authored duration — the tick a deficit is recognised has not yet had any of the window spent against it.
    /// </para>
    /// <para>
    /// <b>Active ignores the deficit for its whole duration.</b> A deficit that clears mid-window does not cut
    /// the window short; that is what stops the flicker a strict per-frame threshold test would otherwise cause.
    /// </para>
    /// <para>
    /// <b>Cooling ignores the deficit for as long as it is still draining — this is the file's central invariant.</b> Evaluating
    /// the cooldown against the board is exactly the oscillation bug the cooldown exists to prevent: a player
    /// held below threshold would otherwise re-open the bonus the instant the active window closed and never
    /// leave it. The moment the cooldown itself drains, a deficit still in effect re-arms immediately — the same
    /// tick, at the full duration — rather than waiting for a frame the board has not moved on.
    /// </para>
    /// <para>
    /// Zero and negative deltas advance nothing, the convention <see cref="MatchClock.Tick" /> and
    /// <see cref="OvertimeLeadTracker.Tick" /> both follow. Boundaries are inclusive: a window or a cooldown that
    /// lands exactly on its authored length has expired, mirroring how <see cref="OvertimeLeadTracker" /> treats
    /// its hold threshold.
    /// </para>
    /// <para>
    /// Engine-free, and allocation-free on every tick — <see cref="CatchUpTracker" /> constructs its two
    /// windows once and holds them for the life of the match. A class rather than a struct because it is
    /// mutable and nothing serializes it, which is the shape its three siblings in this folder already take.
    /// </para>
    /// </remarks>
    internal sealed class CatchUpWindow
    {
        private CatchUpPhase _phase;
        private float _remainingSeconds;

        /// <remarks>Returns whether the bonus is active <b>after</b> this tick.</remarks>
        internal bool Tick(bool isBelowThreshold, float deltaTime, in CatchUpConfig config)
        {
            if (_phase == CatchUpPhase.Idle)
            {
                if (!isBelowThreshold)
                {
                    return false;
                }

                Activate(config);

                return true;
            }

            if (deltaTime > 0f)
            {
                _remainingSeconds -= deltaTime;
            }

            if (_phase == CatchUpPhase.Active)
            {
                if (_remainingSeconds > 0f)
                {
                    return true;
                }

                _phase = CatchUpPhase.Cooling;
                _remainingSeconds = config.CooldownSeconds;

                return false;
            }

            if (_remainingSeconds > 0f)
            {
                return false;
            }

            if (isBelowThreshold)
            {
                Activate(config);

                return true;
            }

            _phase = CatchUpPhase.Idle;
            _remainingSeconds = 0f;

            return false;
        }

        /// <remarks>
        /// Returns the window to the state a fresh one holds, so the next match measures its first deficit from
        /// Idle rather than from whatever the previous match left behind.
        /// </remarks>
        internal void Reset()
        {
            _phase = CatchUpPhase.Idle;
            _remainingSeconds = 0f;
        }

        private void Activate(in CatchUpConfig config)
        {
            _phase = CatchUpPhase.Active;
            _remainingSeconds = config.DurationSeconds;
        }

        private enum CatchUpPhase
        {
            Idle,
            Active,
            Cooling,
        }
    }
}
