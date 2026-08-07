namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Contract exposing how far an entity's landing converts.
    /// Used by Board (conversion resolution) and Cards (card definitions).
    /// </summary>
    /// <remarks>
    /// Implement it on a reference type, alongside <see cref="IMoveCapable"/>: the board keeps one capability
    /// object per live unit in an <c>IMoveCapable</c>-typed registry and tests it for this interface, and a
    /// value type stored behind an interface boxes on every store.
    /// </remarks>
    public interface IConversionCapable
    {
        /// <summary>
        /// Hex rings around the landing hex whose enemy occupants receive a conversion attempt. One is the
        /// standard reach; Volatile Mass is the only card authored wider. Must be one or greater — the board
        /// falls back to a single ring for any entity that does not implement this contract.
        /// </summary>
        public int ConversionRadius { get; }
    }
}
