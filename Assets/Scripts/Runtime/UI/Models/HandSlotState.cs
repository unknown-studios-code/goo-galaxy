using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.UI.Models
{
    /// <summary>
    /// One hand slot as the HUD draws it: which card is in it, and the authored values a slot shows without
    /// artwork.
    /// </summary>
    /// <remarks>
    /// This is the seam that keeps <c>Runtime.Cards</c> out of the view. The presenter resolves the card
    /// through <c>CardPresenter</c> and hands the view this instead, so no view type ever names
    /// <c>ICardData</c> or <c>CardType</c>.
    /// <para>
    /// <b>Affordability is not here.</b> It moves on every Energy publish, while everything in this struct
    /// only changes when the hand rotates — folding the two together would rebuild a slot's text on a path that
    /// only ever needed a class toggled.
    /// </para>
    /// <para>
    /// Artwork and rarity are absent because <c>ICardData</c> authors neither. The slot's markup leaves room for
    /// both; add the fields here when the card data carries them. The accent role is the first member of that
    /// family to land, and it arrives here the same way the rest will — copied off the card, never re-read.
    /// </para>
    /// </remarks>
    public readonly struct HandSlotState
    {
        /// <summary>An unfilled slot — what a hand shorter than the strip, or an unresolved card, renders as.</summary>
        public static readonly HandSlotState Empty = new(CardId.Empty, string.Empty, 0, HandSlotKind.None, CardAccent.None);

        /// <summary>Builds the state a slot renders.</summary>
        /// <param name="cardId">The card in the slot, or <see cref="CardId.Empty" /> when the slot is unfilled.</param>
        /// <param name="displayName">The card's player-facing name. Empty when the slot is unfilled.</param>
        /// <param name="energyCost">The card's authored Energy cost, which is also what a Deploy is priced at.</param>
        /// <param name="kind">Which of the two card shapes the slot draws.</param>
        /// <param name="accent">
        /// The card's authored accent family. <see cref="CardAccent.None" /> on a card that authors none, and on an
        /// unfilled slot.
        /// </param>
        public HandSlotState(CardId cardId, string displayName, int energyCost, HandSlotKind kind, CardAccent accent)
        {
            CardId = cardId;
            DisplayName = displayName;
            EnergyCost = energyCost;
            Kind = kind;
            Accent = accent;
        }

        /// <summary>The card in the slot, or <see cref="CardId.Empty" /> when the slot is unfilled.</summary>
        public CardId CardId { get; }

        /// <summary>The card's player-facing name. Empty when the slot is unfilled.</summary>
        public string DisplayName { get; }

        /// <summary>The card's authored Energy cost.</summary>
        public int EnergyCost { get; }

        /// <summary>Which of the two card shapes the slot draws.</summary>
        public HandSlotKind Kind { get; }

        /// <summary>The card's authored accent family, or <see cref="CardAccent.None" /> when it authors none.</summary>
        /// <remarks>
        /// A role rather than a colour, so the slot resolves the colour from a stylesheet token and a colourblind
        /// palette can swap every accent at once. <see cref="CardAccent.None" /> is the zero value, so a defaulted
        /// state draws no bar rather than a stripe in whichever colour happened to sort first.
        /// </remarks>
        public CardAccent Accent { get; }

        /// <summary>Whether a card actually occupies this slot.</summary>
        public bool IsFilled => Kind != HandSlotKind.None;
    }

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
