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
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Match
{
    // Named for the flow rather than for a type, per unity-testing.md Rule 2: opening the catch-up bonus spans
    // MatchController, CatchUpTracker, and EnergyPresenter, and no one of them owns the outcome.
    [TestFixture]
    public class MatchCatchUpTests
    {
        private const int BoardRadius = 6;
        private const int HandSize = 4;
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;
        private const int TrailingUnitId = 5;
        private const int OvertimeCatchUpUnitIdA = 601;
        private const int OvertimeCatchUpUnitIdB = 602;
        private const int OvertimeCatchUpUnitIdC = 603;

        // Not a real bound — [Timeout] on each test is what actually bounds the wait. This only backstops
        // against an infinite loop if the awaited event never fires at all.
        private const int PollFrameBudget = 20000;
        private const int SettleFrameBudget = 60;
        private const string TroopCardId = "troop_alpha";

        // Short enough to actually run the Standard clock out inside a fixture's timeout. BuildConfig's fixed
        // 60s Standard phase, used by every other test in this file, starts a match already inside Standard and
        // never waits for its clock to expire.
        private const float ShortStandardDurationSeconds = 1f;
        private const float ShortCountdownSeconds = 1f;
        private const float LongOvertimeDurationSeconds = 60f;

        // Long relative to LongOvertimeDurationSeconds so Overtime's own lead-hold ending cannot race the
        // assertion — mirrors MatchOvertimeTests' LargeOvertimeLeadHoldSeconds for the same reason.
        private const float LargeOvertimeLeadHoldSeconds = 30f;

        // Four units for Player One against one for Player Two: 1 of 5 is a 20% share, comfortably inside the
        // 40% deficit threshold, so the ratio math is never the thing a failure is ambiguous about.
        private static readonly StartingPlacement[] _deficitPlacements =
        {
            Placement(1, PlayerOneId, 0, 0),
            Placement(2, PlayerOneId, 1, 0),
            Placement(3, PlayerOneId, 2, 0),
            Placement(4, PlayerOneId, 3, 0),
            Placement(TrailingUnitId, PlayerTwoId, -1, 0),
        };

        // Short enough that a fixture never waits out the GDD's authored 20s/60s. The constructor does not
        // clamp — see CatchUpConfig — which is exactly what makes an authored value this far below its own
        // Inspector band usable in a test. The cooldown is long relative to the duration on purpose: the
        // publish-count test settles for a while after the window closes, and a short cooldown risks a second,
        // unrelated re-open landing inside that settle window on a slow frame rate.
        private static readonly CatchUpConfig _shortCatchUp = new(thresholdRatio: 0.4f, regenMultiplier: 1.15f, durationSeconds: 0.3f, cooldownSeconds: 10f);

        // Used only by the rematch test. The cooldown is long relative to the 0.2s duration on purpose: a
        // fixture that completes a domination and restarts well inside it is proof the tracker was reset, not
        // proof the cooldown happened to drain on its own.
        private static readonly CatchUpConfig _rematchCatchUp = new(thresholdRatio: 0.4f, regenMultiplier: 1.15f, durationSeconds: 0.2f, cooldownSeconds: 6f);

        private readonly List<Object> _spawned = new();
        private readonly List<(int PlayerId, bool IsActive)> _catchUpEvents = new();

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

            _boardGO = new GameObject("MatchCatchUp_Board_Test");
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

            _catchUpEvents.Clear();
            MatchEvents.CatchUpChanged += HandleCatchUpChanged;
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
        public IEnumerator Update_RealDeficitBoard_OpensTrailingPlayersBonusAtTheAuthoredMultiplier()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(_shortCatchUp, _deficitPlacements));
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);

            // WHEN
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);

            // THEN
            Assert.That(_energyPresenter.GetState(PlayerTwoId).CatchUpMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(_energyPresenter.GetState(PlayerOneId).CatchUpMultiplier, Is.EqualTo(1f).Within(0.0001f));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator Update_CatchUpWindowRuns_LeavesTheTrailingPlayerWithMoreEnergyThanTheLeader()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(_shortCatchUp, _deficitPlacements));
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);

            // WHEN
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: false);

            // THEN
            float trailingEnergy = _energyPresenter.GetState(PlayerTwoId).CurrentEnergy;
            float leadingEnergy = _energyPresenter.GetState(PlayerOneId).CurrentEnergy;
            Assert.That(trailingEnergy, Is.GreaterThan(leadingEnergy));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator Update_OvertimeAndCatchUpBothActive_ComposeTheirMultipliersOnTheTrailingPlayer()
        {
            // GIVEN — a level 1-v-1 start reaches Overtime on the Standard clock rather than being decided by
            // it, and the large lead-hold keeps Overtime's own ending from racing the assertion below.
            MatchConfigSO config = BuildShortStandardConfig(_shortCatchUp, Placement(1, PlayerOneId, 0, 0), Placement(2, PlayerTwoId, -1, 0));
            MatchController matchController = BuildMatchController(config);
            matchController.TryStartMatch();

            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != MatchPhase.Overtime) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(MatchPhase.Overtime), "Test setup expects Overtime to have been reached.");

            // WHEN — three more units for Player One turn Player Two's 1-of-5 share into a deficit, opening
            // their catch-up bonus while Overtime's own doubled regeneration is already in effect.
            RegisterUnit(OvertimeCatchUpUnitIdA, PlayerOneId, new HexCoordinates(2, 0));
            RegisterUnit(OvertimeCatchUpUnitIdB, PlayerOneId, new HexCoordinates(3, 0));
            RegisterUnit(OvertimeCatchUpUnitIdC, PlayerOneId, new HexCoordinates(4, 0));
            MatchEvents.RaiseLandingResolved(default, default);

            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);

            // THEN — the two multipliers compose rather than one overriding the other: Overtime doubles the base
            // rate and the catch-up bonus multiplies that result by another 1.15, mirroring EnergyState's own
            // composition of the two.
            Assert.That(_energyPresenter.GetState(PlayerTwoId).IsOvertime, Is.True);
            Assert.That(_energyPresenter.GetState(PlayerTwoId).CatchUpMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
            Assert.That(_energyPresenter.GetState(PlayerTwoId).EffectiveRegenRate, Is.EqualTo(0.821429f).Within(0.0001f));
        }

        // WORKAROUND: ExpectedResult is mandatory on a parameterized UnityTest — the method returns IEnumerator, and
        // a TestCase without one makes NUnit reject it as "non-void return value, but no result is expected".
        [UnityTest]
        [Timeout(5000)]
        [TestCase(MatchPhase.Loading, ExpectedResult = null)]
        [TestCase(MatchPhase.Countdown, ExpectedResult = null)]
        public IEnumerator Update_RealDeficitOutsideStandardOrOvertime_NeverPublishesCatchUpChanged(MatchPhase phase)
        {
            // GIVEN — a real deficit board, registered directly rather than through TryStartMatch, so the
            // phase gate is the only thing that could be refusing this.
            MatchController matchController = BuildActiveMatchControllerAtPhase(phase);

            for (int i = 0; i < _deficitPlacements.Length; i++)
            {
                RegisterUnit(
                    _deficitPlacements[i].UnitId,
                    _deficitPlacements[i].PlayerId,
                    new HexCoordinates(_deficitPlacements[i].Q, _deficitPlacements[i].R)
                );
            }

            // WHEN — forces the recount branch to actually run rather than trivially never firing.
            MatchEvents.RaiseFuseExpired(999, PlayerOneId);

            int settleFrames = SettleFrameBudget;

            while (settleFrames-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That((matchController.Phase, _catchUpEvents.Count), Is.EqualTo((phase, 0)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator CatchUpChanged_OneWindowOpensAndCloses_PublishesExactlyTwice()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(_shortCatchUp, _deficitPlacements));
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);

            // WHEN — waits for the open and the close, then keeps polling well past the cooldown's start to
            // prove nothing publishes again on every settled frame in between.
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: false);

            int settleFrames = SettleFrameBudget;

            while (settleFrames-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(_catchUpEvents.Count, Is.EqualTo(2));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator EndMatch_WhileTheBonusIsStillOpen_RestoresBothPlayersToTheStandardMultiplier()
        {
            // GIVEN
            MatchController matchController = BuildMatchController(BuildConfig(_shortCatchUp, _deficitPlacements));
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);

            // WHEN — ends the match by domination while the bonus is still active, before it would have closed
            // on its own, so this proves EndMatch's reset runs unconditionally rather than only after expiry.
            _unitPresenter.UnregisterUnit(TrailingUnitId);
            MatchEvents.RaiseFuseExpired(TrailingUnitId, PlayerTwoId);

            int frameBudget = PollFrameBudget;

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            // THEN
            Assert.That(outcome, Is.Not.Null, "MatchEnded never fired before the poll exhausted its infinite-loop backstop.");
            Assert.That(_energyPresenter.GetState(PlayerOneId).CatchUpMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(_energyPresenter.GetState(PlayerTwoId).CatchUpMultiplier, Is.EqualTo(1f).Within(0.0001f));

            // The multiplier alone is not the contract: a subscriber that lit a badge on the open edge is left
            // holding it unless the close is announced too.
            Assert.That(_catchUpEvents, Does.Contain((PlayerTwoId, false)));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator SetAuthoredCatchUp_EditedAfterTheMatchHasStarted_TheRunningMatchKeepsTheValuesCapturedAtStart()
        {
            // GIVEN — PrepareForNewMatch captures CatchUp into a field the instant the match starts, so an edit
            // made to the live asset afterward must land on the next match rather than the one already running.
            MatchConfigSO config = BuildConfig(_shortCatchUp, _deficitPlacements);
            MatchController matchController = BuildMatchController(config);
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);

            // WHEN — the asset is edited mid-match to a clearly different regeneration multiplier.
            SetCatchUp(config, new CatchUpConfig(thresholdRatio: 0.49f, regenMultiplier: 1.5f, durationSeconds: 0.3f, cooldownSeconds: 10f));
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);

            // THEN — the multiplier the running match applies is the one captured at start, not the edited one.
            Assert.That(_energyPresenter.GetState(PlayerTwoId).CatchUpMultiplier, Is.EqualTo(1.15f).Within(0.0001f));
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator TryStartMatch_RematchAfterACooldownWasLeftOutstanding_OpensTheBonusAgainWithoutWaitingItOut()
        {
            // GIVEN — the first match's bonus opens and is driven through to expiry, into a long cooldown, and
            // the match is then ended by domination while that cooldown is still draining.
            MatchController matchController = BuildMatchController(BuildConfig(_rematchCatchUp, _deficitPlacements));
            MatchOutcome? outcome = null;
            MatchEvents.MatchEnded += raised => outcome = raised;
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: false);

            _unitPresenter.UnregisterUnit(TrailingUnitId);
            MatchEvents.RaiseFuseExpired(TrailingUnitId, PlayerTwoId);

            int frameBudget = PollFrameBudget;

            while ((outcome == null) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(outcome, Is.Not.Null, "Test setup expects the first match to end by domination.");
            _catchUpEvents.Clear();

            // WHEN — a rematch reseeds the same deficit board and is run well within the cooldown the first
            // match left outstanding.
            matchController.TryStartMatch();

            yield return WaitForPhase(matchController, MatchPhase.Standard);
            yield return WaitForCatchUpEvent(PlayerTwoId, isActive: true);

            // THEN
            Assert.That(_catchUpEvents, Does.Contain((PlayerTwoId, true)));
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

        // The same authoring seam the EditMode config tests use. SerializedObject is unavailable here — the
        // PlayMode assembly compiles for a device build too — and an internal setter is what the codebase
        // already offers for exactly that case.
        private static void SetCatchUp(MatchConfigSO config, CatchUpConfig catchUp)
        {
            config.SetAuthoredCatchUp(catchUp);
        }

        private static IEnumerator WaitForPhase(MatchController matchController, MatchPhase phase)
        {
            int frameBudget = PollFrameBudget;

            while ((matchController.Phase != phase) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(matchController.Phase, Is.EqualTo(phase), "Test setup expects the authored phase to have been reached.");
        }

        private IEnumerator WaitForCatchUpEvent(int playerId, bool isActive)
        {
            int frameBudget = PollFrameBudget;

            while (!HasCatchUpEvent(playerId, isActive) && (frameBudget-- > 0))
            {
                yield return null;
            }

            Assert.That(
                HasCatchUpEvent(playerId, isActive),
                Is.True,
                "CatchUpChanged never reported the awaited transition before the poll exhausted its infinite-loop backstop."
            );
        }

        private bool HasCatchUpEvent(int playerId, bool isActive)
        {
            for (int i = 0; i < _catchUpEvents.Count; i++)
            {
                if ((_catchUpEvents[i].PlayerId == playerId) && (_catchUpEvents[i].IsActive == isActive))
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleCatchUpChanged(int playerId, bool isActive, float remainingSeconds)
        {
            _catchUpEvents.Add((playerId, isActive));
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

        private MatchConfigSO BuildConfig(CatchUpConfig catchUp, params StartingPlacement[] placements)
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(60f, 1f, 60f, 3f, placements);
            SetCatchUp(config, catchUp);
            _spawned.Add(config);

            return config;
        }

        // The Overtime-composition test's own config builder: unlike BuildConfig above, the Standard phase is
        // short enough to actually run its clock out rather than starting a match already inside Standard.
        private MatchConfigSO BuildShortStandardConfig(CatchUpConfig catchUp, params StartingPlacement[] placements)
        {
            MatchConfigSO config = ScriptableObject.CreateInstance<MatchConfigSO>();
            config.SetAuthoredData(ShortStandardDurationSeconds, ShortCountdownSeconds, LongOvertimeDurationSeconds, LargeOvertimeLeadHoldSeconds, placements);
            SetCatchUp(config, catchUp);
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
        // because Update has to actually run every frame for the phase gate to be exercised. The config is
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
