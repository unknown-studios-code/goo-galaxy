namespace GooGalaxy.Runtime.Cards.Models
{
    /// <summary>
    /// Which of the two authoring shapes a card takes: a troop that occupies a hex, or a one-time Protocol effect.
    /// </summary>
    /// <remarks>
    /// Values are explicit because this enum is serialized into every authored card asset — renumbering silently
    /// repoints every saved asset to a different member.
    /// </remarks>
    public enum CardType
    {
        /// <summary>A unit that deploys onto the board and can Clone, Jump, and be converted.</summary>
        Troop = 0,

        /// <summary>A one-time Protocol effect resolved on player-picked hexes, with no unit left behind.</summary>
        Spell = 1,
    }
}
