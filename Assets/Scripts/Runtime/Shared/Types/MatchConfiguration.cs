namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// The settled configuration a match starts with, published on <see cref="Events.MatchEvents.MatchStarted" />.
    /// </summary>
    /// <remarks>
    /// Deliberately empty: no field is authored yet, so a gameplay system must not assume one exists. Seed, player
    /// ids and board configuration are the expected additions once match setup is real.
    /// </remarks>
    public readonly struct MatchConfiguration { }
}
