using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.UI.Models
{
    /// <summary>
    /// One hand slot as the HUD draws it: which card is in it, and the authored values the slot shows.
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
    /// Rarity is copied off <c>ICardData</c> like the accent role and never re-read by the view. Artwork is
    /// deliberately not here: it travels beside the state through <c>IMatchHudView.SetHandSlot</c>, so this model
    /// stays engine-free.
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
        /// <param name="rarity">The card's authored collection tier. <see cref="CardRarity.None" /> when unauthored.</param>
        public HandSlotState(CardId cardId, string displayName, int energyCost, HandSlotKind kind, CardAccent accent, CardRarity rarity = CardRarity.None)
        {
            CardId = cardId;
            DisplayName = displayName;
            EnergyCost = energyCost;
            Kind = kind;
            Accent = accent;
            Rarity = rarity;
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

        /// <summary>The card's authored collection tier, or <see cref="CardRarity.None" /> when unauthored.</summary>
        /// <remarks>
        /// A tier rather than a colour, for the same reason as <see cref="Accent" />; no treatment is painted yet.
        /// </remarks>
        public CardRarity Rarity { get; }

        /// <summary>Whether a card actually occupies this slot.</summary>
        public bool IsFilled => Kind != HandSlotKind.None;
    }
}
