using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Controllers;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Data;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Match.Services;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Match
{
    [TestFixture]
    public class MatchOvertimeTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        // Not a real bound — [Timeout] on each test is what actually bounds the wait. This only backstops
        // against an infinite loop if the awaited event never fires at all.
        private const int PollFrameBudget = 20000;
        private const int NewUnitId = 500;
        private const int StaleCacheFlipUnitIdA = 501;
        private const int StaleCacheFlipUnitIdB = 502;
        private const int BreakFlipUnitIdA = 503;
        private const int BreakFlipUnitIdB = 504;
        private const int ReestablishFlipUnitIdA = 505;
        private const int ReestablishFlipUnitIdB = 506;
        private const string TroopCardId = "troop_alpha";

        // PERF: this fixture is bounded by wall-clock waits, not by work — it drives whole match lifecycles
        // through phases that elapse in real time. Accelerating the clock takes it from 31.0s to 3.3s, measured.
        //
        // Shortening the authored durations instead was tried and rejected: it only reached 15.2s, because
        // MatchController counts the countdown in whole ticks and awaits a hardcoded one second per tick, so no
        // value of ShortCountdownSeconds below one changes anything. Time.timeScale reaches that wait too —
        // Awaitable.WaitForSecondsAsync is scaled — which is the whole difference between the two approaches.
        // The durations below are therefore left at the values that read naturally against the GDD.
        //
        // <b>Ten, not the hundred FuseControllerTests uses, and the difference is not caution.</b>
        // Time.maximumDeltaTime clamps the *unscaled* frame delta and timeScale multiplies afterwards, so one
        // frame advances up to 0.333 × timeScale simulated seconds. At 100 that is 33s in a single hitching
        // frame, and an ordinary 16ms frame already advances 1.6s — more than half the two-second hold this
        // fixture's most delicate test measures. That test failed exactly once in three full-suite runs at 100,
        // by overrunning the whole overtime clock, and has passed three consecutive full-suite runs at 10, where
        // an ordinary frame advances 0.16s and the hold is a dozen frames wide. Raising this back to 100 buys
        // 2.8s of suite time and reintroduces a flake, which unity-testing.md Rule 15 does not permit.
        private const float AcceleratedTimeScale = 10f;

        private const float ShortStandardDurationSeconds = 1f;
        private const float ShortCountdownSeconds = 1f;
        private const float ShortOvertimeDurationSeconds = 1f;
        private const float LongOvertimeDurationSeconds = 60f;
        private const float SmallOvertimeLeadHoldSeconds = 1f;
        private const float ModerateOvertimeLeadHoldSeconds = 2f;
        private const float LargeOvertimeLeadHoldSeconds = 30f;

        // Added to SmallOvertimeLeadHoldSeconds so the poll below is guaranteed to observe the completed hold
        // rather than racing it.
        private const float StaleCacheSettleMarginSeconds = 0.25f;

        // Fractions of ModerateOvertimeLeadHoldSeconds: a majority large enough that only a tracker which fails
        // to reset on a leader change could still complete the hold within the post-reestablish wait below.
        private const float MajorityOfHoldFraction = 0.8f;
        private const float PostReestablishWaitFraction = 0.5f;

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _leadUnitCoordinates = new(2, 0);
        private static readonly HexCoordinates _overtimeDeployTarget = new(1, 0);
        private static readonly HexCoordinates _staleCacheFlipCoordinatesA = new(3, 0);
        private static readonly HexCoordinates _staleCacheFlipCoordinatesB = new(4, 0);
        private static readonly HexCoordinates _breakFlipCoordinatesA = new(3, 0);
        private static readonly HexCoordinates _breakFlipCoordinatesB = new(4, 0);
        private static readonly HexCoordinates _reestablishFlipCoordinatesA = new(-2, 0);
        private static readonly HexCoordinates _reestablishFlipCoordinatesB = new(-3, 0);

        private readonly List<Object> _spawned = new();

        private GameObject _boardGO;
        private GridLayoutSO _gridLayout;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private FuseController _fuseController;
        private AbilityController _abilityController;
        private CardPresenter _cardPresenter;
        private CardDataSO _troopCard;
        private DeckPresenter _deckPresenter;
        private EnergyPresenter _energyPresenter;
        private MatchInitializer _initializer;
        private DeployController _deployController;
        private CardDiscardController _cardDiscardController;

        [SetUp]
        public void SetUp()
        {
            Time.timeScale = AcceleratedTimeScale;

            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(_gridLayout);

            _boardGO = new GameObject("MatchOvertime_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _energyPresenter = _boardGO.AddComponent<EnergyPresenter>();
            _unitPresenter.Construct(_gridPresenter, _energyPresenter);
            _fuseController = _boardGO.AddComponent<FuseController>();
            _fuseController.Construct(_unitPresenter);
            _abilityController = _boardGO.AddComponent<AbilityController>();
            _abilityController.Construct(_gridPresenter, _unitPresenter, _fuseController);

            _cardPresenter = _boardGO.AddComponent<CardPresenter>();
            _troopCard = CreateCard(TroopCardId);
            _spawned.Add(_troopCard);
            _cardPresenter.SetAuthoredCards(_troopCard);

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
            // Restored first, before anything here can throw: timeScale is global process state, so a fixture
            // that left it accelerated would silently speed up every test that ran after it.
            Time.timeScale = 1f;

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
        public IEnumerator ClockExpiry_LevelUnitCounts_EntersOvertimeRatherThanPublishingTheDraw()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                SmallOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That((matchController.Phase, outcome), Is.EqualTo((MatchPhase.Overtime, (MatchOutcome?)null)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator BeginOvertimePhase_LevelUnitCountsAtStandardExpiry_ResetsTheClockToTheAuthoredOvertimeDuration()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                SmallOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");
            Assert.That(matchController.RemainingSeconds, Is.EqualTo(LongOvertimeDurationSeconds).Within(0.0001f));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator BeginOvertimePhase_LevelUnitCountsAtStandardExpiry_PublishesTheOvertimeOpeningTick()
        {
            // GIVEN — LongOvertimeDurationSeconds authors a whole 60 seconds, so its opening tick is exactly 60.
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                SmallOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            int lastTick = int.MinValue;
            MatchEvents.MatchClockTicked += remaining => lastTick = remaining;
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");
            Assert.That(lastTick, Is.EqualTo(60));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator EnergyState_DuringOvertime_IsOvertimeIsTrueForBothPlayers()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                SmallOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");
            Assert.That((_energyPresenter.GetState(PlayerOneId).IsOvertime, _energyPresenter.GetState(PlayerTwoId).IsOvertime), Is.EqualTo((true, true)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator EnergyState_AfterTheMatchEnds_IsOvertimeIsFalseForBothPlayers()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                ShortOvertimeDurationSeconds,
                LargeOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((_energyPresenter.GetState(PlayerOneId).IsOvertime, _energyPresenter.GetState(PlayerTwoId).IsOvertime), Is.EqualTo((false, false)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator TryStartMatch_CalledAgainAfterAMatchEndedInOvertime_IsOvertimeIsFalseForBothPlayers()
        {
            // GIVEN — PrepareForNewMatch resets the tracker and re-captures the durations but never calls
            // SetEnergyOvertime(false) itself; it depends on EnergyPresenter.InitializeMatch building the second
            // match's states fresh.
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                ShortOvertimeDurationSeconds,
                LargeOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(outcome, Is.Not.Null, "Test setup expects the first match to have ended in overtime.");

            // WHEN
            MatchStartResult result = matchController.TryStartMatch();

            // THEN
            Assert.That(result, Is.EqualTo(MatchStartResult.Success));
            Assert.That((_energyPresenter.GetState(PlayerOneId).IsOvertime, _energyPresenter.GetState(PlayerTwoId).IsOvertime), Is.EqualTo((false, false)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator OvertimeLead_HeldForTheAuthoredHold_EndsTheMatchForThatPlayer()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                SmallOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");

            // WHEN — a second live unit for Player One creates the lead the tracker holds for the authored
            // duration; LandingResolved only flips MatchController's dirty flag, so its payload does not matter.
            RegisterUnit(NewUnitId, PlayerOneId, _leadUnitCoordinates);
            MatchEvents.RaiseLandingResolved(default, default);

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((outcome.Value.WinnerPlayerId, outcome.Value.Reason), Is.EqualTo((PlayerOneId, MatchEndReason.TimeLimit)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator OvertimeLead_HoldCompletesAgainstAStaleCacheAfterASilentBoardChange_FallsThroughAndPublishesTheSettledScores()
        {
            // GIVEN — a regression for the defect this task fixed: the lead-hold branch used to end the match
            // straight off the cached score, so a landing that changed the registry without going through the
            // recount pipeline left the cache stale and could name a winner the board no longer had, swallowing
            // the real score with it.
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                SmallOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            var lastScoreChanged = new Dictionary<int, int>();
            MatchEvents.MatchEnded += raised => outcome = raised;
            MatchEvents.ScoreChanged += (playerId, unitCount) => lastScoreChanged[playerId] = unitCount;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");

            // WHEN — Player One's lead is established through the normal publish so the tracker starts its hold,
            // and the board is then moved against that leader silently: no MatchEvents publish, so the cache
            // stays stale while the registry already favours Player Two by the time the hold completes.
            RegisterUnit(NewUnitId, PlayerOneId, _leadUnitCoordinates);
            MatchEvents.RaiseLandingResolved(default, default);
            yield return null;

            float remainingAtLeadEstablished = matchController.RemainingSeconds;
            RegisterUnit(StaleCacheFlipUnitIdA, PlayerTwoId, _staleCacheFlipCoordinatesA);
            RegisterUnit(StaleCacheFlipUnitIdB, PlayerTwoId, _staleCacheFlipCoordinatesB);

            float staleHoldSettleWindow = SmallOvertimeLeadHoldSeconds + StaleCacheSettleMarginSeconds;

            while ((outcome == null) && ((remainingAtLeadEstablished - matchController.RemainingSeconds) < staleHoldSettleWindow) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That((outcome, lastScoreChanged[PlayerOneId], lastScoreChanged[PlayerTwoId]), Is.EqualTo(((MatchOutcome?)null, 2, 3)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator OvertimeLead_BrokenAfterAMajorityOfTheHoldThenReestablished_DoesNotEndByWhenANonResettingTrackerWould()
        {
            // GIVEN — the lead is held for a clear majority of the window, broken by a direct flip to the other
            // player, and re-established the same way: a tracker whose leader-change reset was deleted would
            // carry that majority forward through both flips and complete well inside the wait below, where a
            // correctly resetting one has to start the whole window over from the re-establishment.
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                LongOvertimeDurationSeconds,
                ModerateOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");

            // WHEN
            RegisterUnit(NewUnitId, PlayerOneId, _leadUnitCoordinates);
            MatchEvents.RaiseLandingResolved(default, default);
            yield return null;

            float remainingAtLeadEstablished = matchController.RemainingSeconds;
            float majorityHoldWindow = ModerateOvertimeLeadHoldSeconds * MajorityOfHoldFraction;

            while (((remainingAtLeadEstablished - matchController.RemainingSeconds) < majorityHoldWindow) && (frameBudget-- > 0))
            {
                yield return null;
            }

            RegisterUnit(BreakFlipUnitIdA, PlayerTwoId, _breakFlipCoordinatesA);
            RegisterUnit(BreakFlipUnitIdB, PlayerTwoId, _breakFlipCoordinatesB);
            MatchEvents.RaiseLandingResolved(default, default);
            yield return null;

            RegisterUnit(ReestablishFlipUnitIdA, PlayerOneId, _reestablishFlipCoordinatesA);
            RegisterUnit(ReestablishFlipUnitIdB, PlayerOneId, _reestablishFlipCoordinatesB);
            MatchEvents.RaiseLandingResolved(default, default);
            yield return null;

            float remainingAfterReestablished = matchController.RemainingSeconds;
            float postReestablishWindow = ModerateOvertimeLeadHoldSeconds * PostReestablishWaitFraction;

            while ((outcome == null) && ((remainingAfterReestablished - matchController.RemainingSeconds) < postReestablishWindow) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Null);
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator OvertimeExpiry_LeadUnderTheHold_EndsTheMatchForTheLeaderByTimeLimit()
        {
            // GIVEN — the hold is authored far longer than the overtime clock, so only the clock can end this.
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                ShortOvertimeDurationSeconds,
                LargeOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");

            // WHEN
            RegisterUnit(NewUnitId, PlayerOneId, _leadUnitCoordinates);
            MatchEvents.RaiseLandingResolved(default, default);

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That((outcome.Value.WinnerPlayerId, outcome.Value.Reason), Is.EqualTo((PlayerOneId, MatchEndReason.TimeLimit)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator OvertimeExpiry_LevelUnitCounts_PublishesTheDrawOutcome()
        {
            // GIVEN
            MatchConfigSO config = BuildConfig(
                ShortStandardDurationSeconds,
                ShortCountdownSeconds,
                ShortOvertimeDurationSeconds,
                LargeOvertimeLeadHoldSeconds,
                Placement(1, PlayerOneId, 0, 0),
                Placement(2, PlayerTwoId, -1, 0)
            );
            MatchController matchController = BuildMatchController(config);
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            // WHEN
            int frameBudget = PollFrameBudget;

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That(outcome.Value, Is.EqualTo(MatchOutcome.Drawn));
        }

        [Test]
        public void TryPlayCard_LegalTroopPlayWhileInOvertime_ReturnsSuccess()
        {
            // GIVEN — a troop's Energy is charged by UnitPresenter.ResolveDeploy against the ledger it was
            // Constructed with, which is the real EnergyPresenter shared by this fixture's board.
            _energyPresenter.InitializePlayer(PlayerOneId, new EnergyConfig(10f, 1f, 10f));
            _unitPresenter.SetUnitSpawner(new FakeUnitSpawner());
            RegisterUnit(1, PlayerOneId, _origin);
            DeployController deployController = BuildOvertimeDeployController(_troopCard);

            // WHEN
            CardPlayResult result = deployController.TryPlayCard(PlayerOneId, 0, new List<HexCoordinates> { _overtimeDeployTarget });

            // THEN
            Assert.That(result, Is.EqualTo(CardPlayResult.Success));
        }

        [Test]
        public void TryDiscardCard_LegalDiscardWhileInOvertime_ReturnsSuccess()
        {
            // GIVEN
            DeckPresenter deckPresenter = BuildDeckPresenterForCard(_troopCard);
            var ledger = new FakePermissiveDiscardLedger();
            MatchController overtimeMatchController = BuildMatchControllerAtPhase(MatchPhase.Overtime);
            CardDiscardController controller = BuildDiscardController(
                deckPresenter,
                ledger,
                BuildBareComponent<DeployController>("DeployController_Bare_Overtime_Test"),
                overtimeMatchController
            );

            // WHEN
            CardDiscardResult result = controller.TryDiscardCard(PlayerOneId, 0);

            // THEN
            Assert.That(result, Is.EqualTo(CardDiscardResult.Success));
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

        private MatchConfigSO BuildConfig(
            float standardDurationSeconds,
            float countdownSeconds,
            float overtimeDurationSeconds,
            float overtimeLeadHoldSeconds,
            params StartingPlacement[] placements
        )
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(standardDurationSeconds, countdownSeconds, overtimeDurationSeconds, overtimeLeadHoldSeconds, placements);
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

        // Never activated: SetPhaseForTests writes MatchState directly and Phase only reads it back, so nothing
        // here needs Awake, Start, or a match domain — the same seam DeployControllerTests and
        // CardDiscardControllerTests use for their own phase-gate tests.
        private MatchController BuildMatchControllerAtPhase(MatchPhase phase)
        {
            var go = new GameObject("MatchController_Phase_Test");
            go.SetActive(false);
            MatchController controller = go.AddComponent<MatchController>();
            controller.SetPhaseForTests(phase);
            _spawned.Add(go);

            return controller;
        }

        private DeployController BuildOvertimeDeployController(CardDataSO card)
        {
            DeckPresenter deckPresenter = BuildDeckPresenterForCard(card);
            MatchController overtimeMatchController = BuildMatchControllerAtPhase(MatchPhase.Overtime);

            var go = new GameObject("DeployController_Overtime_Test");
            go.SetActive(false);
            DeployController controller = go.AddComponent<DeployController>();
            controller.Construct(deckPresenter, _cardPresenter, _unitPresenter, _abilityController, new FakePermissiveEnergyLedger());
            controller.SetMatchController(overtimeMatchController);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        private CardDiscardController BuildDiscardController(
            DeckPresenter deckPresenter,
            IDiscardLedger ledger,
            DeployController deployController,
            MatchController matchController
        )
        {
            var go = new GameObject("CardDiscardController_Overtime_Test");
            go.SetActive(false);
            CardDiscardController controller = go.AddComponent<CardDiscardController>();
            controller.Construct(deckPresenter, ledger, deployController);
            controller.SetMatchController(matchController);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        private DeckPresenter BuildDeckPresenterForCard(CardDataSO card)
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            var go = new GameObject("DeckPresenter_Overtime_Test");
            go.SetActive(false);
            DeckPresenter presenter = go.AddComponent<DeckPresenter>();
            presenter.SetKit(kit, HandSize);
            go.SetActive(true);
            _spawned.Add(go);

            presenter.InitializePlayer(PlayerOneId);

            return presenter;
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

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private const int FirstSpawnedUnitId = 100;

            private int _nextUnitId = FirstSpawnedUnitId;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }

        // Permissive on purpose: TryPlayCard_LegalTroopPlayWhileInOvertime exercises the Overtime gate, never
        // Energy pricing — the troop's own cost is charged by UnitPresenter against the real EnergyPresenter,
        // not through this.
        private sealed class FakePermissiveEnergyLedger : IEnergyLedger
        {
            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost) { }
        }

        private sealed class FakePermissiveDiscardLedger : IDiscardLedger
        {
            public bool CanAffordDiscard(int playerId)
            {
                return true;
            }

            public bool TryPayForDiscard(int playerId)
            {
                return true;
            }

            public void RefundDiscard(int playerId) { }
        }
    }
}
