using System.Collections.Generic;
using GooGalaxy.Runtime.Audio.Controllers;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Audio
{
    [TestFixture]
    public class MatchSfxControllerTests
    {
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const int UnitId = 7;
        private const float StandardDurationSeconds = 1000f;
        private const int AllocationIterations = 32;
        private const string CardIdValue = "acid_crawler";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _neighbor = new(1, 0);

        private GameObject _controllerGO;
        private MatchSfxController _controller;
        private FakeAudioService _fakeService;
        private FakeInteractionFeedbackSource _feedbackSource;

        [SetUp]
        public void SetUp()
        {
            MatchEvents.ResetEvents();

            _fakeService = new FakeAudioService();
            _feedbackSource = new FakeInteractionFeedbackSource();

            _controllerGO = new GameObject(nameof(MatchSfxController));
            _controllerGO.SetActive(false);
            _controller = _controllerGO.AddComponent<MatchSfxController>();
            _controller.Construct(_fakeService, _feedbackSource);
            _controllerGO.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            MatchEvents.ResetEvents();

            if (_controllerGO != null)
            {
                Object.Destroy(_controllerGO);
            }
        }

        [Test]
        public void CardPlayAttempted_SuccessByTheLocalPlayer_PlaysThatCardsCue()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(LocalPlayerId, CardPlayResult.Success));

            // THEN
            Assert.That(_fakeService.PlayedCardCues, Is.EqualTo(new[] { new CardId(CardIdValue) }));
        }

        [Test]
        public void CardPlayAttempted_SuccessByTheOpponent_PlaysThatCardsCue()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(OpponentPlayerId, CardPlayResult.Success));

            // THEN
            Assert.That(_fakeService.PlayedCardCues, Is.EqualTo(new[] { new CardId(CardIdValue) }));
        }

        [Test]
        public void CardPlayAttempted_Success_PlaysNoOneShot()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(LocalPlayerId, CardPlayResult.Success));

            // THEN
            Assert.That(_fakeService.CallLog, Is.EqualTo(new[] { AudioServiceCall.PlayCardCue }));
        }

        [TestCase(CardPlayResult.IllegalPlacement)]
        [TestCase(CardPlayResult.InsufficientEnergy)]
        [TestCase(CardPlayResult.InvalidTargetCount)]
        public void CardPlayAttempted_LocalPlayerRefused_PlaysPlayRejected(CardPlayResult result)
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(LocalPlayerId, result));

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.PlayRejected }));
        }

        [Test]
        public void CardPlayAttempted_OpponentRefused_PlaysNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(OpponentPlayerId, CardPlayResult.IllegalPlacement));

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void CardPlayAttempted_LocalPlayerRefusedForAReasonThatIsNotTheirMistake_PlaysNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(LocalPlayerId, CardPlayResult.ResolverBusy));

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void CardPlayAttempted_RefusedWithNoSeatDrivenOnThisDevice_PlaysNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfigWithoutLocalSeat());

            // WHEN
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(LocalPlayerId, CardPlayResult.IllegalPlacement));

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [TestCase(MoveType.Clone, AudioCue.CloneMove)]
        [TestCase(MoveType.Jump, AudioCue.JumpMove)]
        public void MoveExecuted_CloneOrJump_PlaysTheMatchingCue(MoveType type, AudioCue expected)
        {
            // GIVEN
            MoveCommand command = BuildMove(type);

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _neighbor });

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void MoveExecuted_Deploy_PlaysNothing()
        {
            // GIVEN
            var command = MoveCommand.ForDeploy(_origin, LocalPlayerId);

            // WHEN
            MatchEvents.RaiseMoveExecuted(command, new List<HexCoordinates> { _origin });

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [TestCase(1, AudioCue.ConversionSingle)]
        [TestCase(2, AudioCue.ConversionChain)]
        [TestCase(4, AudioCue.ConversionChain)]
        [TestCase(5, AudioCue.ConversionMass)]
        [TestCase(8, AudioCue.ConversionMass)]
        public void ConversionResolved_ConvertedCount_PlaysTheGradedCue(int convertedCount, AudioCue expected)
        {
            // GIVEN
            var result = new ConversionResult(new int[convertedCount], null);

            // WHEN
            MatchEvents.RaiseConversionResolved(LocalPlayerId, result);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void ConversionResolved_NothingConvertedOrStripped_PlaysNothing()
        {
            // GIVEN
            var result = new ConversionResult(new int[0], new int[0]);

            // WHEN
            MatchEvents.RaiseConversionResolved(LocalPlayerId, result);

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void ConversionResolved_OnlyArmorStripped_PlaysArmorStrip()
        {
            // GIVEN
            var result = new ConversionResult(new int[0], new int[2]);

            // WHEN
            MatchEvents.RaiseConversionResolved(LocalPlayerId, result);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.ArmorStrip }));
        }

        [Test]
        public void ConversionResolved_ConvertedAndArmorStripped_PlaysTheConversionThenTheArmorStrip()
        {
            // GIVEN
            var result = new ConversionResult(new int[1], new int[1]);

            // WHEN
            MatchEvents.RaiseConversionResolved(LocalPlayerId, result);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.ConversionSingle, AudioCue.ArmorStrip }));
        }

        [TestCase(StatusType.Frozen, AudioCue.Freeze)]
        [TestCase(StatusType.Rooted, AudioCue.Root)]
        public void StatusApplied_FirstOfAKind_PlaysTheMatchingCue(StatusType status, AudioCue expected)
        {
            // GIVEN
            StatusChange change = BuildStatus(status);

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void StatusApplied_SecondFrozenInTheSameDeployment_IsSilent()
        {
            // GIVEN
            StatusChange change = BuildStatus(StatusType.Frozen);
            MatchEvents.RaiseStatusApplied(in change);

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Freeze }));
        }

        [Test]
        public void StatusApplied_FrozenAfterTheDeploymentResolved_PlaysAgain()
        {
            // GIVEN
            StatusChange change = BuildStatus(StatusType.Frozen);
            MatchEvents.RaiseStatusApplied(in change);
            MatchEvents.RaiseAbilityResolved(LocalPlayerId, BuildAbilityResult(ImpactEffectFlags.None));

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Freeze, AudioCue.Freeze }));
        }

        [Test]
        public void StatusApplied_FrozenAfterAMoveExecutedWhoseDeploymentNeverResolved_PlaysAgain()
        {
            // GIVEN
            StatusChange change = BuildStatus(StatusType.Frozen);
            MatchEvents.RaiseStatusApplied(in change);
            MatchEvents.RaiseMoveExecuted(MoveCommand.ForDeploy(_origin, LocalPlayerId), new List<HexCoordinates> { _origin });

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Freeze, AudioCue.Freeze }));
        }

        [Test]
        public void StatusApplied_FrozenAfterASuccessfulCardPlayWhoseDeploymentNeverResolved_PlaysAgain()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());
            StatusChange change = BuildStatus(StatusType.Frozen);
            MatchEvents.RaiseStatusApplied(in change);
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(LocalPlayerId, CardPlayResult.Success));

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Freeze, AudioCue.Freeze }));
        }

        [Test]
        public void StatusApplied_FrozenAfterARefusedCardPlay_PlaysAgain()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());
            StatusChange change = BuildStatus(StatusType.Frozen);
            MatchEvents.RaiseStatusApplied(in change);
            MatchEvents.RaiseCardPlayAttempted(BuildAttempt(OpponentPlayerId, CardPlayResult.IllegalPlacement));

            // WHEN
            MatchEvents.RaiseStatusApplied(in change);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Freeze, AudioCue.Freeze }));
        }

        [TestCase(ImpactEffectFlags.SpawnHazard, AudioCue.HazardSpawned)]
        [TestCase(ImpactEffectFlags.SelfDestruct, AudioCue.Detonation)]
        public void AbilityResolved_ResolvedEffect_PlaysTheMatchingCue(ImpactEffectFlags effects, AudioCue expected)
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(effects);

            // WHEN
            MatchEvents.RaiseAbilityResolved(LocalPlayerId, result);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void AbilityResolved_ApplyStatusOnly_PlaysNothing()
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(ImpactEffectFlags.ApplyStatus);

            // WHEN
            MatchEvents.RaiseAbilityResolved(LocalPlayerId, result);

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void FuseExpired_AnyUnit_PlaysDetonation()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseFuseExpired(UnitId, OpponentPlayerId);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Detonation }));
        }

        [Test]
        public void CardDiscarded_ByTheLocalPlayer_PlaysDiscard()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardDiscarded(LocalPlayerId, new CardId(CardIdValue), 0);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.Discard }));
        }

        [Test]
        public void CardDiscarded_ByTheOpponent_PlaysNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseCardDiscarded(OpponentPlayerId, new CardId(CardIdValue), 0);

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void MatchPhaseChanged_FirstOvertime_PlaysOvertimeWarning()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.OvertimeWarning }));
        }

        [Test]
        public void MatchPhaseChanged_OvertimeEnteredTwice_WarnsOnce()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.OvertimeWarning }));
        }

        [Test]
        public void MatchPhaseChanged_OvertimeInANewMatch_WarnsAgain()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // WHEN
            MatchEvents.RaiseMatchStarted(BuildConfig());
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.OvertimeWarning, AudioCue.OvertimeWarning }));
        }

        [Test]
        public void MatchPhaseChanged_StandardPhase_PlaysNothing()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void MatchEnded_AnyOutcome_PlaysNothing()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.Domination));

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [TestCase(InteractionFeedback.Selected, AudioCue.Select)]
        [TestCase(InteractionFeedback.DragStarted, AudioCue.DragStart)]
        [TestCase(InteractionFeedback.DropRejected, AudioCue.DropRejected)]
        public void FeedbackRaised_GestureStep_PlaysTheMatchingCue(InteractionFeedback feedback, AudioCue expected)
        {
            // GIVEN

            // WHEN
            _feedbackSource.Raise(feedback);

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void FeedbackRaised_Cleared_PlaysNothing()
        {
            // GIVEN

            // WHEN
            _feedbackSource.Raise(InteractionFeedback.Cleared);

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void OnDisable_BoardFactRaised_PlaysNothing()
        {
            // GIVEN
            _controllerGO.SetActive(false);

            // WHEN
            MatchEvents.RaiseMoveExecuted(BuildMove(MoveType.Clone), new List<HexCoordinates> { _neighbor });

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void OnDisable_GestureFeedbackRaised_PlaysNothing()
        {
            // GIVEN
            _controllerGO.SetActive(false);

            // WHEN
            _feedbackSource.Raise(InteractionFeedback.Selected);

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [Test]
        public void OnDisable_Always_ReleasesTheGestureSource()
        {
            // GIVEN

            // WHEN
            _controllerGO.SetActive(false);

            // THEN
            Assert.That(_feedbackSource.SubscriberCount, Is.EqualTo(0));
        }

        [Test]
        public void OnEnable_AfterADisableCycle_PlaysEachFactExactlyOnce()
        {
            // GIVEN
            _controllerGO.SetActive(false);
            _controllerGO.SetActive(true);

            // WHEN
            MatchEvents.RaiseMoveExecuted(BuildMove(MoveType.Jump), new List<HexCoordinates> { _neighbor });

            // THEN
            Assert.That(_fakeService.PlayedCues, Is.EqualTo(new[] { AudioCue.JumpMove }));
        }

        [Test]
        public void Construct_NullAudioService_LogsSfxServiceMissing()
        {
            // GIVEN
            var inertGO = new GameObject(nameof(MatchSfxController) + "_NullService");
            inertGO.SetActive(false);
            MatchSfxController inert = inertGO.AddComponent<MatchSfxController>();
            LogAssert.Expect(LogType.Warning, AudioLogMessages.SfxServiceMissing);

            try
            {
                // WHEN
                inert.Construct(null, _feedbackSource);

                // THEN
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.Destroy(inertGO);
            }
        }

        [Test]
        public void Construct_NullAudioService_IgnoresAMoveWithoutThrowing()
        {
            // GIVEN
            var inertGO = new GameObject(nameof(MatchSfxController) + "_NullServiceMove");
            inertGO.SetActive(false);
            MatchSfxController inert = inertGO.AddComponent<MatchSfxController>();
            LogAssert.Expect(LogType.Warning, AudioLogMessages.SfxServiceMissing);
            inert.Construct(null, _feedbackSource);
            inertGO.SetActive(true);

            try
            {
                // WHEN / THEN
                Assert.DoesNotThrow(() => MatchEvents.RaiseMoveExecuted(BuildMove(MoveType.Clone), new List<HexCoordinates> { _neighbor }));
            }
            finally
            {
                Object.Destroy(inertGO);
            }
        }

        [Test]
        public void Construct_NullFeedbackSource_StillPlaysBoardCues()
        {
            // GIVEN
            var otherGO = new GameObject(nameof(MatchSfxController) + "_NullSource");
            otherGO.SetActive(false);
            MatchSfxController other = otherGO.AddComponent<MatchSfxController>();
            var otherService = new FakeAudioService();
            _controllerGO.SetActive(false);

            try
            {
                other.Construct(otherService, null);
                otherGO.SetActive(true);

                // WHEN
                MatchEvents.RaiseMoveExecuted(BuildMove(MoveType.Clone), new List<HexCoordinates> { _neighbor });

                // THEN
                Assert.That(otherService.PlayedCues, Is.EqualTo(new[] { AudioCue.CloneMove }));
            }
            finally
            {
                Object.Destroy(otherGO);
            }
        }

        [Test]
        public void Construct_NullFeedbackSource_ToleratesBeingEnabledAndDisabled()
        {
            // GIVEN
            var otherGO = new GameObject(nameof(MatchSfxController) + "_NullSourceCycle");
            otherGO.SetActive(false);
            MatchSfxController other = otherGO.AddComponent<MatchSfxController>();
            other.Construct(new FakeAudioService(), null);

            try
            {
                // WHEN / THEN
                Assert.DoesNotThrow(() =>
                {
                    otherGO.SetActive(true);
                    otherGO.SetActive(false);
                });
            }
            finally
            {
                Object.Destroy(otherGO);
            }
        }

        [Test]
        [Category("Allocation")]
        public void ConversionResolvedAndMoveExecuted_AfterWarmup_AllocateNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());
            MoveCommand clone = BuildMove(MoveType.Clone);
            var affected = new List<HexCoordinates> { _neighbor };
            var conversion = new ConversionResult(new int[3], new int[1]);

            void code()
            {
                for (int i = 0; i < AllocationIterations; i++)
                {
                    MatchEvents.RaiseMoveExecuted(clone, affected);
                    MatchEvents.RaiseConversionResolved(LocalPlayerId, conversion);
                }
            }

            code();
            _fakeService.CallLog.Clear();
            _fakeService.PlayedCues.Clear();

            // WHEN / THEN
            Assert.That(code, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void StatusAppliedAndAbilityResolved_AfterWarmup_AllocateNothing()
        {
            // GIVEN
            StatusChange frozen = BuildStatus(StatusType.Frozen);
            StatusChange rooted = BuildStatus(StatusType.Rooted);
            AbilityResult ability = BuildAbilityResult(ImpactEffectFlags.SpawnHazard | ImpactEffectFlags.SelfDestruct);

            void code()
            {
                for (int i = 0; i < AllocationIterations; i++)
                {
                    MatchEvents.RaiseStatusApplied(in frozen);
                    MatchEvents.RaiseStatusApplied(in rooted);
                    MatchEvents.RaiseAbilityResolved(LocalPlayerId, ability);
                }
            }

            code();
            ClearRecordedCalls();

            // WHEN / THEN
            Assert.That(code, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void CardPlayAttempted_AfterWarmup_AllocatesNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchStarted(BuildConfig());
            CardPlayAttempt success = BuildAttempt(LocalPlayerId, CardPlayResult.Success);
            CardPlayAttempt refused = BuildAttempt(LocalPlayerId, CardPlayResult.IllegalPlacement);

            void code()
            {
                for (int i = 0; i < AllocationIterations; i++)
                {
                    MatchEvents.RaiseCardPlayAttempted(success);
                    MatchEvents.RaiseCardPlayAttempted(refused);
                }
            }

            code();
            ClearRecordedCalls();

            // WHEN / THEN
            Assert.That(code, new AllocatesNothingConstraint());
        }

        [Test]
        [Category("Allocation")]
        public void FeedbackRaised_AfterWarmup_AllocatesNothing()
        {
            // GIVEN
            void code()
            {
                for (int i = 0; i < AllocationIterations; i++)
                {
                    _feedbackSource.Raise(InteractionFeedback.Selected);
                    _feedbackSource.Raise(InteractionFeedback.DragStarted);
                    _feedbackSource.Raise(InteractionFeedback.Cleared);
                }
            }

            code();
            ClearRecordedCalls();

            // WHEN / THEN
            Assert.That(code, new AllocatesNothingConstraint());
        }

        private static MatchConfiguration BuildConfig()
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(LocalPlayerId, PlayerControl.LocalHuman),
                new PlayerSlot(OpponentPlayerId, PlayerControl.Machine),
                StandardDurationSeconds,
                5f,
                30f
            );
        }

        private static MatchConfiguration BuildConfigWithoutLocalSeat()
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(LocalPlayerId, PlayerControl.Machine),
                new PlayerSlot(OpponentPlayerId, PlayerControl.Machine),
                StandardDurationSeconds,
                5f,
                30f
            );
        }

        private static CardPlayAttempt BuildAttempt(int playerId, CardPlayResult result)
        {
            return new CardPlayAttempt(playerId, new CardId(CardIdValue), _origin, 2, result);
        }

        private static MoveCommand BuildMove(MoveType type)
        {
            return new MoveCommand(type, _origin, _neighbor, LocalPlayerId, UnitId);
        }

        private static StatusChange BuildStatus(StatusType status)
        {
            return new StatusChange(UnitId, OpponentPlayerId, LocalPlayerId, status, 1);
        }

        private static AbilityResult BuildAbilityResult(ImpactEffectFlags effects)
        {
            return new AbilityResult(null, null, null, 0, 0, effects);
        }

        private void ClearRecordedCalls()
        {
            _fakeService.CallLog.Clear();
            _fakeService.PlayedCues.Clear();
            _fakeService.PlayedCardCues.Clear();
        }
    }
}
