using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.AI.Controllers;
using GooGalaxy.Runtime.AI.Data;
using GooGalaxy.Runtime.AI.Interfaces;
using GooGalaxy.Runtime.AI.Models;
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
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.AI
{
    [TestFixture]
    public class AiControllerTests
    {
        private const int BoardRadius = 4;
        private const int HandSize = 4;
        private const int HumanPlayerId = 1;
        private const int MachinePlayerId = 2;
        private const int MachineUnitId = 1;
        private const int HumanUnitId = 2;
        private const int FirstSpawnedUnitId = 100;
        private const int TroopEnergyCost = 1;
        private const int MatchSeed = 12345;
        private const int JumpDistance = 2;
        private const int SpellEnergyCost = 1;
        private const int SpellClusterSize = 1;
        private const int SpellClusterRadius = 0;
        private const int SpellStatusDuration = 1;
        private const float ThinkSeconds = 0.1f;
        private const float EnergyCeilingThreshold = 8f;

        // An interval no frame budget below can reach, so an action arriving at all is proof the Energy ceiling
        // abandoned the wait rather than proof the machine happened to be quick.
        private const float LongThinkSeconds = 600f;

        private const float EnergyAboveCeiling = EnergyCeilingThreshold + 1f;

        private const string TroopCardIdValue = "troop_card";
        private const string ConfigAssetName = "TestAiConfig";
        private const string SpellCardIdValue = "spell_card";
        private const string FaultySubscriberMessage = "Faulty hand subscriber.";

        // Frames yielded while giving a retired think loop the chance to wake and misbehave. A budget rather
        // than a clock: the assertion holds whether or not the loop woke inside it, so a slower machine weakens
        // the evidence and can never turn the test red.
        private const int RetiredLoopFrameBudget = 60;

        // Bounds on polls that wait for something the loop does on its own clock. Generous enough that a slow
        // machine cannot exhaust them, and an infinite-loop backstop rather than a deadline.
        private const int CeilingTruncationFrameBudget = 300;
        private const int SubscriberFailureFrameBudget = 300;

        private static readonly HexCoordinates _machineOrigin = new(0, 0);
        private static readonly HexCoordinates _humanOrigin = new(-3, 0);
        private static readonly HexCoordinates _humanDeployTarget = new(-2, 0);

        private readonly List<Object> _spawned = new();
        private readonly List<(string Condition, LogType Type)> _logMessages = new();

        private GridLayoutSO _gridLayout;
        private GameObject _boardGO;
        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private AbilityController _abilityController;
        private CardPresenter _cardPresenter;
        private CardDataSO _troopCard;
        private DeckPresenter _deckPresenter;
        private DeployController _deployController;
        private CardDiscardController _discardController;
        private MatchController _matchController;
        private FakeEnergyLedger _ledger;
        private FakeDiscardLedger _discardLedger;
        private AiConfigSO _config;
        private GameObject _aiGO;
        private AiController _ai;

        [SetUp]
        public void SetUp()
        {
            _ledger = new FakeEnergyLedger();
            _discardLedger = new FakeDiscardLedger();

            var cardPresenterGO = new GameObject("CardPresenter_Test");
            cardPresenterGO.SetActive(false);
            _cardPresenter = cardPresenterGO.AddComponent<CardPresenter>();
            _troopCard = ScriptableObject.CreateInstance<CardDataSO>();
            _troopCard.SetAuthoredData(
                TroopCardIdValue,
                TroopCardIdValue,
                "Test description.",
                CardType.Troop,
                TroopEnergyCost,
                false,
                false,
                false,
                false,
                1,
                null
            );
            _spawned.Add(_troopCard);
            _cardPresenter.SetAuthoredCards(_troopCard);
            cardPresenterGO.SetActive(true);
            _spawned.Add(cardPresenterGO);

            _gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            _gridLayout.SetAuthoredData(BoardRadius);
            _spawned.Add(_gridLayout);

            _boardGO = new GameObject("AiController_Board_Test");
            _boardGO.SetActive(false);
            _gridPresenter = _boardGO.AddComponent<GridPresenter>();
            _unitPresenter = _boardGO.AddComponent<UnitPresenter>();
            _unitPresenter.Construct(_gridPresenter, _ledger);
            FuseController fuseController = _boardGO.AddComponent<FuseController>();
            fuseController.Construct(_unitPresenter);
            _abilityController = _boardGO.AddComponent<AbilityController>();
            _abilityController.Construct(_gridPresenter, _unitPresenter, fuseController);
            _gridPresenter.SetGridLayout(_gridLayout);
            _spawned.Add(_boardGO);

            _matchController = BuildMatchController(MatchPhase.Standard);
            _deckPresenter = BuildDeckPresenter(_troopCard);
            _deployController = BuildDeployController();
            _discardController = BuildDiscardController();

            _config = ScriptableObject.CreateInstance<AiConfigSO>();
            _config.name = ConfigAssetName;
            _config.SetAuthoredData(ThinkSeconds, ThinkSeconds, EnergyCeilingThreshold, isDiscardEnabled: true, AiConfig.DerivedSeed);
            _spawned.Add(_config);

            _logMessages.Clear();
            Application.logMessageReceived += HandleLogMessage;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= HandleLogMessage;
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
        [Timeout(10000)]
        public IEnumerator Awake_WithNoConfigAssigned_LogsTheConfigurationFault()
        {
            // GIVEN
            yield return ActivateBoard();

            LogAssert.Expect(LogType.Error, AiLogMessages.AiConfigMissing);

            // WHEN
            CreateAi(null);

            // THEN — once, not once per frame: the fault is reported where it is discovered and never again.
            Assert.That(CountLogMessages(AiLogMessages.AiConfigMissing), Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator OnDestroy_WhileThinking_CancelsTheLoopWithoutLoggingAnything()
        {
            // GIVEN
            yield return ActivateBoard();

            CreateAi(_config);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // WHEN
            Object.Destroy(_aiGO);

            for (int frame = 0; frame < RetiredLoopFrameBudget; frame++)
            {
                yield return null;
            }

            // THEN — the loop ends by cancellation on every normal path, so nothing about it belongs in the
            // console. Counted rather than left to LogAssert.NoUnexpectedReceived, which also fails on warnings
            // the engine raises for its own reasons and would make this test depend on the machine it runs on.
            Assert.That(CountErrorLogs(), Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_OnceThePhaseIsStandard_EnumeratesAndChooses()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(strategy.SelectCallCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_ASuccessfulJump_DebitsTheEnergyLedger()
        {
            // GIVEN — the proof that the action went through UnitPresenter rather than around it: nothing in the
            // AI assembly can debit a ledger, so a charge can only have come from the real move resolution.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_ledger.PayCalls, Has.Count.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_ASuccessfulJump_MovesTheUnitOffItsSector()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_unitPresenter.ActiveUnits[MachineUnitId].Position, Is.Not.EqualTo(_machineOrigin));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_ADeploy_PlacesTheNewUnitOnTheBoard()
        {
            // GIVEN — the Deploy branch of the submission fills the controller's own single-target buffer, which
            // no other test in this fixture drives all the way to a success.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, null);
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Deploy);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_unitPresenter.ActiveUnits.ContainsKey(FirstSpawnedUnitId), Is.True);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_AProtocol_ResolvesTheCardOnTheClusterItChose()
        {
            // GIVEN — the branch the cluster-borrowing contract exists for: the option's own borrowed buffer is
            // handed straight to the card-play entry point rather than copied into the Deploy target list.
            yield return ActivateBoard();

            UseSpellHand();
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.Protocol, MoveType.Deploy);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            int abilityResolvedCount = 0;
            void handleAbilityResolved(int playerId, AbilityResult result) => abilityResolvedCount++;
            MatchEvents.AbilityResolved += handleAbilityResolved;

            // WHEN
            _ai.ProcessTickForTests();

            // THEN — nothing in the AI assembly can resolve an ability, so one resolution can only have come from
            // the Protocol path of the real card-play entry point.
            Assert.That(abilityResolvedCount, Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_NoLegalActionAndAnAffordableDiscard_DiscardsExactlyOnce()
        {
            // GIVEN — no unit on the board leaves the Deploy footprint empty and nothing to move, so the whole
            // hand is dead however affordable it is.
            yield return ActivateBoard();

            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_discardLedger.PayCalls, Has.Count.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_WhileThePhaseIsCountdown_TakesNoAction()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            _matchController.SetPhaseForTests(MatchPhase.Countdown);

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(strategy.SelectCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_PhaseClosingBetweenChoiceAndSubmission_LeavesTheBoardUnchanged()
        {
            // GIVEN — UnitPresenter.ResolveMove carries no phase gate of its own, so a move chosen just before
            // the clock expired would otherwise still land and change a score that had already been decided.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            strategy.OnSelect = () => _matchController.SetPhaseForTests(MatchPhase.Ended);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_unitPresenter.ActiveUnits[MachineUnitId].Position, Is.EqualTo(_machineOrigin));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_PhaseClosingBetweenChoiceAndSubmission_DebitsNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            strategy.OnSelect = () => _matchController.SetPhaseForTests(MatchPhase.Ended);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_ledger.PayCalls, Is.Empty);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_WhileTheDeployResolverIsBusy_PlaysNoCard()
        {
            // GIVEN — both players act at once, so a tick can land inside another play's dispatch, which is
            // exactly when DeployController answers ResolverBusy.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, null);
            PlaceUnit(HumanUnitId, HumanPlayerId, _humanOrigin, null);
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Deploy);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.MoveExecuted += HandleMoveExecutedByTicking;

            // WHEN
            _deployController.TryPlayCard(HumanPlayerId, 0, new List<HexCoordinates> { _humanDeployTarget });

            // THEN — only the human play was ever charged.
            Assert.That(_ledger.PayCalls, Has.Count.EqualTo(1));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_WhileTheDeployResolverIsBusy_AttemptsThePlayExactlyOnce()
        {
            // GIVEN — the tick is dropped rather than retried: retrying in place would spin against a board only
            // the next frame can change.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, null);
            PlaceUnit(HumanUnitId, HumanPlayerId, _humanOrigin, null);
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Deploy);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.MoveExecuted += HandleMoveExecutedByTicking;

            // WHEN
            _deployController.TryPlayCard(HumanPlayerId, 0, new List<HexCoordinates> { _humanDeployTarget });

            // THEN
            Assert.That(
                CountLogMessages(string.Format(AiLogMessages.AiActionRejectedFormat, MachinePlayerId, MoveType.Deploy, CardPlayResult.ResolverBusy)),
                Is.EqualTo(1)
            );
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_AfterTheMatchEnded_TakesNoAction()
        {
            // GIVEN
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            _matchController.SetPhaseForTests(MatchPhase.Ended);
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(strategy.SelectCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_NeitherSeatMachineDriven_TakesNoAction()
        {
            // GIVEN — the PvP scene carries the same component, and it must sit inert there.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.RemoteHuman));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(strategy.SelectCallCount, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_NoLegalActionAndDiscardDisabled_DiscardsNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            _config.SetAuthoredData(ThinkSeconds, ThinkSeconds, EnergyCeilingThreshold, isDiscardEnabled: false, AiConfig.DerivedSeed);
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_discardLedger.PayCalls, Is.Empty);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator ProcessTick_NoLegalActionAndAnUnaffordableDiscard_DiscardsNothing()
        {
            // GIVEN
            yield return ActivateBoard();

            _discardLedger.IsAffordable = false;
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            _ai.ProcessTickForTests();

            // THEN
            Assert.That(_discardLedger.PayCalls, Is.Empty);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchStarted_MachineOnTheSecondSeat_TakesThatSeat()
        {
            // GIVEN
            yield return ActivateBoard();

            CreateAi(_config);

            // WHEN
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // THEN
            Assert.That(_ai.PlayerId, Is.EqualTo(MachinePlayerId));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchStarted_MachineOnTheFirstSeat_TakesThatSeat()
        {
            // GIVEN — nothing hard-codes a player number, so the same component has to find either seat.
            yield return ActivateBoard();

            CreateAi(_config);

            // WHEN
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.Machine, PlayerControl.LocalHuman));

            // THEN
            Assert.That(_ai.PlayerId, Is.EqualTo(HumanPlayerId));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchStarted_NeitherSeatMachineDriven_LeavesTheSeatUnassigned()
        {
            // GIVEN
            yield return ActivateBoard();

            CreateAi(_config);

            // WHEN
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.RemoteHuman));

            // THEN
            Assert.That(_ai.PlayerId, Is.EqualTo(PlayerSlot.UnassignedId));
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchStarted_AfterTheComponentWasDestroyed_IsNeverReached()
        {
            // GIVEN — domain reload is disabled, so a MatchEvents subscription outliving its component would
            // keep a destroyed controller reachable and fire into it next play session.
            yield return ActivateBoard();

            CreateAi(_config);
            Object.Destroy(_aiGO);
            yield return null;

            MatchConfiguration configuration = BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine);

            // WHEN / THEN — a surviving subscription would reach isActiveAndEnabled on a destroyed component,
            // which throws rather than answering.
            Assert.DoesNotThrow(() =>
            {
                MatchEvents.RaiseMatchStarted(configuration);
                MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            });
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchPhaseChanged_ToStandard_StartsThinking()
        {
            // GIVEN
            yield return ActivateBoard();

            CreateAi(_config);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_ai.IsThinking, Is.True);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator HandleMatchPhaseChanged_EnergyCrossingTheCeiling_ActsBeforeTheDrawnIntervalElapses()
        {
            // GIVEN — the behaviour the ceiling exists to produce: a balance sitting near the cap is spent rather
            // than regenerated into nothing, so the remainder of the drawn wait is abandoned.
            yield return ActivateBoard();

            _config.SetAuthoredData(LongThinkSeconds, LongThinkSeconds, EnergyCeilingThreshold, isDiscardEnabled: true, AiConfig.DerivedSeed);
            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, new FakeMoveCapability(canClone: false, canJump: true));
            CreateAi(_config);
            StubMoveStrategy strategy = OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Jump);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseEnergyChanged(MachinePlayerId, EnergyAboveCeiling);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            int frameBudget = CeilingTruncationFrameBudget;

            while (strategy.SelectCallCount == 0 && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN — the drawn interval is ten minutes, so no frame budget could have reached it on its own.
            Assert.That(strategy.SelectCallCount, Is.GreaterThan(0), "The machine never acted, so the Energy ceiling did not shorten the wait.");
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchPhaseChanged_EnergyAtTheCeilingWithNothingToSpendItOn_KeepsYieldingBetweenTicks()
        {
            // GIVEN — a locked late-game board with the balance at the cap: no legal option and no affordable
            // discard, so the tick spends nothing. Reading the ceiling before awaiting made the wait complete
            // without ever suspending, and the caller re-entered it in the same frame — a hang, not a busy loop.
            yield return ActivateBoard();

            _config.SetAuthoredData(ThinkSeconds, ThinkSeconds, EnergyCeilingThreshold, isDiscardEnabled: false, AiConfig.DerivedSeed);
            CreateAi(_config);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseEnergyChanged(MachinePlayerId, EnergyAboveCeiling);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            for (int frame = 0; frame < RetiredLoopFrameBudget; frame++)
            {
                yield return null;
            }

            // THEN — reaching this line at all is the evidence: a wait that never suspends never returns the
            // frame, so the assertion below is only ever read on a loop that yielded.
            Assert.That(_ai.IsThinking, Is.True);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchPhaseChanged_ToAPhaseThatIsNotPlay_StopsThinking()
        {
            // GIVEN
            yield return ActivateBoard();

            CreateAi(_config);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.OvertimeCheck);

            // THEN
            Assert.That(_ai.IsThinking, Is.False);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchPhaseChanged_WalkingThroughOvertimeInsideOneFrame_LeavesExactlyOneThinkLoopLive()
        {
            // GIVEN — play can close and reopen within a single frame, and the loop retired by the close is
            // still suspended in its wait when the reopening starts a new one.
            yield return ActivateBoard();

            CreateAi(_config);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.OvertimeCheck);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // WHEN — long enough for the retired loop to wake; if it cleared the flag on its way out, the
            // reopened loop would be dead while the controller still reported the match as in play.
            for (int frame = 0; frame < RetiredLoopFrameBudget; frame++)
            {
                yield return null;
            }

            // THEN
            Assert.That(_ai.IsThinking, Is.True);
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator HandleMatchPhaseChanged_ASubscriberThrowingWhileTheMachineSubmits_LogsTheThinkLoopFailure()
        {
            // GIVEN — submitting runs the board's own publishes into arbitrary subscriber code, and the caller
            // discards the Awaitable, so an escaping throw would end the loop against a silent console.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, null);
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Deploy);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.HandChanged += HandleHandChangedByThrowing;
            LogAssert.Expect(LogType.Error, AiLogMessages.AiThinkLoopFailed);
            LogAssert.Expect(LogType.Exception, new Regex(FaultySubscriberMessage));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            int frameBudget = SubscriberFailureFrameBudget;

            while (CountLogMessages(AiLogMessages.AiThinkLoopFailed) == 0 && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(CountLogMessages(AiLogMessages.AiThinkLoopFailed), Is.EqualTo(1));
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator HandleMatchPhaseChanged_ReopeningPlayAfterASubscriberThrew_StartsANewThinkLoop()
        {
            // GIVEN — the failure has to leave the flag down, or StartThinking's own guard would refuse every
            // restart and the opponent would stay silent for the rest of the session.
            yield return ActivateBoard();

            PlaceUnit(MachineUnitId, MachinePlayerId, _machineOrigin, null);
            CreateAi(_config);
            OverrideStrategy(MoveOptionKind.BoardMove, MoveType.Deploy);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.HandChanged += HandleHandChangedByThrowing;
            LogAssert.Expect(LogType.Error, AiLogMessages.AiThinkLoopFailed);
            LogAssert.Expect(LogType.Exception, new Regex(FaultySubscriberMessage));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            int frameBudget = SubscriberFailureFrameBudget;

            while (CountLogMessages(AiLogMessages.AiThinkLoopFailed) == 0 && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(_ai.IsThinking, Is.False, "Test setup expects the throwing subscriber to have ended the first loop.");
            MatchEvents.HandChanged -= HandleHandChangedByThrowing;

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_ai.IsThinking, Is.True);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchPhaseChanged_WithNoConfigAssigned_NeverStartsThinking()
        {
            // GIVEN
            yield return ActivateBoard();

            LogAssert.Expect(LogType.Error, AiLogMessages.AiConfigMissing);
            CreateAi(null);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_ai.IsThinking, Is.False);
        }

        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchEnded_WhileThinking_StopsThinking()
        {
            // GIVEN
            yield return ActivateBoard();

            CreateAi(_config);
            MatchEvents.RaiseMatchStarted(BuildConfiguration(PlayerControl.LocalHuman, PlayerControl.Machine));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // WHEN
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // THEN
            Assert.That(_ai.IsThinking, Is.False);
        }

        private MatchConfiguration BuildConfiguration(PlayerControl playerOneControl, PlayerControl playerTwoControl)
        {
            return new MatchConfiguration(
                MatchSeed,
                new PlayerSlot(HumanPlayerId, playerOneControl),
                new PlayerSlot(MachinePlayerId, playerTwoControl),
                180f,
                3f,
                60f
            );
        }

        private IEnumerator ActivateBoard()
        {
            _boardGO.SetActive(true);
            yield return null;

            _unitPresenter.SetUnitSpawner(new FakeUnitSpawner());
        }

        private void PlaceUnit(int unitId, int playerId, HexCoordinates position, IMoveCapable capability)
        {
            var unit = new GridUnit(unitId, playerId, new CardId(TroopCardIdValue), position);

            Assert.That(_unitPresenter.RegisterUnit(unit, capability), Is.True, $"Test setup expects unit {unitId} to register.");
        }

        // The controller reads its tuning off a serialized asset reference and exposes no setter for it, so the
        // fixture writes that reference through Unity's own serialization — the same mechanism the Inspector
        // uses — before the object is activated and Awake reads it.
        private void CreateAi(AiConfigSO config)
        {
            _aiGO = new GameObject("AiController_Test");
            _aiGO.SetActive(false);
            _ai = _aiGO.AddComponent<AiController>();
            _ai.Construct(
                _gridPresenter,
                _unitPresenter,
                _cardPresenter,
                _deployController,
                _discardController,
                _matchController,
                _deckPresenter,
                _ledger,
                _discardLedger
            );

            if (config != null)
            {
                JsonUtility.FromJsonOverwrite($"{{\"_config\":{{\"instanceID\":{config.GetInstanceID()}}}}}", _ai);
            }

            _aiGO.SetActive(true);
            _spawned.Add(_aiGO);
        }

        private StubMoveStrategy OverrideStrategy(MoveOptionKind kind, MoveType moveType)
        {
            var strategy = new StubMoveStrategy(kind, moveType);
            _ai.SetStrategy(strategy);

            return strategy;
        }

        private MatchController BuildMatchController(MatchPhase phase)
        {
            var go = new GameObject("MatchController_Test");
            go.SetActive(false);
            MatchController controller = go.AddComponent<MatchController>();
            controller.SetPhaseForTests(phase);
            _spawned.Add(go);

            return controller;
        }

        private DeckPresenter BuildDeckPresenter(CardDataSO card)
        {
            var cards = new CardDataSO[DeckState.GetMinimumKitSize(HandSize)];

            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = card;
            }

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            kit.SetAuthoredCards(cards);
            _spawned.Add(kit);

            var go = new GameObject("DeckPresenter_Test");
            go.SetActive(false);
            DeckPresenter presenter = go.AddComponent<DeckPresenter>();
            presenter.SetKit(kit, HandSize);
            go.SetActive(true);
            _spawned.Add(go);

            presenter.InitializePlayer(HumanPlayerId);
            presenter.InitializePlayer(MachinePlayerId);

            return presenter;
        }

        private DeployController BuildDeployController()
        {
            var go = new GameObject("DeployController_Test");
            go.SetActive(false);
            DeployController controller = go.AddComponent<DeployController>();
            controller.Construct(_deckPresenter, _cardPresenter, _unitPresenter, _abilityController, _ledger);
            controller.SetMatchController(_matchController);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        private CardDiscardController BuildDiscardController()
        {
            var go = new GameObject("CardDiscardController_Test");
            go.SetActive(false);
            CardDiscardController controller = go.AddComponent<CardDiscardController>();
            controller.Construct(_deckPresenter, _discardLedger, _deployController);
            controller.SetMatchController(_matchController);
            go.SetActive(true);
            _spawned.Add(go);

            return controller;
        }

        // Deals the machine a hand of Protocols instead of troops, which is the only way the enumerator offers a
        // Protocol option at all. The three components downstream of the deck are rebuilt with it, so this must
        // run before CreateAi hands them to the controller.
        private void UseSpellHand()
        {
            CardDataSO spellCard = ScriptableObject.CreateInstance<CardDataSO>();
            spellCard.SetAuthoredData(
                SpellCardIdValue,
                SpellCardIdValue,
                "Test description.",
                CardType.Spell,
                SpellEnergyCost,
                false,
                false,
                false,
                false,
                1,
                new[]
                {
                    new ImpactEffectDefinition(
                        ImpactEffectType.ApplyStatus,
                        StatusType.Frozen,
                        SpellClusterRadius,
                        SpellStatusDuration,
                        TargetFilter.All,
                        SpellClusterSize
                    ),
                }
            );
            _spawned.Add(spellCard);

            _cardPresenter.SetAuthoredCards(_troopCard, spellCard);
            _cardPresenter.BuildRegistry();

            _deckPresenter = BuildDeckPresenter(spellCard);
            _deployController = BuildDeployController();
            _discardController = BuildDiscardController();
        }

        private int CountLogMessages(string expected)
        {
            int count = 0;

            for (int i = 0; i < _logMessages.Count; i++)
            {
                if (_logMessages[i].Condition == expected)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountErrorLogs()
        {
            int count = 0;

            for (int i = 0; i < _logMessages.Count; i++)
            {
                if (_logMessages[i].Type is LogType.Error or LogType.Exception or LogType.Assert)
                {
                    count++;
                }
            }

            return count;
        }

        private static void HandleHandChangedByThrowing(int playerId, IReadOnlyList<CardId> hand, CardId nextCard)
        {
            throw new InvalidOperationException(FaultySubscriberMessage);
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            _logMessages.Add((condition, type));
        }

        private void HandleMoveExecutedByTicking(MoveCommand command, IReadOnlyList<HexCoordinates> affected)
        {
            _ai.ProcessTickForTests();
        }

        private sealed class StubMoveStrategy : IMoveStrategy
        {
            private readonly MoveOptionKind _kind;
            private readonly MoveType _moveType;

            public StubMoveStrategy(MoveOptionKind kind, MoveType moveType)
            {
                _kind = kind;
                _moveType = moveType;
            }

            public Action OnSelect { get; set; }

            public int SelectCallCount { get; private set; }

            public bool TrySelect(IReadOnlyList<MoveOption> options, out MoveOption selected)
            {
                SelectCallCount++;
                OnSelect?.Invoke();

                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].Kind == _kind && options[i].MoveType == _moveType)
                    {
                        selected = options[i];

                        return true;
                    }
                }

                selected = default;

                return false;
            }
        }

        private sealed class FakeEnergyLedger : IEnergyLedger
        {
            public List<(int PlayerId, MoveType Type, int UnitEnergyCost)> PayCalls { get; } = new();

            public bool CanAffordMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                return true;
            }

            public bool TryPayForMove(int playerId, MoveType moveType, int unitEnergyCost)
            {
                PayCalls.Add((playerId, moveType, unitEnergyCost));

                return true;
            }

            public void RefundMove(int playerId, MoveType moveType, int unitEnergyCost) { }
        }

        private sealed class FakeDiscardLedger : IDiscardLedger
        {
            public List<int> PayCalls { get; } = new();

            public bool IsAffordable { get; set; } = true;

            public bool CanAffordDiscard(int playerId)
            {
                return IsAffordable;
            }

            public bool TryPayForDiscard(int playerId)
            {
                if (!IsAffordable)
                {
                    return false;
                }

                PayCalls.Add(playerId);

                return true;
            }

            public void RefundDiscard(int playerId) { }
        }

        private sealed class FakeMoveCapability : IMoveCapable
        {
            public FakeMoveCapability(bool canClone, bool canJump)
            {
                CanClone = canClone;
                CanJump = canJump;
            }

            public bool CanClone { get; }

            public bool CanJump { get; }

            public bool CanIgnoreHazards => false;

            public int CloneDistance => BoardMetrics.DefaultCloneDistance;

            public int JumpDistance => AiControllerTests.JumpDistance;
        }

        private sealed class FakeUnitSpawner : IUnitSpawner
        {
            private int _nextUnitId = FirstSpawnedUnitId;

            public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
            {
                return new GridUnit(_nextUnitId++, playerId, cardId, at);
            }
        }
    }
}
