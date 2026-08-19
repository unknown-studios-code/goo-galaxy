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
    /// Regression coverage for the GOOM-11 fix: <c>GridPresenter.Awake</c> used to raise
    /// <c>MatchEvents.GridInitialized</c> itself, but every subscriber registers in <c>OnEnable</c>, so on a cold
    /// scene load the event went out before <see cref="GridView" /> had subscribed and no cell was ever built.
    /// <see cref="GridPresenter" /> now builds silently and <see cref="MatchInitializer" /> publishes the
    /// announcement, from the setup sequence <see cref="MatchController" /> drives — this fixture proves that
    /// hand-off without re-raising anything itself.
    /// </remarks>
    [TestFixture]
    public class GridInitializationOrderTests
    {
        private const int BoardRadius = 4;
        private const int HandSize = 4;
        private const float CellVisualSize = 1f;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            // Before the destroys, not after: Object.Destroy is deferred to end of frame in PlayMode, so the
            // GridView and MatchController this fixture builds stay subscribed to the static bus past teardown.
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
        public void ColdSceneLoad_MatchStarted_GridViewBuildsItsCellsWithoutAnyManualReRaise()
        {
            // GIVEN
            var boardGO = new GameObject("GridInitializationOrder_Board_Test");
            boardGO.SetActive(false);

            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(gridLayout);

            GridPresenter gridPresenter = boardGO.AddComponent<GridPresenter>();
            UnitPresenter unitPresenter = boardGO.AddComponent<UnitPresenter>();
            EnergyPresenter energyPresenter = boardGO.AddComponent<EnergyPresenter>();
            unitPresenter.Construct(gridPresenter, energyPresenter);
            gridPresenter.SetGridLayout(gridLayout);

            var cellPrefabGO = new GameObject("CellPrefab_GridInitializationOrder_Test");
            cellPrefabGO.SetActive(false);
            CellView cellPrefab = cellPrefabGO.AddComponent<CellView>();
            _spawned.Add(cellPrefabGO);

            GridView gridView = boardGO.AddComponent<GridView>();
            gridView.SetViewConfiguration(cellPrefab, CellVisualSize);

            CardPresenter cardPresenter = boardGO.AddComponent<CardPresenter>();

            DeckPresenter deckPresenter = boardGO.AddComponent<DeckPresenter>();
            deckPresenter.SetKit(BuildKit(), HandSize);

            var initializer = new MatchInitializer(gridPresenter, unitPresenter, cardPresenter, deckPresenter, energyPresenter);

            MatchConfigSO matchConfig = ScriptableObject.CreateInstance<MatchConfigSO>();
            matchConfig.SetAuthoredData(60f, 1f, 1f, 3f);
            _spawned.Add(matchConfig);

            var matchControllerGO = new GameObject("MatchController_GridInitializationOrder_Test");
            matchControllerGO.SetActive(false);
            MatchController matchController = matchControllerGO.AddComponent<MatchController>();
            matchController.SetMatchConfigForTests(matchConfig, matchSeed: 0, isAutoStartEnabled: false);
            matchController.Construct(initializer, unitPresenter, BuildBareDeployController(), BuildBareCardDiscardController(), energyPresenter);
            _spawned.Add(matchControllerGO);

            boardGO.SetActive(true);
            matchControllerGO.SetActive(true);
            _spawned.Add(boardGO);

            // WHEN
            MatchStartResult result = matchController.TryStartMatch();

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.Success));
            Assert.That(gridView.CellViews.Count, Is.GreaterThan(0), "GridView never built a cell, so it was still deaf when GridInitialized was raised.");
        }

        private KitDataSO BuildKit()
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
                card.SetAuthoredData($"kit_card_{i}", $"kit_card_{i}", "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);
                _spawned.Add(card);
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            return kit;
        }

        private DeployController BuildBareDeployController()
        {
            var go = new GameObject("DeployController_Bare_GridInitializationOrder_Test");
            DeployController controller = go.AddComponent<DeployController>();
            _spawned.Add(go);

            return controller;
        }

        private CardDiscardController BuildBareCardDiscardController()
        {
            var go = new GameObject("CardDiscardController_Bare_GridInitializationOrder_Test");
            CardDiscardController controller = go.AddComponent<CardDiscardController>();
            _spawned.Add(go);

            return controller;
        }
    }
}
