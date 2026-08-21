using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Match
{
    [TestFixture]
    public class MatchInitializerTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const string TroopCardId = "troop_alpha";
        private const string UnregisteredCardId = "unregistered_card";

        private readonly List<Object> _spawned = new();

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private CardPresenter _cardPresenter;
        private DeckPresenter _deckPresenter;
        private EnergyPresenter _energyPresenter;
        private MatchInitializer _initializer;

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
        public void InitializeMatch_ValidStartingPlacements_SeedsFourUnitsAtTheAuthoredCoordinatesWithTheAuthoredOwners()
        {
            // GIVEN
            BuildBoard();
            MatchConfigSO config = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 4,
                    R = -4,
                },
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 2,
                    PlayerId = PlayerOneId,
                    Q = -4,
                    R = 4,
                },
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 3,
                    PlayerId = PlayerTwoId,
                    Q = 4,
                    R = 0,
                },
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 4,
                    PlayerId = PlayerTwoId,
                    Q = -4,
                    R = 0,
                }
            );

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(config, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.Success));
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(4));
            Assert.That(_unitPresenter.ActiveUnits[1].PlayerId, Is.EqualTo(PlayerOneId));
            Assert.That(_unitPresenter.ActiveUnits[3].PlayerId, Is.EqualTo(PlayerTwoId));
            Assert.That(GetCell(new HexCoordinates(4, -4)).OccupantUnitId, Is.EqualTo(1));
            Assert.That(GetCell(new HexCoordinates(4, 0)).OccupantUnitId, Is.EqualTo(3));
        }

        [Test]
        public void InitializeMatch_ValidStartingPlacements_PublishesGridInitializedThenMatchStartedThenEnergyChangedThenHandChanged()
        {
            // GIVEN
            BuildBoard();
            MatchConfigSO config = BuildMatchConfig();
            var eventOrder = new List<string>();
            MatchEvents.GridInitialized += _ => eventOrder.Add("GridInitialized");
            MatchEvents.MatchStarted += _ => eventOrder.Add("MatchStarted");
            MatchEvents.EnergyChanged += (_, _) => eventOrder.Add("EnergyChanged");
            MatchEvents.HandChanged += (_, _, _) => eventOrder.Add("HandChanged");

            // WHEN
            _initializer.InitializeMatch(config, BuildConfiguration());

            // THEN
            Assert.That(eventOrder, Is.EqualTo(new[] { "GridInitialized", "MatchStarted", "EnergyChanged", "EnergyChanged", "HandChanged", "HandChanged" }));
        }

        [Test]
        public void InitializeMatch_CalledAgainWithDifferentPlacements_ClearsThePreviousBoardBeforeSeedingTheNewOne()
        {
            // GIVEN
            BuildBoard();
            MatchConfigSO firstConfig = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 0,
                    R = 0,
                }
            );
            _initializer.InitializeMatch(firstConfig, BuildConfiguration());
            MatchConfigSO secondConfig = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 2,
                    PlayerId = PlayerTwoId,
                    Q = 1,
                    R = 0,
                }
            );

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(secondConfig, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.Success));
            Assert.That(_unitPresenter.ActiveUnits.Count, Is.EqualTo(1));
            Assert.That(_unitPresenter.ActiveUnits.ContainsKey(2), Is.True);
        }

        [Test]
        public void InitializeMatch_PlacementNamesACardAbsentFromTheRoster_ReturnsInvalidPlacementAndSeedsNothing()
        {
            // GIVEN
            BuildBoard();
            MatchConfigSO config = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = UnregisteredCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 0,
                    R = 0,
                }
            );
            LogAssert.Expect(LogType.Error, string.Format(MatchLogMessages.StartingPlacementCardMissingFormat, 0, UnregisteredCardId));

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(config, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.InvalidPlacement));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
        }

        [Test]
        public void InitializeMatch_PlacementTargetsABlockedHex_ReturnsInvalidPlacementAndSeedsNothing()
        {
            // GIVEN
            BuildBoard(new Vector2Int(0, 0));
            MatchConfigSO config = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 0,
                    R = 0,
                }
            );
            LogAssert.Expect(LogType.Error, string.Format(MatchLogMessages.StartingPlacementBlockedFormat, 0, new HexCoordinates(0, 0)));

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(config, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.InvalidPlacement));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
        }

        [Test]
        public void InitializeMatch_PlacementReusesAUnitId_ReturnsInvalidPlacementAndSeedsNothing()
        {
            // GIVEN
            BuildBoard();
            MatchConfigSO config = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 0,
                    R = 0,
                },
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerTwoId,
                    Q = 1,
                    R = 0,
                }
            );
            LogAssert.Expect(LogType.Error, string.Format(MatchLogMessages.StartingPlacementDuplicateUnitIdFormat, 1, 1));

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(config, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.InvalidPlacement));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
        }

        [Test]
        public void InitializeMatch_TwoPlacementsTargetTheSameHex_ReturnsInvalidPlacementAndSeedsNothing()
        {
            // GIVEN
            BuildBoard();
            MatchConfigSO config = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 0,
                    R = 0,
                },
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 2,
                    PlayerId = PlayerTwoId,
                    Q = 0,
                    R = 0,
                }
            );
            LogAssert.Expect(LogType.Error, string.Format(MatchLogMessages.StartingPlacementOccupiedFormat, 1, new HexCoordinates(0, 0)));

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(config, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.InvalidPlacement));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
        }

        [Test]
        public void InitializeMatch_RefusedAfterAPriorSuccessfulMatch_LeavesTheBoardEmptyRatherThanRestoringThePreviousOne()
        {
            // GIVEN — the initializer clears the board before validating, so a refused restart must not restore
            // the previous match's units, which is the surprising half of this behaviour.
            BuildBoard();
            MatchConfigSO firstConfig = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = TroopCardId,
                    UnitId = 1,
                    PlayerId = PlayerOneId,
                    Q = 0,
                    R = 0,
                }
            );
            _initializer.InitializeMatch(firstConfig, BuildConfiguration());
            MatchConfigSO refusedConfig = BuildMatchConfig(
                new StartingPlacement
                {
                    CardId = UnregisteredCardId,
                    UnitId = 2,
                    PlayerId = PlayerTwoId,
                    Q = 1,
                    R = 0,
                }
            );
            LogAssert.Expect(LogType.Error, string.Format(MatchLogMessages.StartingPlacementCardMissingFormat, 0, UnregisteredCardId));

            // WHEN
            MatchStartResult result = _initializer.InitializeMatch(refusedConfig, BuildConfiguration());

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.InvalidPlacement));
            Assert.That(_unitPresenter.ActiveUnits, Is.Empty);
        }

        private static CardDataSO CreateCard(string cardId)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(cardId, cardId, "Test description.", CardType.Troop, 1, false, false, false, false, 1, null);

            return card;
        }

        private static MatchConfiguration BuildConfiguration()
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(PlayerOneId, PlayerControl.LocalHuman),
                new PlayerSlot(PlayerTwoId, PlayerControl.LocalHuman),
                180f,
                3f,
                60f
            );
        }

        private void BuildBoard(params Vector2Int[] blockedCoordinates)
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius, blockedCoordinates);
            _spawned.Add(_gridLayout);

            _boardGO = new GameObject("MatchInitializer_Board_Test");
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

            _gridPresenter.SetGridLayout(_gridLayout);
            _boardGO.SetActive(true);
            _spawned.Add(_boardGO);

            _initializer = new MatchInitializer(_gridPresenter, _unitPresenter, _cardPresenter, _deckPresenter, _energyPresenter);
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

        private MatchConfigSO BuildMatchConfig(params StartingPlacement[] placements)
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(180f, 3f, 60f, 3f, placements);
            _spawned.Add(config);

            return config;
        }

        private HexCell GetCell(HexCoordinates coordinates)
        {
            HexGrid grid = _gridPresenter.HexGrid;

            Assert.That(grid, Is.Not.Null, "Test setup expects the grid presenter to have initialized its hex grid.");
            Assert.That(grid.TryGetCell(coordinates, out HexCell cell), Is.True, $"Test expects {coordinates} to exist on the grid.");

            return cell;
        }
    }
}
