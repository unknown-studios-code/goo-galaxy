using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Board.Data;
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
    public class MatchControllerTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        // Not a real bound — [Timeout] on each test is what actually bounds the wait. This only backstops
        // against an infinite loop if the awaited event never fires at all.
        private const int PollFrameBudget = 20000;
        private const string TroopCardId = "troop_alpha";

        private readonly List<Object> _spawned = new();

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

            _boardGO = new GameObject("MatchController_Board_Test");
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
            _deployController = BuildBareComponent<DeployController>("DeployController_Bare_Test");
            _cardDiscardController = BuildBareComponent<CardDiscardController>("CardDiscardController_Bare_Test");
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
        public IEnumerator TryStartMatch_ThroughTheCountdown_PublishesThreeTwoOneThenEntersStandard()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(60f, 3f, 1f));
            var tickedValues = new List<int>();

            void handleTick(int remaining) => tickedValues.Add(remaining);

            void handlePhaseChanged(MatchPhase phase)
            {
                // Unsubscribed on entry to Standard: BeginStandardPhase raises this before its own opening tick,
                // so this is the one point that separates the countdown's ticks from Standard's.
                if (phase == MatchPhase.Standard)
                {
                    MatchEvents.MatchClockTicked -= handleTick;
                }
            }

            MatchEvents.MatchClockTicked += handleTick;
            MatchEvents.MatchPhaseChanged += handlePhaseChanged;
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while (matchController.Phase != MatchPhase.Standard && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(frameBudget, Is.GreaterThan(0), "The match never reached Standard before the poll exhausted its infinite-loop backstop.");
            Assert.That(tickedValues, Is.EqualTo(new[] { 3, 2, 1 }));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TryStartMatch_PhaseChangedSubscriberThrowsOnCountdownEntry_LogsFailureAndStillReachesStandard()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(60f, 1f, 1f));

            static void handlePhaseChanged(MatchPhase phase)
            {
                if (phase == MatchPhase.Countdown)
                {
                    throw new InvalidOperationException("Faulty countdown subscriber.");
                }
            }

            MatchEvents.MatchPhaseChanged += handlePhaseChanged;
            LogAssert.Expect(LogType.Error, MatchLogMessages.CountdownSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex("Faulty countdown subscriber"));

            // WHEN
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while (matchController.Phase != MatchPhase.Standard && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(frameBudget, Is.GreaterThan(0), "The match never reached Standard before the poll exhausted its infinite-loop backstop.");
            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Standard));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TryStartMatch_PhaseChangedSubscriberThrowsOnStandardEntry_LogsFailureAndTheMatchStaysPlayable()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(60f, 1f, 1f));

            static void handlePhaseChanged(MatchPhase phase)
            {
                if (phase == MatchPhase.Standard)
                {
                    throw new InvalidOperationException("Faulty standard-entry subscriber.");
                }
            }

            MatchEvents.MatchPhaseChanged += handlePhaseChanged;
            LogAssert.Expect(LogType.Error, MatchLogMessages.PhaseSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex("Faulty standard-entry subscriber"));

            // WHEN
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while (matchController.Phase != MatchPhase.Standard && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(frameBudget, Is.GreaterThan(0), "The match never reached Standard before the poll exhausted its infinite-loop backstop.");
            Assert.That(matchController.RemainingSeconds, Is.GreaterThan(0f));
        }

        [Test]
        public void TryStartMatch_CalledTwice_SecondCallReturnsAlreadyRunningWithoutRepublishing()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(60f, 1f, 1f));
            matchController.TryStartMatch();
            int phaseChangedCount = 0;
            MatchEvents.MatchPhaseChanged += _ => phaseChangedCount++;

            // WHEN
            MatchStartResult secondResult = matchController.TryStartMatch();

            // THEN
            Assert.That(secondResult, Is.EqualTo(MatchStartResult.AlreadyRunning));
            Assert.That(phaseChangedCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TryStartMatch_ComponentDisabledDuringTheCountdown_AbandonsTheStartAndReturnsToNone()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(60f, 3f, 1f));
            matchController.TryStartMatch();

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Countdown), "Test setup expects the countdown to have started.");

            // WHEN — destroyCancellationToken fires on destroy and never on disable, so the countdown's awaits
            // keep running and the component has to decide for itself what a disable means.
            matchController.gameObject.SetActive(false);

            int frameBudget = PollFrameBudget;

            while (matchController.Phase == MatchPhase.Countdown && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.None));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TryStartMatch_AfterACountdownAbandonedByADisable_StartsAMatchAgain()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(60f, 3f, 1f));
            matchController.TryStartMatch();
            matchController.gameObject.SetActive(false);

            int frameBudget = PollFrameBudget;

            while (matchController.Phase != MatchPhase.None && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.None), "Test setup expects the disabled countdown to have abandoned the start.");
            matchController.gameObject.SetActive(true);

            // WHEN
            MatchStartResult result = matchController.TryStartMatch();

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.Success));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TryStartMatch_CalledAgainAfterADrawOnTheOvertimeClock_ReturnsSuccess()
        {
            // GIVEN — the four refusal branches all call AbandonStart() too, so this pins that none of them
            // fire on the happy path: a normal time-limit ending must not leave the match unable to restart.
            MatchConfigSO config = BuildConfig(1f, 1f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while (outcome == null && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(outcome, Is.Not.Null, "Test setup expects the match to have ended: level counts run through overtime and draw on the overtime clock.");

            // WHEN
            MatchStartResult result = matchController.TryStartMatch();

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.Success));
        }

        [Test]
        public void TryStartMatch_NoMatchConfigAssigned_ReturnsConfigMissing()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(null);
            LogAssert.Expect(LogType.Error, MatchLogMessages.MatchConfigMissing);

            // WHEN
            MatchStartResult result = matchController.TryStartMatch();

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.ConfigMissing));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator MatchClockTicked_DuringStandardPhase_FiresAtMostOncePerWholeSecond()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(1f, 1f, 1f));
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while (matchController.Phase != MatchPhase.Standard && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Standard), "Test setup expects Standard to have been reached.");

            int tickCount = 0;
            MatchEvents.MatchClockTicked += _ => tickCount++;

            // WHEN — the subscriber attaches after Standard opened, so its opening tick is already out and the
            // zero tick is the only whole second left for a one-second duration to publish, however many Update
            // frames run in between; an unthrottled implementation would report one per frame. It can be zero
            // rather than one: the countdown's continuation and the first Update of Standard can land on the
            // same frame as this subscription, publishing the zero tick before it exists.
            while (matchController.Phase == MatchPhase.Standard && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(tickCount, Is.LessThanOrEqualTo(1));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator ClockExpiry_UnequalUnitCounts_PublishesMatchEndedWithTimeLimitAndTheLeader()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(1f, 1f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerOneId, 1, 0), Placement(3, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while (outcome == null && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((outcome.Value.WinnerPlayerId, outcome.Value.Reason), Is.EqualTo((PlayerOneId, MatchEndReason.TimeLimit)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator ScoreChanged_WhenOnlyOnePlayersCountMoves_FiresOnlyForThatPlayer()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(60f, 1f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while (matchController.Phase != MatchPhase.Standard && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Standard), "Test setup expects Standard to have been reached.");

            var scoreChanges = new List<(int PlayerId, int UnitCount)>();
            MatchEvents.ScoreChanged += (playerId, unitCount) => scoreChanges.Add((playerId, unitCount));

            // WHEN — simulates what FuseController does: the unit leaves the registry, then the expiry fact fires.
            _unitPresenter.UnregisterUnit(1);
            MatchEvents.RaiseFuseExpired(1, PlayerOneId);

            int flushBudget = PollFrameBudget;

            while (scoreChanges.Count == 0 && flushBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(scoreChanges, Is.EqualTo(new[] { (PlayerOneId, 0) }));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator ScoreChanged_FuseExpiresAfterTheMatchHasEnded_DoesNotRepublish()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(1f, 1f, 1f, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while (outcome == null && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(outcome, Is.Not.Null, "Test setup expects the match to have ended: level counts run through overtime and draw on the overtime clock.");

            var scoreChanges = new List<(int PlayerId, int UnitCount)>();
            MatchEvents.ScoreChanged += (playerId, unitCount) => scoreChanges.Add((playerId, unitCount));

            // WHEN — simulates a fuse that expires after the outcome is already decided.
            _unitPresenter.UnregisterUnit(1);
            MatchEvents.RaiseFuseExpired(1, PlayerOneId);

            int settleFrames = 3;

            while (settleFrames-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(scoreChanges, Is.Empty);
        }

        [TestCase(MatchPhase.None)]
        [TestCase(MatchPhase.Loading)]
        [TestCase(MatchPhase.Countdown)]
        [TestCase(MatchPhase.Standard)]
        [TestCase(MatchPhase.OvertimeCheck)]
        [TestCase(MatchPhase.Overtime)]
        [TestCase(MatchPhase.Ended)]
        [TestCase(MatchPhase.Results)]
        public void SetPhaseForTests_EveryDeclaredPhase_LeavesTheControllerInThatPhase(MatchPhase phase)
        {
            // GIVEN
            MatchController matchController = BuildInactiveMatchController();

            // WHEN
            matchController.SetPhaseForTests(phase);

            // THEN
            Assert.That(matchController.Phase, Is.EqualTo(phase));
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

        private MatchConfigSO BuildConfig(
            float standardDurationSeconds,
            float countdownSeconds,
            float overtimeDurationSeconds,
            params StartingPlacement[] placements
        )
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(standardDurationSeconds, countdownSeconds, overtimeDurationSeconds, 3f, placements);
            _spawned.Add(config);

            return config;
        }

        // Activated so its countdown Awaitable and per-frame Update actually run; SetMatchConfigForTests always
        // disables auto-start, so TryStartMatch is never raced by Start() firing on the same activation.
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

        // Never activated: SetPhaseForTests writes MatchState directly and Phase only reads it back, so nothing
        // here needs Awake, Start, or a match domain — and an inactive component cannot race the fixture with
        // the auto-start that an un-Constructed one would otherwise attempt.
        private MatchController BuildInactiveMatchController()
        {
            var go = new GameObject("MatchController_Phase_Test");
            go.SetActive(false);
            MatchController controller = go.AddComponent<MatchController>();
            _spawned.Add(go);

            return controller;
        }

        // Never Constructed and never activated: MatchController.Construct only needs a non-null reference to
        // push itself into, and this fixture never exercises a card play or a discard.
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
