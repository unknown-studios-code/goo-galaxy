namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the faults of the <c>Runtime.Match</c> assembly that an integrator or a designer has to
    /// act on: a match that cannot start, a starting position that cannot be seeded, a phase sequence that went
    /// somewhere it should not, and the wiring the two card controllers need to accept a play or a discard.
    /// </summary>
    /// <remarks>
    /// Every message that takes arguments carries the <c>Format</c> suffix, matching
    /// <see cref="BoardLogMessages" /> and <see cref="DeckLogMessages" />. The starting-placement messages all
    /// name the authored index rather than the coordinate alone, because the reader's next action is to open the
    /// Match Config asset and fix that entry.
    /// <para>
    /// The deploy and discard wiring faults live here rather than in <see cref="DeckLogMessages" /> because the
    /// components that log them moved into <c>Runtime.Match</c>: one feature's faults are read together or not
    /// at all. <see cref="DeckLogMessages" /> keeps what <c>Runtime.Deck</c> itself still logs — the Kit and the
    /// hand.
    /// </para>
    /// </remarks>
    public static class MatchLogMessages
    {
        public const string MatchConfigMissing =
            "MatchController has no MatchConfigSO assigned, so no match can be started: nothing authors the phase durations or the starting position. "
            + "Assign a Match Config asset in the Inspector.";

        public const string MatchInitializerMissing =
            "MatchController was not injected with a MatchInitializer, so no match can be set up. "
            + "Register MatchInitializer in GameLifetimeScope and keep a GameLifetimeScope in the scene.";

        public const string MatchUnitPresenterMissing =
            "MatchController was not injected with a UnitPresenter, so no score can be counted and no match can be decided. "
            + "Register both components in GameLifetimeScope and keep them in the scene.";

        public const string MatchDeployControllerMissing =
            "MatchController was not injected with a DeployController, so card plays would never be gated on the match phase. "
            + "Register both components in GameLifetimeScope and keep them in the scene.";

        public const string MatchDiscardControllerMissing =
            "MatchController was not injected with a CardDiscardController, so discards would never be gated on the match phase. "
            + "Register both components in GameLifetimeScope and keep them in the scene.";

        public const string MatchEnergyPresenterMissing =
            "MatchController was not injected with an EnergyPresenter, so overtime will never double energy regeneration and both players will "
            + "regenerate at the standard rate through sudden death. Register both components in GameLifetimeScope and keep them in the scene.";

        public const string DeployCardCycleMissing =
            "DeployController was not injected with an ICardCycle, so no card can be read from a hand and every play is rejected. "
            + "Register DeckPresenter as ICardCycle in GameLifetimeScope and keep a DeckPresenter in the scene.";

        public const string DeployCardPresenterMissing =
            "DeployController was not injected with a CardPresenter, so no played card can be resolved to its authored data. "
            + "Register both components in GameLifetimeScope and keep them in the scene.";

        public const string DeployAbilityControllerMissing =
            "DeployController was not injected with an AbilityController, so no Protocol can resolve. "
            + "Register both components in GameLifetimeScope and keep them in the scene.";

        public const string DeployEnergyLedgerMissing =
            "DeployController was not injected with an IEnergyLedger, so no Protocol can be paid for and every one is rejected. "
            + "Register EnergyPresenter as IEnergyLedger in GameLifetimeScope and keep an EnergyPresenter in the scene.";

        public const string DiscardCardCycleMissing =
            "CardDiscardController was not injected with an ICardCycle, so no card can be read from a hand and every discard is rejected. "
            + "Register DeckPresenter as ICardCycle in GameLifetimeScope and keep a DeckPresenter in the scene.";

        public const string DiscardLedgerMissing =
            "CardDiscardController was not injected with an IDiscardLedger, so no discard can be paid for and every one is rejected. "
            + "Register EnergyPresenter as IDiscardLedger in GameLifetimeScope and keep an EnergyPresenter in the scene.";

        public const string DiscardDeployControllerMissing =
            "CardDiscardController was not injected with a DeployController, so it cannot tell whether a play is mid-resolution and "
            + "every discard is rejected. Register both components in GameLifetimeScope and keep them in the scene.";

        public const string MatchDomainUnavailable =
            "MatchController could not set the match up because a system it needs is missing or has been destroyed — the match initializer, the "
            + "grid, the unit registry, the card roster, the deck, or the energy ledger. Check that every one is registered in GameLifetimeScope "
            + "and present in the scene.";

        public const string MatchGridUnavailable =
            "MatchInitializer cannot set a match up because GridPresenter built no grid. Check that its Grid Layout asset is assigned.";

        public const string CountdownSubscriberFailed =
            "A MatchEvents subscriber threw during the pre-match countdown — a MatchPhaseChanged handler on the phase entry, or a "
            + "MatchClockTicked handler on a tick. Normal play was opened anyway so the match is not stranded, but any remaining countdown ticks "
            + "were lost. The stack follows; fix the subscriber.";

        public const string PhaseSubscriberFailed =
            "A MatchEvents.MatchPhaseChanged subscriber threw while the match was opening normal play or abandoning the countdown. The phase had "
            + "already changed, so the match is not stranded, but later subscribers never saw it. The stack follows; fix the subscriber.";

        public const string IllegalPhaseTransitionFormat =
            "MatchController refused to move the match from phase {0} to {1}, which is not a legal transition. The phase was left unchanged and "
            + "the match is then abandoned back to None rather than stranded in a phase nothing leaves. This is a sequencing defect in the "
            + "orchestrator, not something a designer can author.";

        public const string PhaseWalkRefusedFormat =
            "MatchController could not walk the match to phase {2}: MatchState refused the step from {0} to {1}. The walk follows the phase "
            + "order and the transition table together, so one of the two has changed without the other. Fix the table or the walk, not the test.";

        public const string PhaseWalkUnreachable =
            "MatchController cannot walk the match to that phase, because no chain of legal transitions out of None reaches it. A phase member "
            + "was added without an edge into it, or the transition table lost one. Declare the edge in MatchState.";

        public const string StartingPlacementCardMissingFormat =
            "Match Config starting placement {0} names card '{1}', which is not on the CardPresenter roster. No unit was seeded and the match "
            + "did not start — a partially seeded board hands one player a geometry advantage. Fix the card id or add the card to the roster.";

        public const string StartingPlacementOffGridFormat =
            "Match Config starting placement {0} targets hex {1}, which is not on the board. No unit was seeded and the match did not start. "
            + "Move the placement inside the grid radius authored on the Grid Layout asset.";

        public const string StartingPlacementBlockedFormat =
            "Match Config starting placement {0} targets hex {1}, which the Grid Layout asset marks as blocked. No unit was seeded and the match "
            + "did not start. Move the placement or unblock the hex.";

        public const string StartingPlacementOccupiedFormat =
            "Match Config starting placement {0} targets hex {1}, which another unit already holds. No unit was seeded and the match did not "
            + "start. Two placements on the same hex, or a board that was never cleared, are the usual causes.";

        public const string StartingPlacementDuplicateUnitIdFormat =
            "Match Config starting placement {0} reuses unit id {1}, which an earlier placement already claimed. No unit was seeded and the match "
            + "did not start — re-registering an id would silently drop the earlier unit. Give every placement its own id.";

        public const string StartingPlacementRegistrationFailedFormat =
            "Match Config starting placement {0} passed validation but the unit registry refused it, so every placement already seeded has been "
            + "rolled back and the match did not start. The registry logs the specific reason above this line.";

        public const string SpawnedUnitCardMissingFormat =
            "No authored card for '{0}', so the unit spawned by this match has no armor. Add the card to the CardPresenter roster, or correct "
            + "the id the played card carries.";

        public const string MatchConfigPhaseDurationInvalidFormat =
            "{0} authors a {1} of {2} seconds, which is not a usable duration. It has been clamped to {3}. Author a positive duration on the "
            + "Match Config asset.";

        public const string MatchConfigNoPlacementsFormat =
            "{0} authors no starting placements, so a match started from it puts nothing on the board and neither player can act. Author the "
            + "opening position — two hexes per player.";
    }
}
