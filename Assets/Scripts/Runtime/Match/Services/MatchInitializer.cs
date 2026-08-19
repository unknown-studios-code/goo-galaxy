using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Match.Services
{
    /// <summary>
    /// Puts a match on the board: clears whatever the last one left, seeds the authored opening position,
    /// announces the board and the match, and deals both players their energy and their hand.
    /// </summary>
    /// <remarks>
    /// A plain class rather than a component, because none of this is per-frame and none of it belongs to a
    /// GameObject — it is the one place the five presenters a match needs are composed, and constructor
    /// injection is what keeps that composition visible. <c>MatchController</c> owns the phase and calls this;
    /// this owns setup and knows nothing about phases.
    /// <para>
    /// <b>The order of the steps is load-bearing and is documented step by step in the body.</b> Nothing here
    /// is safe to reorder for tidiness.
    /// </para>
    /// </remarks>
    public sealed class MatchInitializer
    {
        // Spawned units are numbered from here up, above every id the opening position is allowed to author,
        // so a Clone can never collide with a seeded unit. StartingPlacement's tooltip states the same bound
        // from the authoring side.
        private const int FirstSpawnedUnitId = 1000;

        // The GDD's opening is two units per player; a sizing hint rather than a rule.
        private const int ExpectedPlacementCount = 4;

        private readonly GridPresenter _gridPresenter;
        private readonly UnitPresenter _unitPresenter;
        private readonly CardPresenter _cardPresenter;
        private readonly DeckPresenter _deckPresenter;
        private readonly EnergyPresenter _energyPresenter;

        private readonly List<int> _unitIdBuffer = new(ExpectedPlacementCount);
        private readonly List<int> _seededUnitIds = new(ExpectedPlacementCount);
        private readonly HashSet<int> _claimedUnitIds = new();
        private readonly Dictionary<CardId, CardDefinition> _cardDefinitions = new(ExpectedPlacementCount);

        public MatchInitializer(
            GridPresenter gridPresenter,
            UnitPresenter unitPresenter,
            CardPresenter cardPresenter,
            DeckPresenter deckPresenter,
            EnergyPresenter energyPresenter
        )
        {
            _gridPresenter = gridPresenter;
            _unitPresenter = unitPresenter;
            _cardPresenter = cardPresenter;
            _deckPresenter = deckPresenter;
            _energyPresenter = energyPresenter;
        }

        /// <summary>
        /// Sets a match up on the board and announces it. Publishes nothing and seeds nothing for any
        /// non-<see cref="MatchStartResult.Success" /> result.
        /// </summary>
        /// <remarks>
        /// Reports rather than logs its missing dependencies: this class holds presenters but is not itself a
        /// <c>UnityEngine.Object</c>, so the caller writes that message with itself as the console context. The
        /// starting-placement faults are the exception — the authored index they name cannot be recovered from
        /// the returned code, so they are logged here, each against the asset the reader has to open.
        /// </remarks>
        /// <param name="matchConfig">The authored match shape. Must not be null.</param>
        /// <param name="configuration">
        /// The settled configuration to announce, which also names the two players to deal decks to.
        /// </param>
        /// <returns>Success once the board is seeded and both players are dealt, or the reason setup stopped.</returns>
        public MatchStartResult InitializeMatch(MatchConfigSO matchConfig, in MatchConfiguration configuration)
        {
            if (matchConfig == null)
            {
                return MatchStartResult.ConfigMissing;
            }

            if (_gridPresenter == null || _unitPresenter == null || _cardPresenter == null || _deckPresenter == null || _energyPresenter == null)
            {
                return MatchStartResult.DomainUnavailable;
            }

            // 1. The grid, first, because every later step reads it: the placements are validated against its
            //    cells and the announcement below hands it to the whole scene.
            HexGrid grid = _gridPresenter.HexGrid;

            if (grid == null)
            {
                Debug.LogError(MatchLogMessages.MatchGridUnavailable, _gridPresenter);
                return MatchStartResult.DomainUnavailable;
            }

            // 2. Clear the previous match. Occupancy is what the placements are validated against, so a board
            //    still holding last match's units would reject every hex the opening position names.
            ClearBoard();

            // 3. A new spawner rather than a reset one, so spawned ids restart from the same base every match
            //    and a stale id can never collide with a unit seeded below. Set before seeding because the
            //    registry re-arms its spawn-failure log here.
            _unitPresenter.SetUnitSpawner(new MatchUnitSpawner(_cardPresenter, FirstSpawnedUnitId));

            // 4. Seed the opening position, all or nothing.
            MatchStartResult seeding = SeedStartingPlacements(matchConfig, grid);

            if (seeding != MatchStartResult.Success)
            {
                return seeding;
            }

            // 5. Announce the board. GridPresenter builds it in Awake and deliberately publishes nothing, so
            //    this is the only GridInitialized a scene load sees — raised here, from Start-time setup, it
            //    reaches views that registered in OnEnable rather than going out before they existed.
            MatchEvents.RaiseGridInitialized(grid);

            // 6. Announce the match, and do it BEFORE dealing. DeckPresenter captures the shuffle seed from
            //    this event and DeployController drops its memoized card definitions on it; dealing first would
            //    shuffle from the previous match's seed and play from the previous match's authored values.
            MatchEvents.RaiseMatchStarted(configuration);

            // 7. Energy before cards, so a hand is never dealt to a player who has no balance to play it from.
            _energyPresenter.InitializeMatch();

            // 8. Deal both players. Named by the configuration rather than invented here — DeckPresenter has no
            //    opinion about who is playing, and neither does this.
            _deckPresenter.InitializePlayer(configuration.PlayerOneId);
            _deckPresenter.InitializePlayer(configuration.PlayerTwoId);

            return MatchStartResult.Success;
        }

        private void ClearBoard()
        {
            _unitIdBuffer.Clear();

            // PERF: ActiveUnitValues, not ActiveUnits.Keys — the interface-typed collection boxes its backing
            // struct enumerator, the same trade-off MatchScoreCounter's <remarks> states for its own passes.
            //
            // The ids are copied out first because unregistering mutates the dictionary this walks.
            foreach (GridUnit unit in _unitPresenter.ActiveUnitValues)
            {
                if (unit != null)
                {
                    _unitIdBuffer.Add(unit.UnitId);
                }
            }

            for (int i = 0; i < _unitIdBuffer.Count; i++)
            {
                _unitPresenter.UnregisterUnit(_unitIdBuffer[i]);
            }
        }

        // Validates and registers in one pass, rolling every unit back the moment one placement fails. Two
        // passes would have to re-derive intra-batch collisions itself; one pass lets the board answer them —
        // a second placement on a hex the first already took reads as occupied, which is exactly what it is.
        private MatchStartResult SeedStartingPlacements(MatchConfigSO matchConfig, HexGrid grid)
        {
            IReadOnlyList<StartingPlacement> placements = matchConfig.StartingPlacements;

            _seededUnitIds.Clear();
            _claimedUnitIds.Clear();

            // A definition is an immutable copy of an asset a designer can edit between matches, so the cache
            // is dropped per match for the same reason DeployController drops its own on MatchStarted.
            _cardDefinitions.Clear();

            for (int i = 0; i < placements.Count; i++)
            {
                StartingPlacement placement = placements[i];

                if (!TrySeedPlacement(grid, placement, i))
                {
                    RollBackSeededUnits();
                    return MatchStartResult.InvalidPlacement;
                }
            }

            return MatchStartResult.Success;
        }

        private bool TrySeedPlacement(HexGrid grid, StartingPlacement placement, int placementIndex)
        {
            if (string.IsNullOrEmpty(placement.CardId))
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementCardMissingFormat, placementIndex, placement.CardId), _cardPresenter);
                return false;
            }

            var cardId = new CardId(placement.CardId);

            if (!_cardPresenter.TryGetCard(cardId, out ICardData card))
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementCardMissingFormat, placementIndex, placement.CardId), _cardPresenter);
                return false;
            }

            // Checked before the hex, because re-registering an id is not refused by the registry — it releases
            // the earlier unit's cell and takes its place, which would drop a unit rather than report a fault.
            if (!_claimedUnitIds.Add(placement.UnitId))
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementDuplicateUnitIdFormat, placementIndex, placement.UnitId), _unitPresenter);
                return false;
            }

            var coordinates = new HexCoordinates(placement.Q, placement.R);

            if (!grid.TryGetCell(coordinates, out HexCell cell))
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementOffGridFormat, placementIndex, coordinates), _gridPresenter);
                return false;
            }

            if (cell.IsBlocked)
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementBlockedFormat, placementIndex, coordinates), _gridPresenter);
                return false;
            }

            if (cell.IsOccupied)
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementOccupiedFormat, placementIndex, coordinates), _gridPresenter);
                return false;
            }

            var unit = new GridUnit(placement.UnitId, placement.PlayerId, cardId, coordinates, card.HasArmor);

            if (!_unitPresenter.RegisterUnit(unit, GetCardDefinition(card)))
            {
                Debug.LogError(string.Format(MatchLogMessages.StartingPlacementRegistrationFailedFormat, placementIndex), _unitPresenter);
                return false;
            }

            _seededUnitIds.Add(placement.UnitId);

            return true;
        }

        private void RollBackSeededUnits()
        {
            for (int i = 0; i < _seededUnitIds.Count; i++)
            {
                _unitPresenter.UnregisterUnit(_seededUnitIds[i]);
            }

            _seededUnitIds.Clear();
        }

        // CardDefinition is the only type carrying a card's authored data and the board's capability contracts
        // at once, so it is what the registry stores for a seeded unit — the same object DeployController hands
        // the board for a played one. Shared between placements of the same card because it is immutable.
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
    }
}
