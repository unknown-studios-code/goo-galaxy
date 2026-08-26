namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the wiring and authoring problems the runtime UI reports.
    /// </summary>
    /// <remarks>
    /// Kept here with every other <c>*LogMessages</c> class rather than inside <c>Runtime.UI</c>, so the whole
    /// project's console text is auditable in one folder. Every message that takes arguments carries the
    /// <c>Format</c> suffix, and every one of them names the object whose state is wrong first — a HUD fault is
    /// read while the screen is blank, and the reader needs something to click.
    /// </remarks>
    public static class UiLogMessages
    {
        public const string UiDocumentMissingFormat =
            "{0}: no UIDocument on this GameObject, so no panel can be built and the screen renders nothing. "
            + "Add a UIDocument component beside the view, above it in the component order.";

        public const string PanelUnavailableFormat =
            "{0}: the UIDocument has no root visual element by Start, so no element could be cached and the screen renders nothing. "
            + "Assign a Source Asset and a Panel Settings asset on the UIDocument, and add the UIDocument before the view so it builds first.";

        public const string ElementMissingFormat =
            "{0}: the bound UXML has no element named '{1}', so that part of the screen will never update. "
            + "Restore the name in the UXML, or update the matching constant in HudSelectors.";

        public const string HudViewMissing =
            "MatchHudPresenter has no MatchHudView assigned, so every match update it receives is discarded and the HUD renders nothing. "
            + "Assign the view in the Inspector on the HUD object.";

        public const string HudMatchControllerMissing =
            "MatchHudPresenter was constructed without a MatchController, so it cannot read the opening phase, clock or scores and the HUD "
            + "starts blank until the first event arrives. Register MatchController in the scene's lifetime scope.";

        public const string HudEnergyPresenterMissing =
            "MatchHudPresenter was constructed without an EnergyPresenter, so the Energy gauge stays empty and no hand slot can be priced. "
            + "Register EnergyPresenter in the scene's lifetime scope.";

        public const string HudDeckPresenterMissing =
            "MatchHudPresenter was constructed without a DeckPresenter, so the hand cannot be filled before the first deal and the strip "
            + "starts empty. Register DeckPresenter in the scene's lifetime scope.";

        public const string HudCardPresenterMissing =
            "MatchHudPresenter was constructed without a CardPresenter, so no card in hand can be named or priced and every slot renders "
            + "empty. Register CardPresenter in the scene's lifetime scope.";

        public const string HudLocalSeatUnresolvedFormat =
            "MatchHudPresenter found no seat driven by the local player (player one is {0}, player two is {1}), so the HUD renders player "
            + "{2} as the home side. Expected in a machine-versus-machine debug match; anywhere else, the match configuration named the wrong control.";

        public const string HudCardDataMissingFormat =
            "MatchHudPresenter could not resolve card '{0}' through CardPresenter, so its hand slot renders empty. "
            + "Add the CardDataSO to the card registry, or remove the card from the Kit that dealt it.";

        public const string HudHandLongerThanStripFormat =
            "MatchHudPresenter was dealt a hand of {0} cards but the HUD strip authors {1} slots, so the surplus is not drawn. "
            + "Lower Hand Size on DeckPresenter, or add slots to MatchHudView.uxml and raise HudSelectors.HandSlotCount.";
    }
}
