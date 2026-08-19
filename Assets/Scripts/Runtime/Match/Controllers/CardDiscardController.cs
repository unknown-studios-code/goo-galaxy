using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Match.Controllers
{
    /// <summary>
    /// The single entry point for discarding a card from hand: it reads the slot, charges the discard's Energy
    /// price, and rotates the hand only once the charge has been taken.
    /// </summary>
    /// <remarks>
    /// This is the GDD's Sample Purge mechanic — a cheap, deliberate way to cycle a dead card out of hand rather
    /// than waiting for it to be played. It reuses <see cref="ICardCycle.TryAdvanceSlot"/>, the same rotation
    /// <c>DeployController</c> triggers once a play resolves, so a discarded card leaves the hand exactly the way
    /// a played one does: to the back of the cycle, with the queued <c>Next</c> card sliding into the freed slot.
    /// </remarks>
    [DisallowMultipleComponent]
    public class CardDiscardController : MonoBehaviour
    {
        private ICardCycle _cardCycle;
        private IDiscardLedger _discardLedger;
        private DeployController _deployController;
        private MatchController _matchController;
        private bool _isDiscarding;

        [Inject]
        public void Construct(ICardCycle cardCycle, IDiscardLedger discardLedger, DeployController deployController)
        {
            Debug.Assert(cardCycle != null, MatchLogMessages.DiscardCardCycleMissing, this);
            Debug.Assert(discardLedger != null, MatchLogMessages.DiscardLedgerMissing, this);
            Debug.Assert(deployController != null, MatchLogMessages.DiscardDeployControllerMissing, this);

            _cardCycle = cardCycle;
            _discardLedger = discardLedger;
            _deployController = deployController;
        }

        /// <summary>
        /// Discards the card in one of a player's hand slots: charges the discard's Energy price, then rotates
        /// that slot so the card leaves the hand for the back of the cycle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Energy is charged before the rotation and refunded if the rotation fails.</b> A refused rotation
        /// after the charge has already been taken would mean the deck vanished mid-discard, which nothing in
        /// this codebase can currently cause; it is reported as <see cref="CardDiscardResult.DeckUnavailable"/>
        /// rather than a silent no-op, and the charge is returned rather than left stranded.
        /// </para>
        /// <para>
        /// <b>The re-entrancy latch is raised before the charge, not after the rotation.</b> The rotation
        /// publishes <c>MatchEvents.HandChanged</c> synchronously, so a subscriber runs inside this call;
        /// raising the latch first is what makes a discard attempted from that subscriber fail with
        /// <see cref="CardDiscardResult.DeckBusy"/> rather than rotate a second slot out from under this one,
        /// and it covers the charge too, so no unguarded call sits inside the window. A discard attempted from
        /// inside a play is rejected on the separate <c>DeployController.IsResolving</c> latch, which this
        /// method reads alongside its own. The reverse direction is deliberately unguarded: a play started
        /// from inside this dispatch is allowed, because the rotation has already completed before
        /// <c>HandChanged</c> is published, so that play reads a consistent deck. It does reorder the bus —
        /// the play's events land before this discard's — which matters only to a consumer replaying the bus
        /// as an ordered log.
        /// </para>
        /// <para>
        /// Checks run in a fixed order, so the returned code is predictable when several would fail at once:
        /// missing dependencies, then the match phase, then re-entrancy, then the player, then the slot, then
        /// Energy.
        /// </para>
        /// <para>
        /// <b>The phase gate sits ahead of the re-entrancy latch</b>, for the reason
        /// <c>DeployController.TryPlayCard</c> states for its own: a discard made outside
        /// <see cref="MatchPhase.Standard" /> must not raise the latch, and
        /// <see cref="CardDiscardResult.MatchNotInPlay" /> is the more useful answer than
        /// <see cref="CardDiscardResult.DeckBusy" /> when both would apply. It also puts the two controllers on
        /// the same gate, so a card cannot be cycled out of hand in a phase it cannot be played in.
        /// </para>
        /// <para>
        /// Sits on the input path, once per player action, and allocates nothing on every path.
        /// </para>
        /// </remarks>
        /// <param name="playerId">The player discarding the card.</param>
        /// <param name="slotIndex">The zero-based hand slot to discard from.</param>
        /// <returns>Success once the card has left the hand and the cycle has rotated, or the specific reason the
        /// discard was rejected.</returns>
        public CardDiscardResult TryDiscardCard(int playerId, int slotIndex)
        {
            if (
                UnityReference.IsUnavailable(_cardCycle)
                || _deployController == null
                || _matchController == null
                || UnityReference.IsUnavailable(_discardLedger)
            )
            {
                return CardDiscardResult.DeckUnavailable;
            }

            if (_matchController.Phase != MatchPhase.Standard)
            {
                return CardDiscardResult.MatchNotInPlay;
            }

            if (_isDiscarding || _deployController.IsResolving)
            {
                return CardDiscardResult.DeckBusy;
            }

            if (!_cardCycle.TryGetHand(playerId, out _))
            {
                return CardDiscardResult.UnknownPlayer;
            }

            if (!_cardCycle.TryGetSlot(playerId, slotIndex, out _))
            {
                return CardDiscardResult.SlotOutOfRange;
            }

            _isDiscarding = true;

            try
            {
                if (!_discardLedger.TryPayForDiscard(playerId))
                {
                    return CardDiscardResult.InsufficientEnergy;
                }

                // The rotation reports the card it actually removed, read at the moment of the mutation. Taking it
                // from here rather than from the earlier slot read keeps the published fact correct even when the
                // charge itself disturbed the deck, which the ledger is free to do.
                if (!_cardCycle.TryAdvanceSlot(playerId, slotIndex, out CardId discardedCard))
                {
                    _discardLedger.RefundDiscard(playerId);
                    return CardDiscardResult.DeckUnavailable;
                }

                MatchEvents.RaiseCardDiscarded(playerId, discardedCard, slotIndex);

                return CardDiscardResult.Success;
            }
            finally
            {
                _isDiscarding = false;
            }
        }

        /// <remarks>
        /// Pushed by <see cref="MatchController" /> from its own injection rather than injected here, for the
        /// reason <c>DeployController.SetMatchController</c> states: VContainer refuses a dependency cycle at
        /// build time, so only the orchestrator's direction is registered.
        /// </remarks>
        internal void SetMatchController(MatchController matchController)
        {
            _matchController = matchController;
        }
    }
}
