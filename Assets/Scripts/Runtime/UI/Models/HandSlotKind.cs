namespace GooGalaxy.Runtime.UI.Models
{
    /// <summary>
    /// What a hand slot holds, reduced to what the HUD needs to draw it.
    /// </summary>
    /// <remarks>
    /// A narrowing of <c>CardType</c> that also carries the empty case, so a view never has to name the Cards
    /// assembly's enum and never has to pair it with a separate "is filled" flag that could disagree with it.
    /// </remarks>
    public enum HandSlotKind
    {
        /// <summary>No card in the slot.</summary>
        None = 0,

        /// <summary>A card that deploys a specimen onto the board.</summary>
        Specimen = 1,

        /// <summary>A card that resolves a one-time Protocol effect.</summary>
        Protocol = 2,
    }
}
