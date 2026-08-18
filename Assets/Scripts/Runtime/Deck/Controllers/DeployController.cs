using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Deck.Controllers
{
    /// <summary>
    /// The single entry point for playing a card from hand: it reads the slot, resolves the card, sends it down
    /// the troop or the Protocol path, and advances the player's cycle only once the board has accepted the play.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the reason <c>Runtime.Deck</c> is the first feature assembly in the project to reference another
    /// one. Playing a card needs the authored card data from <c>Runtime.Cards</c> and the board from
    /// <c>Runtime.Board</c> at the same moment, and this component exists to compose the two so that neither has
    /// to know the other exists: Cards still authors data with no idea what a hex is, and Board still resolves
    /// moves with no reference to Cards, receiving the capability it needs as a parameter.
    /// </para>
    /// <para>
    /// The arrangement is expected to be temporary. This controller migrates to a Match Orchestrator assembly
    /// with GOOM-11 and takes the <c>Runtime.Board</c> reference with it, leaving <c>Runtime.Deck</c> depending
    /// on <c>Runtime.Cards</c> alone. Keep board-facing logic in this file rather than spreading it through the
    /// assembly, so that move is a file move.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class DeployController : MonoBehaviour
    {
        // A troop deploys onto exactly one hex. A Protocol's cluster size is authored on the card and validated
        // by the board, so only "at least one" can be checked here.
        private const int TroopTargetCount = 1;

        private const int CardDefinitionCapacity = 8;

        private readonly Dictionary<CardId, CardDefinition> _cardDefinitions = new(CardDefinitionCapacity);

        private DeckPresenter _deckPresenter;
        private CardPresenter _cardPresenter;
        private UnitPresenter _unitPresenter;
        private AbilityController _abilityController;
        private IEnergyLedger _energyLedger;
        private bool _isResolving;

        /// <remarks>
        /// True from the moment <see cref="TryPlayCard"/> clears its dependency and re-entrancy checks until it
        /// returns, on every path including a rejection. Exists so a discard raised from an event subscriber
        /// cannot rotate the hand mid-play: <see cref="TryPlayCard"/> reads the slot at the top of its body and
        /// advances that same index at the bottom, and a rotation landing in between would advance a slot whose
        /// card is no longer the one that was played, cycling an extra card out of the hand. Assembly-internal
        /// because the only other reader is <c>CardDiscardController</c>, in this same assembly.
        /// </remarks>
        internal bool IsResolving => _isResolving;

        /// <remarks>
        /// The ledger is held as the <c>Runtime.Shared</c> interface, so playing a card creates no dependency on
        /// the Energy assembly.
        /// </remarks>
        [Inject]
        public void Construct(
            DeckPresenter deckPresenter,
            CardPresenter cardPresenter,
            UnitPresenter unitPresenter,
            AbilityController abilityController,
            IEnergyLedger energyLedger
        )
        {
            Debug.Assert(deckPresenter != null, DeckLogMessages.DeployDeckPresenterMissing, this);
            Debug.Assert(cardPresenter != null, DeckLogMessages.DeployCardPresenterMissing, this);
            Debug.Assert(unitPresenter != null, BoardLogMessages.UnitPresenterMissing, this);
            Debug.Assert(abilityController != null, DeckLogMessages.DeployAbilityControllerMissing, this);
            Debug.Assert(energyLedger != null, DeckLogMessages.DeployEnergyLedgerMissing, this);

            _deckPresenter = deckPresenter;
            _cardPresenter = cardPresenter;
            _unitPresenter = unitPresenter;
            _abilityController = abilityController;
            _energyLedger = energyLedger;
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
        }

        /// <summary>
        /// Plays the card in one of a player's hand slots onto the hexes they picked, and rotates that slot when
        /// the board accepts it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Energy is paid in two different places, and that asymmetry is deliberate.</b> A troop is charged by
        /// the board: <c>UnitPresenter.ResolveDeploy</c> runs validate-charge-mutate-refund as one block, exactly
        /// as it does for a Clone or a Jump, so nothing is charged here and charging here would double it. A
        /// Protocol is charged here: <c>AbilityController.ResolveSpell</c> never touches Energy — its own
        /// contract says paying belongs to the caller — so this method pays before the call and refunds on every
        /// way out of it, including an escaping exception. Both paths therefore end in the same guarantee: a
        /// rejected play leaves the player's balance exactly where it was.
        /// </para>
        /// <para>
        /// <b>The cycle advances only on <see cref="CardPlayResult.Success" />.</b> Every rejection leaves the
        /// hand exactly as it was, so an illegal target costs the player nothing — not the card, not the Energy.
        /// A refused rotation after the board has already accepted the play would mean the deck vanished
        /// mid-play, which nothing in this codebase can currently cause; it is reported as
        /// <see cref="CardPlayResult.BoardUnavailable" /> rather than a silent no-op.
        /// </para>
        /// <para>
        /// Checks run in a fixed order, so the returned code is predictable when several would fail at once: the
        /// injected deck, then re-entrancy, then the player, then the slot, then the card, then the target count,
        /// then whatever the board decides. A play attempted from inside another play's event dispatch is
        /// rejected with <see cref="CardPlayResult.ResolverBusy" /> ahead of all but the first — see
        /// <see cref="IsResolving" />.
        /// </para>
        /// <para>
        /// Sits on the input path, once per player action, and allocates nothing after the first play of each
        /// distinct card.
        /// </para>
        /// </remarks>
        /// <param name="playerId">The player playing the card.</param>
        /// <param name="slotIndex">The zero-based hand slot they played from.</param>
        /// <param name="targets">
        /// The hexes they picked — exactly one for a troop, the card's authored cluster for a Protocol, centre
        /// first. Borrowed for the duration of the call and never retained, so the caller may reuse the buffer.
        /// </param>
        /// <returns>Success once the board has applied the play and the hand has rotated, or the specific reason
        /// the play was rejected.</returns>
        public CardPlayResult TryPlayCard(int playerId, int slotIndex, IReadOnlyList<HexCoordinates> targets)
        {
            if (_deckPresenter == null)
            {
                return CardPlayResult.BoardUnavailable;
            }

            // Checked, not merely raised: a nested play from an event subscriber would otherwise clear this flag
            // in its own finally while the outer play still sits between its slot read and its rotation, and a
            // discard resolved in that window would rotate the hand out from under it.
            if (_isResolving)
            {
                return CardPlayResult.ResolverBusy;
            }

            _isResolving = true;

            try
            {
                // Asked before the slot, and only for the player's existence, so an unknown player and an unknown
                // slot stay distinguishable: TryGetSlot alone answers false for both.
                if (!_deckPresenter.TryGetHand(playerId, out _))
                {
                    return CardPlayResult.UnknownPlayer;
                }

                if (!_deckPresenter.TryGetSlot(playerId, slotIndex, out CardId cardId))
                {
                    return CardPlayResult.SlotOutOfRange;
                }

                if (_cardPresenter == null || !_cardPresenter.TryGetCard(cardId, out ICardData card))
                {
                    return CardPlayResult.CardNotFound;
                }

                // Troop is every branch but Spell, because CardType.Troop is zero and is therefore what an asset
                // whose type was never authored deserializes to. A troop play is fully validated by the board, so a
                // mis-authored card is rejected on the rules rather than on a type the HUD cannot see.
                CardPlayResult result = card.Type == CardType.Spell ? PlaySpell(playerId, cardId, card, targets) : PlayTroop(playerId, cardId, card, targets);

                if (result != CardPlayResult.Success)
                {
                    return result;
                }

                if (!_deckPresenter.TryAdvanceSlot(playerId, slotIndex, out _))
                {
                    return CardPlayResult.BoardUnavailable;
                }

                return CardPlayResult.Success;
            }
            finally
            {
                _isResolving = false;
            }
        }

        // The whole of the board-to-HUD translation, kept in one place so the grouping is auditable against the
        // board's own enums instead of being rediscovered at each call site.
        //
        // Every code a player can act on is preserved. The rest collapse into IllegalPlacement, because the
        // difference between "that hex is occupied", "that hex is outside your territory" and "this card cannot
        // land there" is a distinction the board makes and a HUD cannot usefully render — all three mean "pick
        // another hex". SpawnFailed and InvalidCommand are not player mistakes at all: they are wiring faults
        // the board already logs, so they read as BoardUnavailable rather than as something the player did.
        private static CardPlayResult MapMovementResult(MovementResult result)
        {
            return result switch
            {
                MovementResult.Success => CardPlayResult.Success,
                MovementResult.InsufficientEnergy => CardPlayResult.InsufficientEnergy,
                MovementResult.ResolverBusy => CardPlayResult.ResolverBusy,
                MovementResult.BoardUnavailable or MovementResult.SpawnFailed or MovementResult.InvalidCommand => CardPlayResult.BoardUnavailable,
                _ => CardPlayResult.IllegalPlacement,
            };
        }

        // The Protocol half of the mapping above, and the reason InvalidTargets does not become
        // InvalidTargetCount: the board rejects a cluster for its shape, its position and its size at once, so
        // the count is not recoverable from the code. InvalidTargetCount stays reserved for the count this
        // controller checks itself. CardHasNoImpacts is an authoring fault with no player-facing distinction
        // either — the card does nothing where it was aimed.
        private static CardPlayResult MapSpellResult(SpellResult result)
        {
            return result switch
            {
                SpellResult.Success => CardPlayResult.Success,
                SpellResult.BoardUnavailable => CardPlayResult.BoardUnavailable,
                SpellResult.ResolverBusy => CardPlayResult.ResolverBusy,
                _ => CardPlayResult.IllegalPlacement,
            };
        }

        private CardPlayResult PlayTroop(int playerId, CardId cardId, ICardData card, IReadOnlyList<HexCoordinates> targets)
        {
            if (targets == null || targets.Count != TroopTargetCount)
            {
                return CardPlayResult.InvalidTargetCount;
            }

            if (_unitPresenter == null)
            {
                return CardPlayResult.BoardUnavailable;
            }

            var command = MoveCommand.ForDeploy(targets[0], playerId);

            // The board charges the card's Energy itself, inside the same block that validates and mutates.
            return MapMovementResult(_unitPresenter.ResolveDeploy(in command, cardId, GetCardDefinition(card)));
        }

        private CardPlayResult PlaySpell(int playerId, CardId cardId, ICardData card, IReadOnlyList<HexCoordinates> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return CardPlayResult.InvalidTargetCount;
            }

            if (_abilityController == null || UnityReference.IsUnavailable(_energyLedger))
            {
                return CardPlayResult.BoardUnavailable;
            }

            // Priced as a Deploy, which is what makes a Protocol cost the card's whole authored Energy — the
            // same rule the board applies to a troop being deployed.
            if (!_energyLedger.TryPayForMove(playerId, MoveType.Deploy, card.EnergyCost))
            {
                return CardPlayResult.InsufficientEnergy;
            }

            // Cleared only once the spell is committed, so the refund below covers every way out of the block —
            // each of the four rejection codes and any exception that escapes it — rather than resting on the
            // callee catching its own. The ledger re-derives the price from the same three arguments, so the net
            // change over a rejected Protocol is zero.
            bool isChargeOutstanding = true;

            try
            {
                var command = new SpellCommand(playerId, cardId, targets);
                SpellResult resolution = _abilityController.ResolveSpell(in command, GetCardDefinition(card));

                if (resolution != SpellResult.Success)
                {
                    return MapSpellResult(resolution);
                }

                isChargeOutstanding = false;
            }
            finally
            {
                if (isChargeOutstanding)
                {
                    _energyLedger.RefundMove(playerId, MoveType.Deploy, card.EnergyCost);
                }
            }

            return CardPlayResult.Success;
        }

        // CardDefinition is the only type that carries a card's authored data and the board's capability
        // contracts at once — CardDataSO implements ICardData alone — so it is what bridges the two assemblies
        // this controller composes.
        //
        // PERF: memoized. One definition is built the first time each distinct card is played and reused for
        // every later play of it, which is what keeps TryPlayCard allocation-free on the input path; building
        // one per play would allocate a definition and an impact array per player action.
        private CardDefinition GetCardDefinition(ICardData card)
        {
            if (_cardDefinitions.TryGetValue(card.CardId, out CardDefinition definition))
            {
                return definition;
            }

            definition = new CardDefinition(card);
            _cardDefinitions[card.CardId] = definition;

            return definition;
        }

        // A definition is an immutable copy of an asset that a designer can edit between matches, and domain
        // reload is disabled, so a cache kept across a rematch would resolve the previous session's authored
        // values. Dropping it at match start is what keeps an Inspector edit visible in the next match.
        private void HandleMatchStarted(MatchConfiguration config)
        {
            _cardDefinitions.Clear();
        }
    }
}
