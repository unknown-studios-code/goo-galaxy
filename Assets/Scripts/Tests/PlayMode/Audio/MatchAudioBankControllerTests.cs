using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GooGalaxy.Runtime.Audio.Controllers;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.Audio
{
    [TestFixture]
    public class MatchAudioBankControllerTests
    {
        private const int MaxFramesToWait = 30;

        private GameObject _controllerGO;
        private MatchAudioBankController _controller;
        private FakeAudioService _fakeService;
        private List<AudioBank> _loadedBanks;

        [SetUp]
        public void SetUp()
        {
            MatchEvents.ResetEvents();

            _fakeService = new FakeAudioService();
            _loadedBanks = new List<AudioBank>();

            _controllerGO = new GameObject(nameof(MatchAudioBankController));
            _controllerGO.SetActive(false);
            _controller = _controllerGO.AddComponent<MatchAudioBankController>();
            _controller.Construct(_fakeService);
            _controller.BankLoaded += HandleBankLoaded;
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
        public void MatchPhaseChanged_Loading_RequestsTheMatchBank()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [Test]
        public void MatchPhaseChanged_Loading_ReportsLoadingButNotLoadedUntilTheServiceCompletes()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That((_controller.IsLoading, _controller.IsLoaded(AudioBank.Match)), Is.EqualTo((true, false)));
        }

        [Test]
        public void MatchPhaseChanged_CountdownWithoutPriorLoading_RequestsTheMatchBank()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [Test]
        public void MatchPhaseChanged_CountdownAfterLoading_RequestsNothingMore()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MatchPhaseChanged_LoadingAgainWhileTheBankIsStillLoading_RequestsNothingMore()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Has.Count.EqualTo(1));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_LoadingAgainOnceTheBankIsLoaded_RequestsNothingMore()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsLoaded(AudioBank.Match), "the bank to finish loading");

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public void MatchPhaseChanged_StandardPhaseWithoutPriorLoading_RequestsNothing()
        {
            // GIVEN

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_LoadBankAsyncThrows_LogsMatchBankLoadFailedAndLeavesTheBankNotLoaded()
        {
            // GIVEN
            _fakeService.LoadBankAsyncExceptionToThrow = new InvalidOperationException("Simulated bank load failure.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MatchBankLoadFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;

            // THEN
            Assert.That((_controller.IsLoading, _controller.IsLoaded(AudioBank.Match)), Is.EqualTo((false, false)));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_LoadBankAsyncCancelledWithoutADestroy_LogsMatchBankLoadFailedAndLeavesTheBankNotLoaded()
        {
            // GIVEN
            _fakeService.LoadBankAsyncExceptionToThrow = new OperationCanceledException("Simulated cancellation by someone other than destruction.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MatchBankLoadFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;

            // THEN
            Assert.That((_controller.IsLoading, _controller.IsLoaded(AudioBank.Match)), Is.EqualTo((false, false)));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_LoadingAfterAThrowingLoad_RetriesTheRequest()
        {
            // GIVEN
            _fakeService.LoadBankAsyncExceptionToThrow = new InvalidOperationException("Simulated bank load failure.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MatchBankLoadFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;
            _fakeService.LoadBankAsyncExceptionToThrow = null;

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Is.EqualTo(new[] { AudioBank.Match, AudioBank.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator MatchPhaseChanged_LoadingAfterACancellationWithoutADestroy_RetriesTheRequest()
        {
            // GIVEN
            _fakeService.LoadBankAsyncExceptionToThrow = new OperationCanceledException("Simulated cancellation by someone other than destruction.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MatchBankLoadFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;
            _fakeService.LoadBankAsyncExceptionToThrow = null;

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_fakeService.LoadBankRequests, Is.EqualTo(new[] { AudioBank.Match, AudioBank.Match }));
        }

        [Test]
        public void IsLoaded_BeforeAnyPhaseIsEntered_IsFalse()
        {
            // GIVEN

            // WHEN
            bool isLoaded = _controller.IsLoaded(AudioBank.Match);

            // THEN
            Assert.That(isLoaded, Is.False);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_AfterLoading_ReportsTheMatchBankLoaded()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsLoaded(AudioBank.Match), "the bank to finish loading");

            // THEN
            Assert.That(_controller.IsLoaded(AudioBank.Match), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_AfterLoading_RaisesBankLoadedOnceWithTheMatchBank()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsLoaded(AudioBank.Match), "the bank to finish loading");
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;

            // THEN
            Assert.That(_loadedBanks, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_AfterLoading_AnswersLoadedAlreadyInsideTheBankLoadedCallback()
        {
            // GIVEN
            bool wasLoadedInsideCallback = false;
            _controller.BankLoaded += _ => wasLoadedInsideCallback = _controller.IsLoaded(AudioBank.Match);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _loadedBanks.Count > 0, "BankLoaded to be raised");

            // THEN
            Assert.That(wasLoadedInsideCallback, Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_WhileTheComponentIsDisabled_StillReportsTheBankLoaded()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _controllerGO.SetActive(false);

            // WHEN
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsLoaded(AudioBank.Match), "the bank to finish loading while the component was disabled");

            // THEN
            Assert.That(_loadedBanks, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator BankLoadCompleting_WhenASubscriberThrows_LogsTheFaultAndStillReportsTheBankLoaded()
        {
            // GIVEN
            _controller.BankLoaded += _ => throw new InvalidOperationException("Simulated subscriber failure.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.BankLoadedSubscriberFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsLoaded(AudioBank.Match), "the bank to finish loading");

            // THEN
            Assert.That(_controller.IsLoaded(AudioBank.Match), Is.True);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator OnDestroy_AfterTheBankWasRequested_UnloadsTheMatchBankExactlyOnce()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            _fakeService.CompleteBankLoad();
            yield return WaitUntilAsync(() => _controller.IsLoaded(AudioBank.Match), "the bank to finish loading");

            // WHEN
            Object.Destroy(_controllerGO);
            yield return null;

            // THEN
            Assert.That(_fakeService.UnloadBankRequests, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator OnDestroy_WhenTheBankWasNeverRequested_UnloadsNothing()
        {
            // GIVEN

            // WHEN
            Object.Destroy(_controllerGO);
            yield return null;

            // THEN
            Assert.That(_fakeService.UnloadBankRequests, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator OnDestroy_WhileTheBankIsStillLoading_UnloadsTheMatchBank()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            Object.Destroy(_controllerGO);
            yield return null;
            yield return null;

            // THEN
            Assert.That(_fakeService.UnloadBankRequests, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator OnDestroy_WhileTheBankIsStillLoading_LogsNothing()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            Object.Destroy(_controllerGO);
            yield return null;
            yield return null;

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator OnDestroy_WhileTheBankIsStillLoading_NeverReportsTheBankLoaded()
        {
            // GIVEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // WHEN
            Object.Destroy(_controllerGO);
            yield return null;
            _fakeService.CompleteBankLoad();
            yield return null;

            // THEN
            Assert.That(_loadedBanks, Is.Empty);
        }

        [UnityTest]
        [Timeout(5000)]
        public IEnumerator OnDestroy_AfterAFaultedLoad_UnloadsTheMatchBank()
        {
            // GIVEN
            _fakeService.LoadBankAsyncExceptionToThrow = new InvalidOperationException("Simulated bank load failure.");
            LogAssert.Expect(LogType.Warning, AudioLogMessages.MatchBankLoadFailed);
            LogAssert.Expect(LogType.Exception, new Regex(".*"));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);
            yield return null;

            // WHEN
            Object.Destroy(_controllerGO);
            yield return null;

            // THEN
            Assert.That(_fakeService.UnloadBankRequests, Is.EqualTo(new[] { AudioBank.Match }));
        }

        [Test]
        public void Construct_NullAudioService_LogsWarningAndNeverRequestsTheBank()
        {
            // GIVEN
            var inertGO = new GameObject(nameof(MatchAudioBankController) + "_NullService");
            inertGO.SetActive(false);
            MatchAudioBankController inert = inertGO.AddComponent<MatchAudioBankController>();
            LogAssert.Expect(LogType.Warning, AudioLogMessages.BankServiceMissing);

            try
            {
                // WHEN
                inert.Construct(null);
                inertGO.SetActive(true);
                MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

                // THEN
                Assert.That(inert.IsLoading, Is.False);
            }
            finally
            {
                Object.Destroy(inertGO);
            }
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

        private void HandleBankLoaded(AudioBank bank)
        {
            _loadedBanks.Add(bank);
        }
    }
}
