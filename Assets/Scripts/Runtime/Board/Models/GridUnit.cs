using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// Runtime state of a single unit standing on the hex board.
    /// A pure domain model with no Unity dependency: units are created, moved, and cloned by
    /// board services, while any visual representation is a separate view keyed by <see cref="UnitId"/>.
    /// Card data is resolved on demand through the card registry rather than held here.
    /// </summary>
    /// <remarks>
    /// A fresh unit carries no armor and no statuses, so a Clone produces a clean instance even when the
    /// source is wounded or frozen. The status list is not allocated until the first status is applied.
    /// </remarks>
    public class GridUnit : IConvertibleUnit
    {
        private const int ExpectedStatusCapacity = 2;

        private static readonly StatusMarker[] _noStatuses = Array.Empty<StatusMarker>();

        private List<StatusMarker> _activeStatuses;

        public GridUnit(int unitId, int playerId, CardId cardId, HexCoordinates position, bool hasArmor = false)
        {
            UnitId = unitId;
            PlayerId = playerId;
            CardId = cardId;
            Position = position;
            IsAlive = true;
            HasArmor = hasArmor;
        }

        public int UnitId { get; }

        public int PlayerId { get; internal set; }

        public CardId CardId { get; }

        public HexCoordinates Position { get; set; }

        public bool IsAlive { get; set; }

        /// <summary>
        /// Whether an intact armor layer still absorbs the next conversion attempt.
        /// Seeded from the card's authored passive at spawn, and stripped for good on the attempt that
        /// consumes it — armor never regenerates.
        /// </summary>
        public bool HasArmor { get; private set; }

        /// <summary>
        /// Whether the unit is currently under Cryo-Stasis, and therefore unable to move or be converted.
        /// Derived from <see cref="ActiveStatuses"/> so the flag can never disagree with the marker list.
        /// </summary>
        public bool IsFrozen => HasStatus(StatusType.Frozen);

        /// <summary>
        /// Whether a fuse is running on the unit. A fused unit is removed the moment
        /// <see cref="RemainingFuseSeconds"/> reaches zero, and nothing else resolves when it does.
        /// </summary>
        /// <remarks>
        /// A fuse is not a status and is deliberately not one: it is unaffected by conversion, by Frozen, and by
        /// every action window. A unit converted mid-countdown keeps its remaining time and goes off for its new
        /// owner, and a frozen unit's fuse keeps burning.
        /// </remarks>
        public bool HasFuse { get; private set; }

        /// <summary>
        /// Seconds of scaled match time before the fuse removes the unit — a paused match freezes it. Zero
        /// whenever <see cref="HasFuse"/> is false.
        /// </summary>
        public float RemainingFuseSeconds { get; private set; }

        /// <summary>
        /// The conditions currently on the unit, at most one marker per <see cref="StatusType"/>.
        /// Empty until the first status is applied, and owned by this unit — callers must not cast and mutate it.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="HasStatus"/> or an indexed <c>for</c> loop: <c>foreach</c> over the interface boxes
        /// the backing enumerator, one allocation per pass.
        /// </remarks>
        public IReadOnlyList<StatusMarker> ActiveStatuses => (IReadOnlyList<StatusMarker>)_activeStatuses ?? _noStatuses;

        /// <summary>Reports whether the given condition is currently active on the unit.</summary>
        /// <param name="status">The condition to look for.</param>
        /// <returns><see langword="true"/> while a marker of that type is held; otherwise <see langword="false"/>.</returns>
        public bool HasStatus(StatusType status)
        {
            return IndexOfStatus(status) >= 0;
        }

        /// <summary>
        /// Applies a condition to the unit. Re-applying an active condition refreshes its duration rather than
        /// stacking a second marker. A <see cref="StatusType.None"/> type or a non-positive duration is ignored.
        /// </summary>
        /// <param name="status">The condition to apply.</param>
        /// <param name="duration">How long it lasts, in defender action windows. Must be one or greater.</param>
        public void AddStatus(StatusType status, int duration)
        {
            if (status == StatusType.None || duration <= 0)
            {
                return;
            }

            _activeStatuses ??= new List<StatusMarker>(ExpectedStatusCapacity);

            var marker = new StatusMarker(status, duration);
            int existingIndex = IndexOfStatus(status);

            if (existingIndex >= 0)
            {
                _activeStatuses[existingIndex] = marker;
                return;
            }

            _activeStatuses.Add(marker);
        }

        /// <summary>Clears a condition from the unit. Removing one that is not active does nothing.</summary>
        /// <param name="status">The condition to clear.</param>
        public void RemoveStatus(StatusType status)
        {
            int index = IndexOfStatus(status);

            if (index < 0)
            {
                return;
            }

            _activeStatuses.RemoveAt(index);
        }

        /// <inheritdoc />
        public ConversionOutcome ReceiveConversionAttempt(int newOwnerId)
        {
            if (!IsAlive || newOwnerId == PlayerId)
            {
                return ConversionOutcome.None;
            }

            if (IsFrozen)
            {
                return ConversionOutcome.Immune;
            }

            if (HasArmor)
            {
                HasArmor = false;
                return ConversionOutcome.ArmorStripped;
            }

            PlayerId = newOwnerId;

            return ConversionOutcome.Converted;
        }

        /// <summary>
        /// Closes one action window on every condition the unit holds, dropping the ones whose last window
        /// expires. Internal because the status system owns expiry timing: a caller that ticked directly would
        /// bypass the ownership rule that decides <i>whose</i> deployment closes the window. Allocation-free.
        /// Returns true when the unit held at least one condition and it was ticked.
        /// </remarks>
        internal bool TickStatusDurations()
        {
            if (_activeStatuses == null || _activeStatuses.Count == 0)
            {
                return false;
            }

            for (int i = _activeStatuses.Count - 1; i >= 0; i--)
            {
                StatusMarker marker = _activeStatuses[i];
                int remaining = marker.RemainingDuration - 1;

                if (remaining <= 0)
                {
                    _activeStatuses.RemoveAt(i);
                    continue;
                }

                _activeStatuses[i] = new StatusMarker(marker.Type, remaining);
            }

            return true;
        }

        /// <remarks>
        /// Internal because the fuse system owns the clock, exactly as the status system owns action windows: a
        /// caller that armed a unit directly would leave it burning a fuse no ticker knows about, and the unit
        /// would sit on the board forever. Re-arming refreshes the remaining time rather than stacking a second
        /// fuse — a unit carries at most one. A duration of zero or less is ignored, so a mis-authored impact
        /// cannot arm a fuse that is already expired.
        /// </remarks>
        internal void ArmFuse(float durationInSeconds)
        {
            if (durationInSeconds <= 0f)
            {
                return;
            }

            HasFuse = true;
            RemainingFuseSeconds = durationInSeconds;
        }

        /// <remarks>
        /// Reads no other state — Frozen does not pause the countdown. See <see cref="HasFuse" /> for why. Returns
        /// true on the tick that runs the fuse out, and only that tick.
        /// </remarks>
        internal bool TickFuse(float deltaSeconds)
        {
            if (!HasFuse)
            {
                return false;
            }

            RemainingFuseSeconds -= deltaSeconds;

            if (RemainingFuseSeconds > 0f)
            {
                return false;
            }

            HasFuse = false;
            RemainingFuseSeconds = 0f;

            return true;
        }

        private int IndexOfStatus(StatusType status)
        {
            if (_activeStatuses == null)
            {
                return -1;
            }

            for (int i = 0; i < _activeStatuses.Count; i++)
            {
                if (_activeStatuses[i].Type == status)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
