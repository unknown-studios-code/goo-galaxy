using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
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
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Match
{
    [TestFixture]
    public class MatchDominationTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        // Not a real bound — [Timeout] on each test is what actually bounds the wait. This only backstops
        // against an infinite loop if the awaited event never fires at all.
        private const int PollFrameBudget = 20000;
        private const int SettleFrameBudget = 60;
        private const string TroopCardId = "troop_alpha";
        private const string ScoreChangedPlayerOneEventName = "ScoreChanged:P1";
        private const string ScoreChangedPlayerTwoEventName = "ScoreChanged:P2";
        private const string MatchEndedEventName = "MatchEnded";

        private static readonly HexCoordinates _playerOneUnitA = new(0, 0);
        private static readonly HexCoordinates _playerOneUnitB = new(1, 0);
        private static readonly HexCoordinates _playerTwoUnit = new(-1, 0);
        private static readonly HexCoordinates _conversionLandingSourceCoordinates = new(3, 0);

        private readonly List<Object> _spawned = new();
        private readonly List<string> _eventOrder = new();

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private CardPresenter _cardPresenter;
        private DeckPresenter _deckPresenter;
        private EnergyPresenter _energyPresenter;
        private MatchInitializer _initializer;
        private DeployController _deployController;
        private CardDiscardController _cardDiscardController;

        [SetUp]
        public void SetUp()
        {
            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(_gridLayout);

            _boardGO = new GameObject("MatchDomination_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _energyPresenter = _boardGO.AddComponent<EnergyPresenter>();
            _unitPresenter.Construct(_gridPresenter, _energyPresenter);
            _boardGO.AddComponent<ConversionController>().Construct(_gridPresenter, _unitPresenter);

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
            _deployController = BuildBareComponent<DeployController>("DeployController_Bare_Test");
            _cardDiscardController = BuildBareComponent<CardDiscardController>("CardDiscardController_Bare_Test");

            _eventOrder.Clear();
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

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator LateUpdate_OnePlayerReducedToZeroLiveUnitsDuringStandard_PublishesDominationToTheSurvivor()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(60f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerOneId, 1, 0), Placement(3, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Standard) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Standard), "Test setup expects Standard to have been reached.");

            // WHEN — FuseExpired only flips MatchController's dirty flag, so it is raised for the same reason
            // MatchControllerTests raises it: to force the recount that follows a real removal.
            _unitPresenter.UnregisterUnit(3);
            MatchEvents.RaiseFuseExpired(3, PlayerTwoId);

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((outcome.Value.WinnerPlayerId, outcome.Value.Reason), Is.EqualTo((PlayerOneId, MatchEndReason.Domination)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator LateUpdate_OnePlayerReducedToZeroLiveUnitsDuringOvertime_PublishesDominationToTheSurvivor()
        {
            // GIVEN
            MatchController matchController = BuildActiveMatchControllerAtPhase(MatchPhase.Overtime);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            RegisterUnit(1, PlayerOneId, _playerOneUnitA);
            RegisterUnit(2, PlayerOneId, _playerOneUnitB);
            RegisterUnit(3, PlayerTwoId, _playerTwoUnit);

            // WHEN
            _unitPresenter.UnregisterUnit(3);
            MatchEvents.RaiseFuseExpired(3, PlayerTwoId);

            int frameBudget = PollFrameBudget;

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((outcome.Value.WinnerPlayerId, outcome.Value.Reason), Is.EqualTo((PlayerOneId, MatchEndReason.Domination)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator LateUpdate_Domination_PublishesBothPlayersFinalScoreChangedBeforeMatchEnded()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(60f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            MatchEvents.ScoreChanged += (playerId, unitCount) =>
                _eventOrder.Add(playerId == PlayerOneId ? ScoreChangedPlayerOneEventName : ScoreChangedPlayerTwoEventName);
            MatchEvents.MatchEnded += _ => _eventOrder.Add(MatchEndedEventName);
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Standard) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Standard), "Test setup expects Standard to have been reached.");
            _eventOrder.Clear();

            // WHEN — one recount moves both counts at once: Player One gains the third unit that wipes Player
            // Two, so both counts change on the very frame that decides the match.
            RegisterUnit(3, PlayerOneId, _playerOneUnitB);
            _unitPresenter.UnregisterUnit(2);
            MatchEvents.RaiseFuseExpired(2, PlayerTwoId);

            while ((_eventOrder.Count < 3) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(_eventOrder, Is.EqualTo(new[] { ScoreChangedPlayerOneEventName, ScoreChangedPlayerTwoEventName, MatchEndedEventName }));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator LateUpdate_ARealConversionFlipsAPlayersLastUnit_PublishesDominationToTheConvertingPlayer()
        {
            // GIVEN — both domination tests above remove the last unit with UnregisterUnit and a manual
            // RaiseFuseExpired; this one goes through ConversionController and ConversionResolver instead, the
            // path a landing's conversions actually take when they flip a player's last units.
            MatchConfigSO config = BuildConfig(60f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Standard) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Standard), "Test setup expects Standard to have been reached.");

            // WHEN — Player One's own unit lands on the cell it already occupies, so ConversionController's real
            // resolution converts Player Two's only unit at the adjacent cell rather than a manual removal.
            var command = new MoveCommand(MoveType.Clone, _conversionLandingSourceCoordinates, _playerOneUnitA, PlayerOneId, 1);
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _playerOneUnitA });

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((outcome.Value.WinnerPlayerId, outcome.Value.Reason), Is.EqualTo((PlayerOneId, MatchEndReason.Domination)));
        }

        // WORKAROUND: ExpectedResult is mandatory on a parameterized UnityTest — the method returns IEnumerator, and
        // a TestCase without one makes NUnit reject it as "non-void return value, but no result is expected".
        [UnityTest]
        [Timeout(5000)]
        [TestCase(MatchPhase.Loading, ExpectedResult = null)]
        [TestCase(MatchPhase.Countdown, ExpectedResult = null)]
        public IEnumerator LateUpdate_BothCountsAtZeroOutsidePlay_NeverPublishesDomination(MatchPhase phase)
        {
            // GIVEN — no units registered, so a recount naturally settles on zero for both players.
            MatchController matchController = BuildActiveMatchControllerAtPhase(phase);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;

            // WHEN — forces the recount branch to actually run rather than trivially never firing.
            MatchEvents.RaiseFuseExpired(999, PlayerOneId);

            int settleFrames = SettleFrameBudget;

            while (settleFrames-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That((matchController.Phase, outcome), Is.EqualTo((phase, (MatchOutcome?)null)));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator LateUpdate_OnePlayerHasUnitsWhileOutsidePlayDuringCountdown_NeverPublishesDomination()
        {
            // GIVEN — a real 0-vs-N board, not both counts at zero, so only the phase gate can be what refuses
            // this: MatchOutcomeResolver's own both-zero rule cannot explain a refusal here the way it does for
            // LateUpdate_BothCountsAtZeroOutsidePlay_NeverPublishesDomination above.
            MatchController matchController = BuildActiveMatchControllerAtPhase(MatchPhase.Countdown);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            RegisterUnit(1, PlayerOneId, _playerOneUnitA);

            // WHEN — forces the recount branch to actually run rather than trivially never firing.
            MatchEvents.RaiseFuseExpired(999, PlayerOneId);

            int settleFrames = SettleFrameBudget;

            while (settleFrames-- > 0)
            {
                yield return null;
            }

            // THEN — an ungated check would find Player One holding every live unit, attempt the illegal
            // Countdown -> Ended edge, log the failure, and abandon the match to None rather than leaving it here.
            LogAssert.NoUnexpectedReceived();
            Assert.That((matchController.Phase, outcome), Is.EqualTo((MatchPhase.Countdown, (MatchOutcome?)null)));
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

        private void RegisterUnit(int unitId, int playerId, HexCoordinates position)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(TroopCardId), position);

            Assert.That(_unitPresenter.RegisterUnit(unit, null), Is.True, "Test setup expects the unit to register.");
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

        private MatchConfigSO BuildConfig(float standardDurationSeconds, float countdownSeconds, params StartingPlacement[] placements)
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(standardDurationSeconds, countdownSeconds, 60f, 3f, placements);
            _spawned.Add(config);

            return config;
        }

        // Activated so its countdown Awaitable and per-frame Update actually run. Auto-start is passed as false,
        // so Start() returns without calling TryStartMatch and cannot race the explicit call the test makes.
        private MatchController BuildMatchController(MatchConfigSO config)
        {
            var go = new GameObject("MatchController_Test");
            go.SetActive(false);
            MatchController controller = go.AddComponent<MatchController>();
            controller.SetMatchConfigForTests(config, matchSeed: 0, isAutoStartEnabled: false);
            controller.Construct(_initializer, _unitPresenter, _deployController, _cardDiscardController, _energyPresenter);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        // Activated, unlike DeployControllerTests' and CardDiscardControllerTests' own SetPhaseForTests seam,
        // because LateUpdate has to actually run every frame for a domination to be detected. The config is
        // deliberately null, so auto-start has to be off: Start() would otherwise call TryStartMatch against a
        // config that does not exist and log MatchConfigMissing, failing the test on the error.
        private MatchController BuildActiveMatchControllerAtPhase(MatchPhase phase)
        {
            var go = new GameObject("MatchController_ActivePhase_Test");
            go.SetActive(false);
            MatchController controller = go.AddComponent<MatchController>();
            controller.SetMatchConfigForTests(null, matchSeed: 0, isAutoStartEnabled: false);
            controller.Construct(_initializer, _unitPresenter, _deployController, _cardDiscardController, _energyPresenter);
            go.SetActive(true);
            controller.SetPhaseForTests(phase);
            _spawned.Add(go);

            return controller;
        }

        // Never Constructed, but active from creation — AddComponent on a live GameObject runs OnEnable, so a
        // bare DeployController does subscribe to MatchEvents.MatchStarted. Harmless here: MatchController.Construct
        // only needs a non-null reference to push itself into, nothing in this fixture publishes a play through
        // it, and the one member the orchestrator reads off it — IsResolving — answers false on every frame.
        private T BuildBareComponent<T>(string name)
            where T : Component
        {
            var go = new GameObject(name);
            T component = go.AddComponent<T>();
            _spawned.Add(go);

            return component;
        }
    }
}
