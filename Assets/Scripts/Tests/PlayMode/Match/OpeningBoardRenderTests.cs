using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Events;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Match
{
    /// <remarks>
    /// Regression coverage for the GOOM-11 defect the unit-registry assertions could not see: the orchestrator
    /// seeded the opening position into <see cref="UnitPresenter" /> and nothing ever told
    /// <see cref="UnitView" /> to render it, so the board opened empty and stayed empty until the first move
    /// published an event the view listens to. Every fixture here asserts what is on screen, never what is in
    /// the registry — that is the whole point of it.
    /// </remarks>
    [TestFixture]
    public class OpeningBoardRenderTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const float CellVisualSize = 1f;
        private const string TroopCardId = "troop_alpha";

        private readonly List<Object> _spawned = new();

        private GameObject _boardGO;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private CardPresenter _cardPresenter;
        private DeckPresenter _deckPresenter;
        private EnergyPresenter _energyPresenter;
        private UnitView _unitView;
        private MatchInitializer _initializer;

        [SetUp]
        public void SetUp()
        {
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(gridLayout);

            var unitPrefabGO = new GameObject("UnitPrefab_OpeningBoardRender_Test");
            unitPrefabGO.AddComponent<SpriteRenderer>();
            unitPrefabGO.SetActive(false);
            _spawned.Add(unitPrefabGO);

            _boardGO = new GameObject("OpeningBoardRender_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _energyPresenter = _boardGO.AddComponent<EnergyPresenter>();
            _unitPresenter.Construct(_gridPresenter, _energyPresenter);

            _cardPresenter = _boardGO.AddComponent<CardPresenter>();
            CardDataSO troopCard = CreateCard(TroopCardId);
            _spawned.Add(troopCard);
            _cardPresenter.SetAuthoredCards(troopCard);

            _deckPresenter = _boardGO.AddComponent<DeckPresenter>();
            _deckPresenter.SetKit(BuildKit(), HandSize);

            _unitView = _boardGO.AddComponent<UnitView>();
            _unitView.Construct(_gridPresenter, _unitPresenter);
            _unitView.SetViewConfiguration(unitPrefabGO, null, null, null, CellVisualSize);

            _gridPresenter.SetGridLayout(gridLayout);
            _boardGO.SetActive(true);
            _spawned.Add(_boardGO);

            _initializer = new MatchInitializer(_gridPresenter, _unitPresenter, _cardPresenter, _deckPresenter, _energyPresenter);
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _spawned.Clear();
        }

        [Test]
        [Timeout(10000)]
        public void TryStartMatch_WithAnAuthoredOpeningPosition_RendersAVisualForEverySeededUnit()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                Placement(1, PlayerOneId, 4, -4),
                Placement(2, PlayerOneId, -4, 4),
                Placement(3, PlayerTwoId, 4, 0),
                Placement(4, PlayerTwoId, -4, 0)
            );
            MatchController matchController = BuildMatchController(config);

            // WHEN
            MatchStartResult result = matchController.TryStartMatch();

            // THEN — the count alone would pass on four visuals keyed to the wrong units, so two of the seeded
            // ids are looked up as well.
            Assert.That(
                (result, _unitView.RenderedUnitCount, _unitView.TryGetUnitVisual(1, out _), _unitView.TryGetUnitVisual(4, out _)),
                Is.EqualTo((MatchStartResult.Success, 4, true, true))
            );
        }

        [Test]
        [Timeout(10000)]
        public void TryStartMatch_SeedingAnOpeningPositionOverAPreviousOne_ReleasesTheVisualsOfTheUnitsThatLeft()
        {
            // GIVEN — a second orchestrator rather than a restart of the first, because the setup this asserts on
            // is the initializer's board clear, and a match already in Countdown refuses to be started again.
            BuildMatchController(BuildConfig(Placement(1, PlayerOneId, 4, -4), Placement(2, PlayerTwoId, -4, 0))).TryStartMatch();

            Assert.That(_unitView.RenderedUnitCount, Is.EqualTo(2), "Test setup expects the first opening position to have been rendered.");

            MatchController rematch = BuildMatchController(BuildConfig(Placement(7, PlayerOneId, 0, 0)));

            // WHEN
            rematch.TryStartMatch();

            // THEN
            Assert.That((_unitView.RenderedUnitCount, _unitView.TryGetUnitVisual(1, out _)), Is.EqualTo((1, false)));
        }

        private static StartingPlacement Placement(int unitId, int playerId, int q, int r)
        {
            return new StartingPlacement
            {
                CardId = TroopCardId,
                UnitId = unitId,
                PlayerId = playerId,
                Q = q,
                R = r,
            };
        }

        private static CardDataSO CreateCard(string cardId)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(cardId, cardId, "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);

            return card;
        }

        private KitDataSO BuildKit()
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                CardDataSO card = CreateCard($"kit_card_{i}");
                _spawned.Add(card);
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            return kit;
        }

        private MatchConfigSO BuildConfig(params StartingPlacement[] placements)
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(60f, 1f, 1f, placements);
            _spawned.Add(config);

            return config;
        }

        // Activated so the countdown Awaitable runs, though nothing here waits for it: seeding and the
        // MatchStarted announcement both happen inside TryStartMatch, before the first tick is awaited.
        private MatchController BuildMatchController(MatchConfigSO config)
        {
            var go = new GameObject("MatchController_OpeningBoardRender_Test");
            go.SetActive(false);
            MatchController controller = go.AddComponent<MatchController>();
            controller.SetMatchConfigForTests(config, matchSeed: 0, isAutoStartEnabled: false);
            controller.Construct(_initializer, _unitPresenter, BuildBareComponent<DeployController>(), BuildBareComponent<CardDiscardController>());
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        // Never Constructed and never activated: MatchController.Construct only needs a non-null reference to
        // push itself into, and this fixture never plays or discards a card.
        private T BuildBareComponent<T>()
            where T : Component
        {
            var go = new GameObject($"{typeof(T).Name}_Bare_OpeningBoardRender_Test");
            T component = go.AddComponent<T>();
            _spawned.Add(go);

            return component;
        }
    }
}
