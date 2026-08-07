using System.Collections.Generic;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// What one landing's impact abilities did: the units they touched, the hexes whose state changed, and the
    /// units marked for self-cleanup. Lives in Shared so the ability event can cross assembly boundaries
    /// without any consumer depending on the Board feature assembly.
    /// </summary>
    /// <remarks>
    /// All three lists are owned by the publisher and are only valid for the duration of the callback: they are
    /// reusable buffers that the next landing clears and refills. Subscribers must copy what they intend to
    /// keep, and must read them with an indexed <c>for</c> loop — <c>foreach</c> over the interface boxes the
    /// backing enumerator, one allocation per subscriber per landing.
    /// <see cref="DestroyedUnitIds"/> names the units the acting card's self-destruct marked for removal. They
    /// are <b>still alive and still registered</b> while a subscriber reads the list: step 6 self-cleanup runs
    /// after this event is published, so the removal has not happened yet. A view despawning a visual must
    /// therefore key off the id alone and must not gate on a registry lookup — the unit is present now and gone
    /// moments later, and a lookup would succeed here and fail on the next frame's whole-board sync.
    /// </remarks>
    public readonly struct AbilityResult
    {
        /// <summary>Wraps the three buffers a landing's impacts filled. Any may be null, which reads as empty.</summary>
        /// <param name="affectedUnitIds">The units an impact applied a status to.</param>
        /// <param name="affectedHexes">The hexes whose state an impact changed, hazards included.</param>
        /// <param name="destroyedUnitIds">The units a self-destruct impact removed.</param>
        public AbilityResult(IReadOnlyList<int> affectedUnitIds, IReadOnlyList<HexCoordinates> affectedHexes, IReadOnlyList<int> destroyedUnitIds)
        {
            AffectedUnitIds = affectedUnitIds;
            AffectedHexes = affectedHexes;
            DestroyedUnitIds = destroyedUnitIds;
        }

        /// <summary>Identifiers of the units an impact applied a status to, at most once each.</summary>
        public IReadOnlyList<int> AffectedUnitIds { get; }

        /// <summary>
        /// Coordinates whose hex state an impact changed: the hexes of the affected units, and any hex a
        /// hazard was spawned on.
        /// </summary>
        public IReadOnlyList<HexCoordinates> AffectedHexes { get; }

        /// <summary>Identifiers of the units a self-destruct impact removed from the board.</summary>
        public IReadOnlyList<int> DestroyedUnitIds { get; }

        /// <summary>Whether the impacts changed nothing, including on a default-constructed value.</summary>
        public bool IsEmpty => GetCount(AffectedUnitIds) == 0 && GetCount(AffectedHexes) == 0 && GetCount(DestroyedUnitIds) == 0;

        private static int GetCount<TItem>(IReadOnlyList<TItem> items)
        {
            return items == null ? 0 : items.Count;
        }
    }
}
