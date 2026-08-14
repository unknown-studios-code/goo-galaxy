using System;

namespace GooGalaxy.Runtime.Energy.Models
{
    /// <summary>
    /// Represents the mutable, live runtime energy state of a player.
    /// </summary>
    public class EnergyState
    {
        private float _currentEnergy;
        private int _pendingSpendCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnergyState"/> class.
        /// </summary>
        /// <param name="config">The starting energy configuration.</param>
        public EnergyState(EnergyConfig config)
        {
            Config = config;
            SetEnergy(config.StartingEnergy);
            LastPublishedEnergy = _currentEnergy;
        }

        /// <summary>
        /// Gets the immutable configuration parameters for this state.
        /// </summary>
        public EnergyConfig Config { get; }

        /// <summary>
        /// Gets the player's current energy, always clamped between 0 and Config.MaxEnergy.
        /// </summary>
        public float CurrentEnergy => _currentEnergy;

        /// <summary>
        /// Gets the energy value last broadcast on the match bus, which trails
        /// <see cref="CurrentEnergy"/> between broadcasts.
        /// </summary>
        /// <remarks>
        /// Publication is throttled against this value rather than against the previous frame's, so energy
        /// that drifts by less than the threshold each frame still crosses it eventually instead of never
        /// being announced at all.
        /// </remarks>
        public float LastPublishedEnergy { get; private set; }

        /// <summary>
        /// Gets the number of successful spends that have been applied to the balance but not yet announced.
        /// </summary>
        public int PendingSpendCount => _pendingSpendCount;

        /// <summary>
        /// Sets the player's current energy, clamping the value between 0 and Config.MaxEnergy.
        /// </summary>
        /// <param name="value">The unclamped energy value to apply.</param>
        public void SetEnergy(float value)
        {
            _currentEnergy = MathF.Max(0f, MathF.Min(value, Config.MaxEnergy));
        }

        /// <remarks>
        /// Internal because the publication cycle belongs to the presenter that owns the flush: a caller holding
        /// this state through <c>GetEnergy</c>'s sibling accessor could otherwise queue a spend that never
        /// happened. Counted rather than flagged so two spends resolved in the same frame are each announced,
        /// both carrying the balance as it stands when they are flushed rather than as it stood at each spend.
        /// </remarks>
        internal void MarkSpendPending()
        {
            _pendingSpendCount++;
        }

        /// <remarks>
        /// Clamped at zero rather than asserted: the refund contract already forbids reversing a charge that was
        /// never taken, and a presenter mid-teardown is not worth failing a build over.
        /// </remarks>
        internal void CancelPendingSpend()
        {
            if (_pendingSpendCount > 0)
            {
                _pendingSpendCount--;
            }
        }

        /// <remarks>
        /// Called before the announcements are raised, never after, so a subscriber that re-enters the presenter
        /// finds the work already recorded instead of flushing the same spend twice.
        /// </remarks>
        internal void MarkPublished()
        {
            _pendingSpendCount = 0;
            LastPublishedEnergy = _currentEnergy;
        }

        /// <summary>
        /// Gets or sets whether overtime is active for this player.
        /// </summary>
        public bool IsOvertime { get; set; }

        /// <summary>
        /// Gets or sets the catch-up multiplier (default is 1.0).
        /// </summary>
        public float CatchUpMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Gets the effective energy regeneration rate per second, taking overtime and catch-up multipliers into account.
        /// </summary>
        public float EffectiveRegenRate => Config.RegenRate * (IsOvertime ? 2.0f : 1.0f) * CatchUpMultiplier;
    }
}
