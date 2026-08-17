namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Decoupled interface contract representing the hex grid model.
    /// Exposes read-only access to grid dimensions to decouple assemblies.
    /// </summary>
    public interface IHexGrid
    {
        /// <summary>The board's radius, counted in rings around the centre hex — not in cells.</summary>
        public int GridRadius { get; }
    }
}
