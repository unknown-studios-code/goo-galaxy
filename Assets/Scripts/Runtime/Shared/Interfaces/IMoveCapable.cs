namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Contract defining movement capability characteristics for entities.
    /// Used by Board (movement validation) and Cards (card definitions).
    /// </summary>
    public interface IMoveCapable
    {
        public bool CanClone { get; }
        public bool CanJump { get; }
    }
}
