using System;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <remarks>
    /// Fed whatever delta the caller passes, and <c>MatchController</c> passes scaled time — see its class
    /// remarks for why that choice is not this type's to make.
    /// <para>
    /// Expiry is latched <b>and</b> edge-detectable. <see cref="HasExpired" /> stays true from the tick that
    /// drained the clock until the next <see cref="Reset" />, so a late reader still sees that the clock ran
    /// out; <see cref="TryConsumeExpiry" /> hands that edge to exactly one caller, so the transition the expiry
    /// triggers runs once even though <see cref="HasExpired" /> is read on every later frame. A caller that
    /// cannot act yet simply does not consume, and the edge waits for it.
    /// </para>
    /// <para>
    /// Engine-free on purpose, so the whole countdown is exercised in EditMode without a scene or a frame.
    /// </para>
    /// </remarks>
    internal sealed class MatchClock
    {
        private float _remaining;
        private bool _hasExpired;
        private bool _isExpiryUnconsumed;

        /// <remarks>Seconds of scaled match time left, clamped at zero. Never negative, however large a tick is.</remarks>
        internal float Remaining => _remaining;

        /// <remarks>
        /// True from the tick that drained the clock onward, whether or not the expiry has been consumed. False
        /// again only after <see cref="Reset" />, and false for a clock that was reset to zero seconds — nothing
        /// ran, so nothing ran out.
        /// </remarks>
        internal bool HasExpired => _hasExpired;

        /// <remarks>Restarts the clock and discards any unconsumed expiry. A negative duration is treated as zero.</remarks>
        internal void Reset(float seconds)
        {
            _remaining = MathF.Max(0f, seconds);
            _hasExpired = false;
            _isExpiryUnconsumed = false;
        }

        /// <remarks>Zero and negative deltas are ignored.</remarks>
        internal void Tick(float deltaTime)
        {
            if ((deltaTime <= 0f) || _hasExpired || (_remaining <= 0f))
            {
                return;
            }

            _remaining -= deltaTime;

            if (_remaining > 0f)
            {
                return;
            }

            _remaining = 0f;
            _hasExpired = true;
            _isExpiryUnconsumed = true;
        }

        /// <remarks>
        /// Returns true to exactly one caller per expiry, which is what lets a per-frame reader act once.
        /// Deferring is free: leaving the edge unconsumed on a frame the caller is not ready keeps it available
        /// on the next one, and only <see cref="Reset" /> discards it.
        /// </remarks>
        internal bool TryConsumeExpiry()
        {
            if (!_isExpiryUnconsumed)
            {
                return false;
            }

            _isExpiryUnconsumed = false;

            return true;
        }
    }
}
