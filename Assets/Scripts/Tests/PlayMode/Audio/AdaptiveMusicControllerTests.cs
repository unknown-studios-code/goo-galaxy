using System;
using System.Collections;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Audio.Controllers;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Models;
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
    public class AdaptiveMusicControllerTests
    {
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;
        private const float StandardDurationSeconds = 1000f;
        private const float EarlyBandRemainingSeconds = 999f;
        private const float FirstMidBandRemainingSeconds = 930f;
        private const float StayingInMidBandRemainingSeconds = 910f;
        private const float LateBandRemainingSeconds = 850f;
        private const float MidIntensity = 0.5f;
        private const float OvertimeIntensity = 1f;
        private const int MaxFramesToWait = 30;
        private const int AllocationWarmupIterations = 32;
        private const float Tolerance = 0.0001f;

        private GameObject _controllerGO;
        private AdaptiveMusicController _controller;
        private AudioConfigSO _config;
        private FakeAudioService _fakeService;

        [SetUp]
        public void SetUp()
        {
            MatchEvents.ResetEvents();

            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _fakeService = new FakeAudioService();

            _controllerGO = new GameObject(nameof(AdaptiveMusicController));
            _controllerGO.SetActive(false);
            _controller = _controllerGO.AddComponent<AdaptiveMusicController>();
            _controller.Construct(_fakeService, _config);
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

            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void MatchPhaseChanged_Loading_RequestsTheMatchBankExactlyOnce()
        {
            // GIVEN

            // WHEN — Countdown follows Loading, exercising the case that would ask for the bank a second time if
            // "exactly once" did not hold.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [Test]
        public void MatchPhaseChanged_Loading_DoesNotPlayMusicBeforeTheBankFinishesLoading()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_CountdownWithoutPriorLoading_StartsMusicAfterTheBankLoads()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after Countdown started the load");

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.EqualTo(new[] { MusicState.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_OvertimeAfterBankReady_WritesMatchIntensityAtItsMaximum()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            _fakeService.ParameterWrites.Clear();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // THEN
            Assert.That(_fakeService.ParameterWrites, Has.Count.EqualTo(1));
            Assert.That(_fakeService.ParameterWrites[0].Parameter, Is.EqualTo(AudioParameter.MatchIntensity));
            Assert.That(_fakeService.ParameterWrites[0].Value, Is.EqualTo(OvertimeIntensity).Within(Tolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_AfterLoadingMatchStartedAndScores_PlaysMusicThenWritesBothParameters()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            MatchEvents.RaiseScoreChanged(LocalPlayerId, 3);
            MatchEvents.RaiseScoreChanged(OpponentPlayerId, 1);

            // WHEN
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");

            // THEN
            Assert.That(
                _fakeService.CallLog,
                Is.EqualTo(new[] { AudioServiceCall.LoadBankAsync, AudioServiceCall.PlayMusic, AudioServiceCall.SetParameter, AudioServiceCall.SetParameter })
            );
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_AfterAnAbandonedLoadIsSupersededByANewOne_PlaysMusicExactlyOnce()
        {
            // GIVEN — the first Loading kicks off a bank load that None abandons before it ever completes.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.None);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(
                () => _controller.IsBankReady,
                "AdaptiveMusicController.IsBankReady to become true after the superseding load completed"
            );

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.EqualTo(new[] { MusicState.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_WhenLoadBankAsyncThrows_LogsMusicStartFailedAndLeavesTheNextMatchWorking()
        {
            // GIVEN
            _fakeService.LoadBankAsyncExceptionToThrow = new InvalidOperationException("Simulated bank load failure.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MusicStartFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;
            _fakeService.LoadBankAsyncExceptionToThrow = null;
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.None);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(
                () => _controller.IsBankReady,
                "AdaptiveMusicController.IsBankReady to become true after the next match's load completed"
            );

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.EqualTo(new[] { MusicState.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchClockTicked_CrossingFromEarlyIntoMidBandAfterBankReady_WritesMatchIntensity()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            _fakeService.ParameterWrites.Clear();

            // WHEN
            MatchEvents.RaiseMatchClockTicked((int)FirstMidBandRemainingSeconds);

            // THEN
            Assert.That(_fakeService.ParameterWrites, Has.Count.EqualTo(1));
            Assert.That(_fakeService.ParameterWrites[0].Parameter, Is.EqualTo(AudioParameter.MatchIntensity));
            Assert.That(_fakeService.ParameterWrites[0].Value, Is.EqualTo(MidIntensity).Within(Tolerance));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchClockTicked_StayingInTheSameBandAfterBankReady_WritesNoAdditionalParameter()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            MatchEvents.RaiseMatchClockTicked((int)FirstMidBandRemainingSeconds);
            _fakeService.ParameterWrites.Clear();

            // WHEN
            MatchEvents.RaiseMatchClockTicked((int)StayingInMidBandRemainingSeconds);

            // THEN
            Assert.That(_fakeService.ParameterWrites, Is.Empty);
        }

        [UnityTest]
        [Category("Allocation")]
        [Timeout(5000)]
        public IEnumerator MatchClockTicked_AfterWarmup_AllocatesNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            static void code()
            {
                for (int i = 0; i < AllocationWarmupIterations; i++)
                {
                    MatchEvents.RaiseMatchClockTicked((i % 2 == 0) ? (int)EarlyBandRemainingSeconds : (int)LateBandRemainingSeconds);
                }
            }

            code();

            // WHEN / THEN
            Assert.That(code, new AllocatesNothingConstraint());
        }

        [Test]
        public void ScoreChanged_WhileTheBankIsStillLoading_WritesNoParameters()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseScoreChanged(LocalPlayerId, 3);

            // THEN
            Assert.That(_fakeService.ParameterWrites, Is.Empty);
        }

        [UnityTest]
        [Category("Allocation")]
        [Timeout(5000)]
        public IEnumerator ScoreChanged_AfterWarmup_AllocatesNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");

            static void code()
            {
                for (int i = 0; i < AllocationWarmupIterations; i++)
                {
                    MatchEvents.RaiseScoreChanged(LocalPlayerId, i % 2);
                    MatchEvents.RaiseScoreChanged(OpponentPlayerId, (i + 1) % 2);
                }
            }

            code();

            // WHEN / THEN
            Assert.That(code, new AllocatesNothingConstraint());
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchEnded_AfterBankReady_StopsMusicThenPlaysExactlyOneStinger()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            _fakeService.CallLog.Clear();

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            Assert.That(_fakeService.CallLog, Is.EqualTo(new[] { AudioServiceCall.StopMusic, AudioServiceCall.PlayStinger }));
            Assert.That(_fakeService.PlayedStingers, Is.EqualTo(new[] { MatchStinger.Victory }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchEnded_AfterBankReadyWithOpponentWinning_PlaysExactlyOneDefeatStinger()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            _fakeService.CallLog.Clear();

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(OpponentPlayerId, MatchEndReason.TimeLimit));

            // THEN
            Assert.That(_fakeService.PlayedStingers, Is.EqualTo(new[] { MatchStinger.Defeat }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchEnded_AfterBankReadyWithNoWinner_PlaysExactlyOneDrawStinger()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            _fakeService.CallLog.Clear();

            // WHEN
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // THEN
            Assert.That(_fakeService.PlayedStingers, Is.EqualTo(new[] { MatchStinger.Draw }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchEnded_AfterBankReadyWithNoLocalSeat_PlaysNoStinger()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfigWithoutLocalSeat());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            _fakeService.CallLog.Clear();

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            Assert.That(_fakeService.PlayedStingers, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchEnded_BeforeTheBankFinishesLoading_NeverPlaysMusicEvenAfterTheLoadLaterCompletes()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));
            _fakeService.CompleteBankLoad();

            for (int frame = 0; frame < MaxFramesToWait; frame++)
            {
                yield return null;
            }

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchStarted_ForARematchAfterTheFirstMatchEnded_PlaysMusicExactlyOncePerMatch()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(
                () => _controller.IsBankReady,
                "AdaptiveMusicController.IsBankReady to become true after the first bank load completed"
            );
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Ended);
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(
                () => _fakeService.PlayedMusicStates.Count >= 2,
                "AdaptiveMusicController to have played music for the rematch after its bank load completed"
            );

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.EqualTo(new[] { MusicState.Match, MusicState.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchStarted_ForARematchWhoseBankIsAlreadyLoaded_PlaysMusicExactlyOnce()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(
                () => _controller.IsBankReady,
                "AdaptiveMusicController.IsBankReady to become true after the first bank load completed"
            );
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Ended);
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));
            _fakeService.CompletesLoadBankAsyncSynchronously = true;

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            yield return WaitUntilAsync(
                () => _fakeService.PlayedMusicStates.Count >= 2,
                "AdaptiveMusicController to have played music for the rematch whose bank load completed synchronously"
            );

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.EqualTo(new[] { MusicState.Match, MusicState.Match }));
        }

        [Test]
        public void EventsRaised_AfterTheComponentIsDisabled_ProduceNoCalls()
        {
            // GIVEN
            _controllerGO.SetActive(false);
            _fakeService.CallLog.Clear();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            MatchEvents.RaiseScoreChanged(LocalPlayerId, 3);
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            Assert.That(_fakeService.CallLog, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator EventsRaised_ComponentDisabledWhileMusicIsPlaying_StopsMusicWithFadeOut()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");
            _fakeService.CallLog.Clear();

            // WHEN
            _controllerGO.SetActive(false);

            // THEN
            Assert.That(_fakeService.StopMusicFadeOutFlags, Is.EqualTo(new[] { true }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator EventsRaised_ComponentDisabledWhileMusicIsPlaying_ResetsTheModel()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            MatchEvents.RaiseMatchStarted(BuildConfig());
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after the bank load completed");

            // WHEN
            _controllerGO.SetActive(false);

            // THEN
            Assert.That(_controller.Model.MusicState, Is.EqualTo(MusicState.None));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator EventsRaised_ComponentDisabledWhileTheBankLoadIsPendingThenReenabled_PlaysMusicExactlyOnceAfterTheNextLoadCompletes()
        {
            // GIVEN — the disable supersedes the pending load; a None raised while disabled reaches nothing, exactly
            // like an abandoned start the orchestrator itself never observes while this component is unsubscribed.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _controllerGO.SetActive(false);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.None);

            // WHEN
            _controllerGO.SetActive(true);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsBankReady, "AdaptiveMusicController.IsBankReady to become true after re-enabling and reloading");

            // THEN
            Assert.That(_fakeService.PlayedMusicStates, Is.EqualTo(new[] { MusicState.Match }));
        }

        [Test]
        public void Construct_NullAudioService_LogsWarningAndLeavesTheComponentInert()
        {
            // GIVEN
            var inertControllerGO = new GameObject(nameof(AdaptiveMusicController) + "_NullService");
            AdaptiveMusicController inertController = inertControllerGO.AddComponent<AdaptiveMusicController>();
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MusicServiceMissing);

            try
            {
                // WHEN
                inertController.Construct(null, null);

                // THEN
                Assert.That(inertController.Model, Is.Null);
            }
            finally
            {
                Object.Destroy(inertControllerGO);
            }
        }

        [Test]
        public void Construct_NullAudioConfig_LogsWarningAndLeavesTheComponentInert()
        {
            // GIVEN
            var inertControllerGO = new GameObject(nameof(AdaptiveMusicController) + "_NullConfig");
            AdaptiveMusicController inertController = inertControllerGO.AddComponent<AdaptiveMusicController>();
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MusicConfigMissing);

            try
            {
                // WHEN
                inertController.Construct(new FakeAudioService(), null);

                // THEN
                Assert.That(inertController.Model, Is.Null);
            }
            finally
            {
                Object.Destroy(inertControllerGO);
            }
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

        private static IEnumerator WaitUntilAsync(Func<bool> condition, string expectationMessage)
        {
            int framesWaited = 0;

            while (!condition())
            {
                Assert.That(framesWaited, Is.LessThan(MaxFramesToWait), $"Expected: {expectationMessage}.");
                framesWaited++;
                yield return null;
            }
        }
    }
}
