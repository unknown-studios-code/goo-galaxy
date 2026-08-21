namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the faults of the <c>Runtime.AI</c> assembly that an integrator or a designer has to act
    /// on: a machine player with no tuning asset, one that was never injected what it needs to read the board or
    /// commit an action, and the two actions a tick can have refused.
    /// </summary>
    /// <remarks>
    /// Every message that takes arguments carries the <c>Format</c> suffix, matching
    /// <see cref="MatchLogMessages" /> and <see cref="BoardLogMessages" />. The wiring faults name
    /// <c>GameLifetimeScope</c> rather than the PvE scope, because every dependency but the controller itself is
    /// a shared match component registered there; the controller is the one entry <c>PveLifetimeScope</c> adds.
    /// <para>
    /// The two rejection messages are written at log level rather than warning level on purpose. Both players
    /// act simultaneously, so a sector enumerated as empty can be taken before the machine commits to it — a
    /// refusal on that contention is the system working, and raising it to a warning would bury the faults
    /// above it. A refusal for any other reason <i>is</i> a fault, which is why both messages name the reason
    /// rather than assuming one: the enumerator only ever offers actions a validator already accepted, so
    /// anything but contention says the enumerator and the board disagree about the rules.
    /// </para>
    /// </remarks>
    public static class AiLogMessages
    {
        public const string AiConfigMissing =
            "AiController has no AiConfigSO assigned, so nothing authors its think interval or its Energy ceiling and it will not act. "
            + "Assign an AI Config asset in the Inspector, or remove the component from the scene.";

        public const string AiGridPresenterMissing =
            "AiController was not injected with a GridPresenter, so it cannot read the board and will not act. "
            + "Register GridPresenter in GameLifetimeScope and keep it in the scene.";

        public const string AiUnitPresenterMissing =
            "AiController was not injected with a UnitPresenter, so it can neither find its units nor move them. "
            + "Register UnitPresenter in GameLifetimeScope and keep it in the scene.";

        public const string AiCardPresenterMissing =
            "AiController was not injected with a CardPresenter, so it cannot resolve the cards in its hand and will never play one. "
            + "Register CardPresenter in GameLifetimeScope and keep it in the scene.";

        public const string AiDeployControllerMissing =
            "AiController was not injected with a DeployController, so it has no way to play a card. "
            + "Register DeployController in GameLifetimeScope and keep it in the scene.";

        public const string AiDiscardControllerMissing =
            "AiController was not injected with a CardDiscardController, so it can never cycle a dead hand. "
            + "Register CardDiscardController in GameLifetimeScope and keep it in the scene.";

        public const string AiMatchControllerMissing =
            "AiController was not injected with a MatchController, so it cannot tell whether the match is open and would act after the clock expired. "
            + "Register MatchController in GameLifetimeScope and keep it in the scene.";

        public const string AiCardCycleMissing =
            "AiController was not injected with an ICardCycle, so it cannot read its hand and will never play or discard a card. "
            + "Register DeckPresenter as ICardCycle in GameLifetimeScope and keep it in the scene.";

        public const string AiEnergyLedgerMissing =
            "AiController was not injected with an IEnergyLedger, so it cannot tell what it can afford and will not act. "
            + "Register EnergyPresenter as IEnergyLedger in GameLifetimeScope and keep it in the scene.";

        public const string AiDiscardLedgerMissing =
            "AiController was not injected with an IDiscardLedger, so it can never cycle a dead hand. "
            + "Register EnergyPresenter as IDiscardLedger in GameLifetimeScope and keep it in the scene.";

        /// <remarks>
        /// The action name <see cref="AiActionRejectedFormat" /> takes for a Protocol, which has no
        /// <c>MoveType</c> of its own to name — it is priced as a Deploy, and reporting it as one would send the
        /// reader to the wrong rule.
        /// </remarks>
        public const string ProtocolActionName = "Protocol";

        public const string AiActionRejectedFormat =
            "AiController: player {0} had a {1} refused as {2}. TargetOccupied, TargetBlocked and ResolverBusy are normal — both players act at "
            + "once, so a sector enumerated as empty can be taken before the machine commits to it — and the tick is dropped so the next one "
            + "re-reads the board. Any other reason is a fault: the enumerator only ever offers actions a validator already accepted.";

        public const string AiThinkLoopFailed =
            "AiController: a subscriber threw while the machine player was committing an action, which ends its think loop for the rest of the "
            + "match. Fix the subscriber named in the exception below; the machine player stops acting until the match restarts.";

        public const string AiDiscardRejectedFormat =
            "AiController: player {0} had a discard of slot {1} refused as {2}. The tick is dropped and the next one re-reads the hand.";

        public const string AiConfigThinkRangeInvalidFormat =
            "AI Config '{0}' authors a maximum think interval of {1}s below its minimum of {2}s, which would make every wait the minimum. "
            + "Raised to {2}s; author the maximum at or above the minimum.";
    }
}
