namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Contract exposing what an entity is worth in Energy.
    /// Used by Board (move pricing) and Cards (card definitions).
    /// </summary>
    /// <remarks>
    /// Implement it on a reference type, alongside <see cref="IMoveCapable"/>: the board keeps one capability
    /// object per live unit in an <c>IMoveCapable</c>-typed registry and tests it for this interface, and a
    /// value type stored behind an interface boxes on every store.
    /// </remarks>
    public interface IEnergyPriced
    {
        /// <summary>
        /// The entity's authored Energy cost, and the figure every action's price is derived from — a Deploy
        /// charges it whole, a Clone a fraction of it. The board prices an entity that does not implement this
        /// contract at <c>BoardMetrics.DefaultUnitEnergyCost</c> rather than at nothing.
        /// </summary>
        public int EnergyCost { get; }
    }
}
