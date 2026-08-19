namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the Kit authoring and deck wiring problems a designer or an integrator has to act on.
    /// </summary>
    /// <remarks>
    /// Split from <see cref="CardLogMessages" /> because a Kit fault is read in both places: the same
    /// <see cref="KitTooSmallFormat" /> is warned from <c>KitDataSO.OnValidate</c> while authoring and logged as
    /// an error from <c>DeckPresenter.InitializePlayer</c> at deal time, so it is stated once here rather than
    /// split across an authoring class and a runtime one. Every message that takes arguments carries the
    /// <c>Format</c> suffix.
    /// <para>
    /// Scoped to what <c>Runtime.Deck</c> itself logs. The deploy and discard wiring faults moved to
    /// <see cref="MatchLogMessages" /> along with the components that log them.
    /// </para>
    /// </remarks>
    public static class DeckLogMessages
    {
        public const string KitCardMissingFormat =
            "{0}: Kit slot {1} is empty. Assign a CardDataSO or delete the slot — an empty one is dropped from the shuffled deck, "
            + "which silently shortens the Kit and can leave it too small to deal a hand.";

        public const string KitTooSmallFormat =
            "{0}: the Kit authors {1} cards, fewer than the {2} needed to fill a hand of {3} plus its next slot. "
            + "No deck can be built from it and the player would start with no hand. Add cards to the Kit, or lower Hand Size on DeckPresenter.";

        public const string KitDataMissing = "DeckPresenter has no KitDataSO assigned, so no player can be dealt a hand. Assign a Kit asset in the Inspector.";

        public const string HandSizeTooSmallFormat =
            "DeckPresenter has a Hand Size of {0}, below the minimum of {1}. A hand that small has no slot to rotate, so no deck can be "
            + "built from it and the player would start with no hand. Raise Hand Size on DeckPresenter.";
    }
}
