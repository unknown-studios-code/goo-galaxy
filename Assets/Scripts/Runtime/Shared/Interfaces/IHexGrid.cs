namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Decoupled interface contract representing the hex grid model.
    /// Exposes read-only access to grid dimensions to decouple assemblies.
    /// </summary>
    public interface IHexGrid
    {
        public int GridRadius { get; }
    }
}
