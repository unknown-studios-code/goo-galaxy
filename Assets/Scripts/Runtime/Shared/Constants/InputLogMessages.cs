namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the faults of the <c>Runtime.Input</c> assembly: a pointer view with nothing authored to
    /// read a finger through, a presenter that was never injected what it needs to read the board or commit an
    /// action, and the three actions a gesture can have been refused for.
    /// </summary>
    /// <remarks>
    /// Kept here with every other <c>*LogMessages</c> class rather than inside <c>Runtime.Input</c>, so the whole
    /// project's console text is auditable in one folder. Every message that takes arguments carries the
    /// <c>Format</c> suffix, and every one names the object whose state is wrong first — an input fault is read
    /// while nothing on screen responds, and the reader needs something to click.
    /// <para>
    /// The three rejection messages are written at log level rather than warning level, for the reason
    /// <see cref="AiLogMessages" /> gives for its own. Each message's own text names which of its codes are
    /// ordinary contention and which mean the highlight and the board disagree about the rules.
    /// </para>
    /// </remarks>
    public static class InputLogMessages
    {
        public const string PointerActionAssetMissing =
            "PointerInputView has no InputActionAsset assigned, so no finger reaches the board and the match cannot be played. "
            + "Assign the match Input Actions asset in the Inspector on the input object.";

        public const string PointerActionMapMissingFormat =
            "PointerInputView found no action map named '{0}' in the assigned asset, so no pointer action can be enabled and the match "
            + "cannot be played. Add the map to the Input Actions asset, or correct InputActionNames.MatchMap.";

        public const string PointerActionMissingFormat =
            "PointerInputView found no action named '{0}' in the '{1}' map, so that half of the pointer never reports and taps are lost. "
            + "Add the action to the Input Actions asset, or correct the matching constant in InputActionNames.";

        public const string BoardCameraMissing =
            "MatchInputController has no board camera assigned and no camera is tagged MainCamera, so no screen point can be turned into a "
            + "hex and every tap is discarded. Assign the board camera in the Inspector on the input object.";

        public const string MatchInputGridPresenterMissing =
            "MatchInputController was not injected with a GridPresenter, so it cannot read the board and every tap is discarded. "
            + "Register GridPresenter in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputGridViewMissing =
            "MatchInputController was not injected with a GridView, so it cannot read the size the board was drawn at and every tap resolves "
            + "to the wrong hex. Register GridView in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputUnitPresenterMissing =
            "MatchInputController was not injected with a UnitPresenter, so it can neither find the player's units nor move them. "
            + "Register UnitPresenter in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputCardPresenterMissing =
            "MatchInputController was not injected with a CardPresenter, so it cannot resolve the cards in hand and no card can be played. "
            + "Register CardPresenter in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputDeployControllerMissing =
            "MatchInputController was not injected with a DeployController, so it has no way to play a card. "
            + "Register DeployController in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputDiscardControllerMissing =
            "MatchInputController was not injected with a CardDiscardController, so a card dragged to the discard zone stays in hand. "
            + "Register CardDiscardController in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputCardCycleMissing =
            "MatchInputController was not injected with a card cycle, so it cannot read the hand and pressing a card highlights nothing. "
            + "Register DeckPresenter as ICardCycle in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputEnergyLedgerMissing =
            "MatchInputController was not injected with an energy ledger, so no action can be priced and nothing is ever highlighted. "
            + "Register EnergyPresenter as IEnergyLedger in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputPointerSourceMissing =
            "MatchInputController was not injected with a pointer source, so no finger reaches it and the match cannot be played. "
            + "Register PointerInputView in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputHighlightPresenterMissing =
            "MatchInputController was not injected with a TargetHighlightPresenter, so legal targets are never shown and the player is "
            + "guessing. Register TargetHighlightPresenter in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputHandGestureSourceMissing =
            "MatchInputController was not injected with a hand gesture source, so pressing a card in hand does nothing and only board units "
            + "can be commanded. Register MatchHudView as IHandGestureSource in GameLifetimeScope and keep it in the scene.";

        public const string HighlightGridViewMissing =
            "TargetHighlightPresenter was not injected with a GridView, so it holds no cell to tint and no legal target is ever shown. "
            + "Register GridView in GameLifetimeScope and keep it in the scene.";

        public const string MatchInputLocalSeatUnresolvedFormat =
            "MatchInputController found no seat driven by the local player (player one is {0}, player two is {1}), so it accepts input for "
            + "player {2}. Expected in a machine-versus-machine debug match; anywhere else, the match configuration named the wrong control.";

        public const string MoveRejectedFormat =
            "MatchInputController: player {0}'s {1} was rejected by the board: {2}. TargetOccupied, TargetBlocked and ResolverBusy are "
            + "normal — both players act at once, so a highlighted sector can be taken before the finger lifts. Any other code means "
            + "the highlight and the board disagree about the rules.";

        public const string CardPlayRejectedFormat =
            "MatchInputController: player {0}'s play from hand slot {1} was rejected: {2}. IllegalPlacement and ResolverBusy are "
            + "normal — both players act at once, so a highlighted sector can be taken before the finger lifts. Any other code means "
            + "the highlight and the board disagree about the rules.";

        public const string CardDiscardRejectedFormat =
            "MatchInputController: player {0}'s discard of hand slot {1} was rejected: {2}. DeckBusy is normal — both players act at "
            + "once, so a discard can land while another play is mid-resolution. Any other code means the discard zone and the deck "
            + "disagree about the rules.";
    }
}
