using System.Collections.Generic;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Audio
{
    [TestFixture]
    public class MatchSfxModelTests
    {
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const int UnitId = 7;
        private const int AllocationIterations = 200;
        private const string CardIdValue = "acid_crawler";

        private static readonly HexCoordinates _origin = new(0, 0);
        private static readonly HexCoordinates _neighbor = new(1, 0);

        private MatchSfxModel _model;

        [SetUp]
        public void SetUp()
        {
            _model = new MatchSfxModel();
        }

        [TestCase(-1, ConversionTier.None)]
        [TestCase(0, ConversionTier.None)]
        [TestCase(1, ConversionTier.Single)]
        [TestCase(2, ConversionTier.Chain)]
        [TestCase(4, ConversionTier.Chain)]
        [TestCase(5, ConversionTier.Mass)]
        [TestCase(12, ConversionTier.Mass)]
        public void ResolveConversionTier_ForAConvertedCount_GradesByTheBounds(int convertedCount, ConversionTier expected)
        {
            // GIVEN

            // WHEN
            ConversionTier tier = MatchSfxModel.ResolveConversionTier(convertedCount);

            // THEN
            Assert.That(tier, Is.EqualTo(expected));
        }

        [Test]
        public void HasLocalSeat_BeforeAnyMatchIsAnnounced_IsFalse()
        {
            // GIVEN

            // WHEN
            bool hasLocalSeat = _model.HasLocalSeat;

            // THEN
            Assert.That(hasLocalSeat, Is.False);
        }

        [Test]
        public void LocalPlayerId_BeforeAnyMatchIsAnnounced_IsUnassigned()
        {
            // GIVEN

            // WHEN
            int localPlayerId = _model.LocalPlayerId;

            // THEN
            Assert.That(localPlayerId, Is.EqualTo(PlayerSlot.UnassignedId));
        }

        [Test]
        public void ApplyMatchStarted_LocalHumanInSeatOne_ResolvesTheLocalSeat()
        {
            // GIVEN
            MatchConfiguration config = BuildConfig(LocalPlayerId, PlayerControl.LocalHuman, OpponentPlayerId, PlayerControl.Machine);

            // WHEN
            _model.ApplyMatchStarted(in config);

            // THEN
            Assert.That(_model.HasLocalSeat, Is.True);
            Assert.That(_model.LocalPlayerId, Is.EqualTo(LocalPlayerId));
        }

        [Test]
        public void ApplyMatchStarted_LocalHumanInSeatTwo_ResolvesTheSecondSeatAsLocal()
        {
            // GIVEN
            MatchConfiguration config = BuildConfig(OpponentPlayerId, PlayerControl.Machine, LocalPlayerId, PlayerControl.LocalHuman);

            // WHEN
            _model.ApplyMatchStarted(in config);

            // THEN
            Assert.That(_model.HasLocalSeat, Is.True);
            Assert.That(_model.LocalPlayerId, Is.EqualTo(LocalPlayerId));
        }

        [Test]
        public void ApplyMatchStarted_NoLocalHuman_HasNoLocalSeat()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();

            // WHEN
            _model.ApplyMatchStarted(in config);

            // THEN
            Assert.That(_model.HasLocalSeat, Is.False);
        }

        [Test]
        public void TryResolveCardPlayed_SuccessfulPlay_ReturnsTheCardPlayed()
        {
            // GIVEN
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, CardPlayResult.Success);

            // WHEN
            bool wasResolved = _model.TryResolveCardPlayed(in attempt, out CardId card);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(card, Is.EqualTo(new CardId(CardIdValue)));
        }

        [Test]
        public void TryResolveCardPlayed_OpponentSuccessfulPlayWithNoLocalSeat_ReturnsTrue()
        {
            // GIVEN
            CardPlayAttempt attempt = BuildAttempt(OpponentPlayerId, CardPlayResult.Success);

            // WHEN
            bool wasResolved = _model.TryResolveCardPlayed(in attempt, out CardId _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [TestCase(CardPlayResult.IllegalPlacement)]
        [TestCase(CardPlayResult.InsufficientEnergy)]
        [TestCase(CardPlayResult.InvalidTargetCount)]
        [TestCase(CardPlayResult.ResolverBusy)]
        [TestCase(CardPlayResult.MatchNotInPlay)]
        public void TryResolveCardPlayed_RefusedPlay_ReturnsFalse(CardPlayResult result)
        {
            // GIVEN
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, result);

            // WHEN
            bool wasResolved = _model.TryResolveCardPlayed(in attempt, out CardId _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [TestCase(CardPlayResult.IllegalPlacement)]
        [TestCase(CardPlayResult.InsufficientEnergy)]
        [TestCase(CardPlayResult.InvalidTargetCount)]
        public void TryResolvePlayRejected_LocalSeatRefusedByAPlayerMistake_ReturnsThePlayRejectedCue(CardPlayResult result)
        {
            // GIVEN
            StartMatchWithLocalSeat();
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, result);

            // WHEN
            bool wasResolved = _model.TryResolvePlayRejected(in attempt, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.PlayRejected));
        }

        [TestCase(CardPlayResult.ResolverBusy)]
        [TestCase(CardPlayResult.UnknownPlayer)]
        [TestCase(CardPlayResult.SlotOutOfRange)]
        [TestCase(CardPlayResult.CardNotFound)]
        [TestCase(CardPlayResult.BoardUnavailable)]
        [TestCase(CardPlayResult.MatchNotInPlay)]
        [TestCase(CardPlayResult.Success)]
        public void TryResolvePlayRejected_LocalSeatWithAResultThePlayerDidNotCause_IsSilent(CardPlayResult result)
        {
            // GIVEN
            StartMatchWithLocalSeat();
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, result);

            // WHEN
            bool wasResolved = _model.TryResolvePlayRejected(in attempt, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolvePlayRejected_OpponentIllegalPlacement_IsSilent()
        {
            // GIVEN
            StartMatchWithLocalSeat();
            CardPlayAttempt attempt = BuildAttempt(OpponentPlayerId, CardPlayResult.IllegalPlacement);

            // WHEN
            bool wasResolved = _model.TryResolvePlayRejected(in attempt, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolvePlayRejected_NoLocalSeat_IsSilent()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, CardPlayResult.IllegalPlacement);

            // WHEN
            bool wasResolved = _model.TryResolvePlayRejected(in attempt, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolvePlayRejected_BeforeAnyMatchIsAnnounced_IsSilent()
        {
            // GIVEN
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, CardPlayResult.IllegalPlacement);

            // WHEN
            bool wasResolved = _model.TryResolvePlayRejected(in attempt, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [TestCase(MoveType.Clone, AudioCue.CloneMove)]
        [TestCase(MoveType.Jump, AudioCue.JumpMove)]
        public void TryResolveMove_CloneOrJump_ReturnsTheMatchingCue(MoveType type, AudioCue expected)
        {
            // GIVEN
            var command = new MoveCommand(type, _origin, _neighbor, OpponentPlayerId, UnitId);

            // WHEN
            bool wasResolved = _model.TryResolveMove(in command, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(expected));
        }

        [Test]
        public void TryResolveMove_NoLocalSeat_StillReturnsTheCue()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);
            var command = new MoveCommand(MoveType.Clone, _origin, _neighbor, OpponentPlayerId, UnitId);

            // WHEN
            bool wasResolved = _model.TryResolveMove(in command, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolveMove_Deploy_IsSilent()
        {
            // GIVEN
            var command = MoveCommand.ForDeploy(_origin, LocalPlayerId);

            // WHEN
            bool wasResolved = _model.TryResolveMove(in command, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [TestCase(1, AudioCue.ConversionSingle)]
        [TestCase(2, AudioCue.ConversionChain)]
        [TestCase(4, AudioCue.ConversionChain)]
        [TestCase(5, AudioCue.ConversionMass)]
        [TestCase(12, AudioCue.ConversionMass)]
        public void TryResolveConversion_ConvertedUnits_ReturnsTheCueOfTheirTier(int convertedCount, AudioCue expected)
        {
            // GIVEN
            var result = new ConversionResult(BuildIds(convertedCount), null);

            // WHEN
            bool wasResolved = _model.TryResolveConversion(in result, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(expected));
        }

        [Test]
        public void TryResolveConversion_LandingThatAlsoStrippedArmor_StillReturnsTheConversionCue()
        {
            // GIVEN
            var result = new ConversionResult(BuildIds(2), BuildIds(1));

            // WHEN
            bool wasResolved = _model.TryResolveConversion(in result, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.ConversionChain));
        }

        [Test]
        public void TryResolveConversion_NoLocalSeat_StillReturnsTheCue()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);
            var result = new ConversionResult(BuildIds(1), null);

            // WHEN
            bool wasResolved = _model.TryResolveConversion(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolveConversion_NoConvertedUnits_IsSilent()
        {
            // GIVEN
            var result = new ConversionResult(new List<int>(), BuildIds(2));

            // WHEN
            bool wasResolved = _model.TryResolveConversion(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveConversion_NullConvertedList_IsSilent()
        {
            // GIVEN
            var result = new ConversionResult(null, null);

            // WHEN
            bool wasResolved = _model.TryResolveConversion(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveArmorStrip_StrippedUnits_ReturnsTheArmorStripCue()
        {
            // GIVEN
            var result = new ConversionResult(null, BuildIds(1));

            // WHEN
            bool wasResolved = _model.TryResolveArmorStrip(in result, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.ArmorStrip));
        }

        [Test]
        public void TryResolveArmorStrip_LandingThatAlsoConverted_StillReturnsTheArmorStripCue()
        {
            // GIVEN
            var result = new ConversionResult(BuildIds(2), BuildIds(1));

            // WHEN
            bool wasResolved = _model.TryResolveArmorStrip(in result, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.ArmorStrip));
        }

        [Test]
        public void TryResolveArmorStrip_NoStrippedUnits_IsSilent()
        {
            // GIVEN
            var result = new ConversionResult(BuildIds(3), new List<int>());

            // WHEN
            bool wasResolved = _model.TryResolveArmorStrip(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [TestCase(StatusType.Frozen, AudioCue.Freeze)]
        [TestCase(StatusType.Rooted, AudioCue.Root)]
        public void TryResolveStatusApplied_FirstOfAKind_ReturnsTheMatchingCue(StatusType status, AudioCue expected)
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, status);

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(expected));
        }

        [Test]
        public void TryResolveStatusApplied_RootedAfterFrozenInTheSameDeployment_ReturnsTheRootCue()
        {
            // GIVEN
            StatusChange frozen = BuildStatusChange(UnitId, StatusType.Frozen);
            StatusChange rooted = BuildStatusChange(UnitId + 1, StatusType.Rooted);
            _model.TryResolveStatusApplied(in frozen, out AudioCue _);

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in rooted, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.Root));
        }

        [Test]
        public void TryResolveStatusApplied_FrozenAfterANewMatchStarted_ReturnsTheFreezeCueAgain()
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, StatusType.Frozen);
            _model.TryResolveStatusApplied(in change, out AudioCue _);
            StartMatchWithLocalSeat();

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolveStatusApplied_ConditionWithNoSoundBeforeAFrozen_DoesNotSilenceTheFrozen()
        {
            // GIVEN
            StatusChange none = BuildStatusChange(UnitId, StatusType.None);
            StatusChange frozen = BuildStatusChange(UnitId, StatusType.Frozen);
            _model.TryResolveStatusApplied(in none, out AudioCue _);

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in frozen, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolveStatusApplied_SecondFrozenInTheSameDeployment_IsSilent()
        {
            // GIVEN
            StatusChange first = BuildStatusChange(UnitId, StatusType.Frozen);
            StatusChange second = BuildStatusChange(UnitId + 1, StatusType.Frozen);
            _model.TryResolveStatusApplied(in first, out AudioCue _);

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in second, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveStatusApplied_ConditionWithNoSound_IsSilent()
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, StatusType.None);

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void ResetDeploymentLatch_AfterAFrozenWasHeard_ReturnsTheFreezeCueAgain()
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, StatusType.Frozen);
            _model.TryResolveStatusApplied(in change, out AudioCue _);
            _model.ResetDeploymentLatch();

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.Freeze));
        }

        [Test]
        public void ResetDeploymentLatch_AfterARootedWasHeard_ReturnsTheRootCueAgain()
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, StatusType.Rooted);
            _model.TryResolveStatusApplied(in change, out AudioCue _);
            _model.ResetDeploymentLatch();

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.Root));
        }

        [Test]
        public void ResetDeploymentLatch_CalledTwiceInARow_StillLetsTheNextFrozenPlay()
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, StatusType.Frozen);
            _model.TryResolveStatusApplied(in change, out AudioCue _);
            _model.ResetDeploymentLatch();
            _model.ResetDeploymentLatch();

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void ResetDeploymentLatch_AfterTheLatchIsClear_DoesNotSilenceTheNextFrozen()
        {
            // GIVEN
            StatusChange change = BuildStatusChange(UnitId, StatusType.Frozen);
            _model.ResetDeploymentLatch();

            // WHEN
            bool wasResolved = _model.TryResolveStatusApplied(in change, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolveHazardSpawned_SpawnHazardFlag_ReturnsTheHazardCue()
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(ImpactEffectFlags.SpawnHazard);

            // WHEN
            bool wasResolved = _model.TryResolveHazardSpawned(in result, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.HazardSpawned));
        }

        [Test]
        public void TryResolveHazardSpawned_SpawnHazardAmongOtherFlags_ReturnsTheHazardCue()
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(ImpactEffectFlags.ApplyStatus | ImpactEffectFlags.SpawnHazard);

            // WHEN
            bool wasResolved = _model.TryResolveHazardSpawned(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolveHazardSpawned_NoLocalSeat_StillReturnsTheCue()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);
            AbilityResult result = BuildAbilityResult(ImpactEffectFlags.SpawnHazard);

            // WHEN
            bool wasResolved = _model.TryResolveHazardSpawned(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [TestCase(ImpactEffectFlags.None)]
        [TestCase(ImpactEffectFlags.ApplyStatus)]
        [TestCase(ImpactEffectFlags.SelfDestruct | ImpactEffectFlags.ArmFuse)]
        public void TryResolveHazardSpawned_WithoutTheSpawnHazardFlag_IsSilent(ImpactEffectFlags flags)
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(flags);

            // WHEN
            bool wasResolved = _model.TryResolveHazardSpawned(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveDetonation_SelfDestructFlag_ReturnsTheDetonationCue()
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(ImpactEffectFlags.SelfDestruct);

            // WHEN
            bool wasResolved = _model.TryResolveDetonation(in result, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.Detonation));
        }

        [TestCase(ImpactEffectFlags.None)]
        [TestCase(ImpactEffectFlags.ArmFuse)]
        [TestCase(ImpactEffectFlags.SpawnHazard | ImpactEffectFlags.ApplyStatus)]
        public void TryResolveDetonation_WithoutTheSelfDestructFlag_IsSilent(ImpactEffectFlags flags)
        {
            // GIVEN
            AbilityResult result = BuildAbilityResult(flags);

            // WHEN
            bool wasResolved = _model.TryResolveDetonation(in result, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveFuseExpired_Always_ReturnsTheDetonationCue()
        {
            // GIVEN

            // WHEN
            bool wasResolved = _model.TryResolveFuseExpired(out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.Detonation));
        }

        [Test]
        public void TryResolveCardDiscarded_LocalSeat_ReturnsTheDiscardCue()
        {
            // GIVEN
            StartMatchWithLocalSeat();

            // WHEN
            bool wasResolved = _model.TryResolveCardDiscarded(LocalPlayerId, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.Discard));
        }

        [Test]
        public void TryResolveCardDiscarded_Opponent_IsSilent()
        {
            // GIVEN
            StartMatchWithLocalSeat();

            // WHEN
            bool wasResolved = _model.TryResolveCardDiscarded(OpponentPlayerId, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolveCardDiscarded_NoLocalSeat_IsSilent()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);

            // WHEN
            bool wasResolved = _model.TryResolveCardDiscarded(LocalPlayerId, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolvePhase_FirstOvertime_ReturnsTheOvertimeWarning()
        {
            // GIVEN

            // WHEN
            bool wasResolved = _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(AudioCue.OvertimeWarning));
        }

        [Test]
        public void TryResolvePhase_OvertimeAfterANewMatchStarted_ReturnsTheOvertimeWarningAgain()
        {
            // GIVEN
            _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);
            StartMatchWithLocalSeat();

            // WHEN
            bool wasResolved = _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolvePhase_NoLocalSeat_StillReturnsTheOvertimeWarning()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);

            // WHEN
            bool wasResolved = _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [Test]
        public void TryResolvePhase_SecondOvertimeInTheSameMatch_IsSilent()
        {
            // GIVEN
            _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);

            // WHEN
            bool wasResolved = _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        public void TryResolvePhase_OtherPhaseBetweenTwoOvertimes_DoesNotRearmTheWarning()
        {
            // GIVEN
            _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);
            _model.TryResolvePhase(MatchPhase.Standard, out AudioCue _);

            // WHEN
            bool wasResolved = _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [TestCase(MatchPhase.None)]
        [TestCase(MatchPhase.Loading)]
        [TestCase(MatchPhase.Countdown)]
        [TestCase(MatchPhase.Standard)]
        [TestCase(MatchPhase.OvertimeCheck)]
        [TestCase(MatchPhase.Ended)]
        [TestCase(MatchPhase.Results)]
        public void TryResolvePhase_AnyPhaseButOvertime_IsSilent(MatchPhase phase)
        {
            // GIVEN

            // WHEN
            bool wasResolved = _model.TryResolvePhase(phase, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [TestCase(InteractionFeedback.Selected, AudioCue.Select)]
        [TestCase(InteractionFeedback.DragStarted, AudioCue.DragStart)]
        [TestCase(InteractionFeedback.DropRejected, AudioCue.DropRejected)]
        public void TryResolveInteraction_AudibleStep_ReturnsTheMatchingCue(InteractionFeedback feedback, AudioCue expected)
        {
            // GIVEN

            // WHEN
            bool wasResolved = _model.TryResolveInteraction(feedback, out AudioCue cue);

            // THEN
            Assert.That(wasResolved, Is.True);
            Assert.That(cue, Is.EqualTo(expected));
        }

        [Test]
        public void TryResolveInteraction_NoLocalSeat_StillReturnsTheCue()
        {
            // GIVEN
            MatchConfiguration config = BuildConfigWithoutLocalSeat();
            _model.ApplyMatchStarted(in config);

            // WHEN
            bool wasResolved = _model.TryResolveInteraction(InteractionFeedback.Selected, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.True);
        }

        [TestCase(InteractionFeedback.Cleared)]
        [TestCase(InteractionFeedback.None)]
        public void TryResolveInteraction_ClearedOrNone_IsSilent(InteractionFeedback feedback)
        {
            // GIVEN

            // WHEN
            bool wasResolved = _model.TryResolveInteraction(feedback, out AudioCue _);

            // THEN
            Assert.That(wasResolved, Is.False);
        }

        [Test]
        [Category("Allocation")]
        public void TryResolve_SteadyStateAcrossEveryRule_AllocatesNoManagedMemory()
        {
            // GIVEN — every rule runs once beforehand so the measured passes see only the steady state.
            StartMatchWithLocalSeat();
            CardPlayAttempt attempt = BuildAttempt(LocalPlayerId, CardPlayResult.IllegalPlacement);
            var move = new MoveCommand(MoveType.Jump, _origin, _neighbor, LocalPlayerId, UnitId);
            var conversion = new ConversionResult(BuildIds(3), BuildIds(1));
            StatusChange change = BuildStatusChange(UnitId, StatusType.Frozen);
            AbilityResult ability = BuildAbilityResult(ImpactEffectFlags.SpawnHazard | ImpactEffectFlags.SelfDestruct);
            RunEveryRule(in attempt, in move, in conversion, in change, in ability);

            // WHEN / THEN
            Assert.That(
                () =>
                {
                    for (int i = 0; i < AllocationIterations; i++)
                    {
                        RunEveryRule(in attempt, in move, in conversion, in change, in ability);
                    }
                },
                new AllocatesNothingConstraint()
            );
        }

        private static MatchConfiguration BuildConfig(int seatOneId, PlayerControl seatOneControl, int seatTwoId, PlayerControl seatTwoControl)
        {
            return new MatchConfiguration(0, new PlayerSlot(seatOneId, seatOneControl), new PlayerSlot(seatTwoId, seatTwoControl), 200f, 5f, 30f);
        }

        private static MatchConfiguration BuildConfigWithoutLocalSeat()
        {
            return BuildConfig(LocalPlayerId, PlayerControl.Machine, OpponentPlayerId, PlayerControl.Machine);
        }

        private static CardPlayAttempt BuildAttempt(int playerId, CardPlayResult result)
        {
            return new CardPlayAttempt(playerId, new CardId(CardIdValue), _origin, 2, result);
        }

        private static StatusChange BuildStatusChange(int unitId, StatusType status)
        {
            return new StatusChange(unitId, OpponentPlayerId, LocalPlayerId, status, 1);
        }

        private static AbilityResult BuildAbilityResult(ImpactEffectFlags flags)
        {
            return new AbilityResult(new List<int>(), new List<HexCoordinates>(), new List<int>(), 0, 0, flags);
        }

        private static List<int> BuildIds(int count)
        {
            var ids = new List<int>(count);

            for (int i = 0; i < count; i++)
            {
                ids.Add(UnitId + i);
            }

            return ids;
        }

        private void StartMatchWithLocalSeat()
        {
            MatchConfiguration config = BuildConfig(LocalPlayerId, PlayerControl.LocalHuman, OpponentPlayerId, PlayerControl.Machine);
            _model.ApplyMatchStarted(in config);
        }

        private void RunEveryRule(
            in CardPlayAttempt attempt,
            in MoveCommand move,
            in ConversionResult conversion,
            in StatusChange change,
            in AbilityResult ability
        )
        {
            _model.TryResolveCardPlayed(in attempt, out CardId _);
            _model.TryResolvePlayRejected(in attempt, out AudioCue _);
            _model.TryResolveMove(in move, out AudioCue _);
            _model.TryResolveConversion(in conversion, out AudioCue _);
            _model.TryResolveArmorStrip(in conversion, out AudioCue _);
            _model.TryResolveStatusApplied(in change, out AudioCue _);
            _model.TryResolveHazardSpawned(in ability, out AudioCue _);
            _model.TryResolveDetonation(in ability, out AudioCue _);
            _model.ResetDeploymentLatch();
            _model.TryResolveFuseExpired(out AudioCue _);
            _model.TryResolveCardDiscarded(LocalPlayerId, out AudioCue _);
            _model.TryResolvePhase(MatchPhase.Overtime, out AudioCue _);
            _model.TryResolveInteraction(InteractionFeedback.DragStarted, out AudioCue _);
        }
    }
}
