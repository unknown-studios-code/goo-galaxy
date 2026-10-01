using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Audio
{
    [TestFixture]
    public class AdaptiveMusicModelTests
    {
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const float StandardDurationSeconds = 200f;
        private const float MidBandRemainingSeconds = 130f;
        private const float StayingInMidBandRemainingSeconds = 120f;
        private const float MidIntensity = 0.5f;
        private const float ConstructionIntensity = 0f;
        private const float Tolerance = 0.0001f;

        [Test]
        public void ApplyPhase_LoadingFromNoMatch_StartsMatchMusic()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();

            // WHEN
            MusicChange changes = model.ApplyPhase(MatchPhase.Loading);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Music), Is.True);
            Assert.That(model.MusicState, Is.EqualTo(MusicState.Match));
        }

        [Test]
        public void ApplyPhase_CountdownAfterLoadingAlreadyStartedMusic_ReportsNoMusicChange()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyPhase(MatchPhase.Loading);

            // WHEN
            MusicChange changes = model.ApplyPhase(MatchPhase.Countdown);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Music), Is.False);
        }

        [Test]
        public void ApplyPhase_CountdownWithoutPriorLoading_StartsMatchMusic()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();

            // WHEN
            MusicChange changes = model.ApplyPhase(MatchPhase.Countdown);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Music), Is.True);
            Assert.That(model.MusicState, Is.EqualTo(MusicState.Match));
        }

        [Test]
        public void ApplyPhase_NoneAfterMusicStarted_StopsMatchMusic()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyPhase(MatchPhase.Loading);

            // WHEN
            MusicChange changes = model.ApplyPhase(MatchPhase.None);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Music), Is.True);
            Assert.That(model.MusicState, Is.EqualTo(MusicState.None));
        }

        [Test]
        public void ApplyClock_CrossingIntoAnotherBand_ReturnsIntensityChange()
        {
            // GIVEN — 200 seconds of Standard, ticked down to 130 remaining: 70 seconds elapsed crosses from early into mid.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyPhase(MatchPhase.Standard);

            // WHEN
            MusicChange changes = model.ApplyClock((int)MidBandRemainingSeconds);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Intensity), Is.True);
            Assert.That(model.Intensity, Is.EqualTo(MidIntensity).Within(Tolerance));
        }

        [Test]
        public void ApplyClock_StayingInTheSameBand_ReturnsNone()
        {
            // GIVEN — already at 70 seconds elapsed (mid); ticking to 80 elapsed stays in the same band.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyPhase(MatchPhase.Standard);
            model.ApplyClock((int)MidBandRemainingSeconds);

            // WHEN
            MusicChange changes = model.ApplyClock((int)StayingInMidBandRemainingSeconds);

            // THEN
            Assert.That(changes, Is.EqualTo(MusicChange.None));
        }

        [Test]
        public void ApplyClock_PhaseNotStandard_ReturnsNone()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyPhase(MatchPhase.Loading);

            // WHEN
            MusicChange changes = model.ApplyClock(30);

            // THEN
            Assert.That(changes, Is.EqualTo(MusicChange.None));
        }

        [Test]
        public void ApplyClock_BeforeMatchStarted_ReturnsNone()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();

            // WHEN
            MusicChange changes = model.ApplyClock(30);

            // THEN
            Assert.That(changes, Is.EqualTo(MusicChange.None));
        }

        [Test]
        public void ApplyScore_ThreeLocalUnitsOneOpponentUnit_ComputesHalfBalance()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyScore(LocalPlayerId, 3);

            // WHEN
            model.ApplyScore(OpponentPlayerId, 1);

            // THEN
            Assert.That(model.Balance, Is.EqualTo(0.5f).Within(Tolerance));
        }

        [Test]
        public void ApplyScore_TwoLocalUnitsOneOpponentUnit_QuantisesBalanceToTheNearestStep()
        {
            // GIVEN — the raw ratio is 1/3 (0.333...); the quantised report is the nearest BalanceStep, not that ratio.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyScore(LocalPlayerId, 2);

            // WHEN
            model.ApplyScore(OpponentPlayerId, 1);

            // THEN
            Assert.That(model.Balance, Is.EqualTo(0.35f).Within(Tolerance));
        }

        [Test]
        public void ApplyScore_SeatsSwapped_ReportsTheExactOppositeBalance()
        {
            // GIVEN
            AdaptiveMusicModel modelWithSeatOneLocal = BuildModel();
            modelWithSeatOneLocal.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            AdaptiveMusicModel modelWithSeatTwoLocal = BuildModel();
            modelWithSeatTwoLocal.ApplyMatchStarted(BuildConfigWithSeatTwoLocal(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            modelWithSeatOneLocal.ApplyScore(LocalPlayerId, 3);
            modelWithSeatOneLocal.ApplyScore(OpponentPlayerId, 1);
            modelWithSeatTwoLocal.ApplyScore(LocalPlayerId, 3);
            modelWithSeatTwoLocal.ApplyScore(OpponentPlayerId, 1);

            // THEN
            Assert.That(modelWithSeatTwoLocal.Balance, Is.EqualTo(-modelWithSeatOneLocal.Balance).Within(Tolerance));
        }

        [Test]
        public void ApplyScore_LocalSeatUnresolved_KeepsBalanceEvenRegardlessOfCounts()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfigWithoutLocalSeat(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            model.ApplyScore(LocalPlayerId, 10);
            model.ApplyScore(OpponentPlayerId, 1);

            // THEN
            Assert.That(model.Balance, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyScore_UnknownPlayerId_ReturnsNone()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            MusicChange changes = model.ApplyScore(playerId: 999, unitCount: 5);

            // THEN
            Assert.That(changes, Is.EqualTo(MusicChange.None));
        }

        [Test]
        public void ApplyMatchEnded_LocalPlayerWins_ChoosesVictoryStinger()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            MusicChange changes = model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger stinger);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Stinger), Is.True);
            Assert.That(stinger, Is.EqualTo(MatchStinger.Victory));
        }

        [Test]
        public void ApplyMatchEnded_OpponentWins_ChoosesDefeatStinger()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            MusicChange changes = model.ApplyMatchEnded(new MatchOutcome(OpponentPlayerId, MatchEndReason.TimeLimit), out MatchStinger stinger);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Stinger), Is.True);
            Assert.That(stinger, Is.EqualTo(MatchStinger.Defeat));
        }

        [Test]
        public void ApplyMatchEnded_Draw_ChoosesDrawStinger()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            MusicChange changes = model.ApplyMatchEnded(MatchOutcome.Drawn, out MatchStinger stinger);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Stinger), Is.True);
            Assert.That(stinger, Is.EqualTo(MatchStinger.Draw));
        }

        [Test]
        public void ApplyMatchEnded_LocalSeatUnresolved_ChoosesNoStinger()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfigWithoutLocalSeat(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            MusicChange changes = model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // THEN
            Assert.That(changes.HasFlag(MusicChange.Stinger), Is.False);
        }

        [Test]
        public void ApplyMatchEnded_LocalSeatUnresolved_YieldsNoneStinger()
        {
            // GIVEN — regression for a default(MatchStinger) that used to read as Victory before None became the zero value.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfigWithoutLocalSeat(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // WHEN
            model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger stinger);

            // THEN
            Assert.That(stinger, Is.EqualTo(MatchStinger.None));
        }

        [Test]
        public void ApplyMatchEnded_CalledASecondTime_ReturnsNone()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // WHEN
            MusicChange changes = model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // THEN
            Assert.That(changes, Is.EqualTo(MusicChange.None));
        }

        [Test]
        public void ApplyMatchStarted_NoScoreApplied_ReportsAnEvenBalance()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();

            // WHEN
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // THEN
            Assert.That(model.Balance, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyMatchStarted_Rematch_ClearsHasMatchEnded()
        {
            // GIVEN — Loading is raised before MatchStarted in production, so the reset must not need it re-raised.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyPhase(MatchPhase.Loading);
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyScore(LocalPlayerId, 5);
            model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // WHEN
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // THEN
            Assert.That(model.HasMatchEnded, Is.False);
        }

        [Test]
        public void ApplyMatchStarted_Rematch_ResetsBalanceToEven()
        {
            // GIVEN — Loading is raised before MatchStarted in production, so the reset must not need it re-raised.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyPhase(MatchPhase.Loading);
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyScore(LocalPlayerId, 5);
            model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // WHEN
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // THEN
            Assert.That(model.Balance, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ApplyMatchStarted_Rematch_KeepsThePhase()
        {
            // GIVEN — Loading is raised before MatchStarted in production, so the reset must not need it re-raised.
            AdaptiveMusicModel model = BuildModel();
            model.ApplyPhase(MatchPhase.Loading);
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyScore(LocalPlayerId, 5);
            model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // WHEN
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));

            // THEN
            Assert.That(model.Phase, Is.EqualTo(MatchPhase.Loading));
        }

        [Test]
        public void Reset_AfterMatchActivityAndMatchEnd_ReturnsToConstructionState()
        {
            // GIVEN
            AdaptiveMusicModel model = BuildModel();
            model.ApplyPhase(MatchPhase.Loading);
            model.ApplyMatchStarted(BuildConfig(LocalPlayerId, OpponentPlayerId, StandardDurationSeconds));
            model.ApplyScore(LocalPlayerId, 5);
            model.ApplyMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit), out MatchStinger _);

            // WHEN
            model.Reset();

            // THEN
            Assert.That(model.Phase, Is.EqualTo(MatchPhase.None));
            Assert.That(model.MusicState, Is.EqualTo(MusicState.None));
            Assert.That(model.Intensity, Is.EqualTo(ConstructionIntensity).Within(Tolerance));
            Assert.That(model.Balance, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(model.HasMatchStarted, Is.False);
            Assert.That(model.HasLocalSeat, Is.False);
            Assert.That(model.HasMatchEnded, Is.False);
        }

        private static AdaptiveMusicModel BuildModel()
        {
            var bands = new IntensityBands(ConstructionIntensity, 0.2f, 60f, 0.5f, 120f, 0.7f, 0.85f, 15f, 1f);

            return new AdaptiveMusicModel(in bands);
        }

        private static MatchConfiguration BuildConfig(int localPlayerId, int opponentPlayerId, float standardDurationSeconds)
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(localPlayerId, PlayerControl.LocalHuman),
                new PlayerSlot(opponentPlayerId, PlayerControl.Machine),
                standardDurationSeconds,
                5f,
                30f
            );
        }

        private static MatchConfiguration BuildConfigWithSeatTwoLocal(int seatOneId, int seatTwoId, float standardDurationSeconds)
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(seatOneId, PlayerControl.Machine),
                new PlayerSlot(seatTwoId, PlayerControl.LocalHuman),
                standardDurationSeconds,
                5f,
                30f
            );
        }

        private static MatchConfiguration BuildConfigWithoutLocalSeat(int seatOneId, int seatTwoId, float standardDurationSeconds)
        {
            return new MatchConfiguration(
                0,
                new PlayerSlot(seatOneId, PlayerControl.Machine),
                new PlayerSlot(seatTwoId, PlayerControl.Machine),
                standardDurationSeconds,
                5f,
                30f
            );
        }
    }
}
