using System;
using System.Text;
using System.Threading;
using FMODUnity;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Audio.Services;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Audio
{
    [TestFixture]
    public class FmodAudioServiceTests
    {
        private const float Tolerance = 0.0001f;
        private const string AcidCrawlerId = "acid_crawler";
        private const string CryoStasisId = "cryo_stasis";
        private const string FirstEventPath = "event:/test/first";
        private const string SecondEventPath = "event:/test/second";

        private AudioConfigSO _config;
        private FmodAudioService _service;

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();

            if (_config != null)
            {
                Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void PlayOneShot_NullConfig_LogsServiceConfigMissingAndDisablesTheEngine()
        {
            // GIVEN
            _service = new FmodAudioService(null);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceConfigMissing);

            // WHEN
            _service.PlayOneShot(AudioCue.ButtonPress);

            // THEN
            Assert.That(_service.IsEngineUnavailable, Is.True);
        }

        [Test]
        public void PlayOneShot_NullConfigCalledASecondTime_DoesNotLogTheWarningAgain()
        {
            // GIVEN
            _service = new FmodAudioService(null);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceConfigMissing);
            _service.PlayOneShot(AudioCue.ButtonPress);

            // WHEN
            _service.PlayOneShot(AudioCue.ButtonPress);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayOneShot_ConfigAssignedOutsidePlayMode_LogsServiceNotPlayingAndDisablesTheEngine()
        {
            // GIVEN
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceNotPlaying);

            // WHEN
            _service.PlayOneShot(AudioCue.ButtonPress);

            // THEN
            Assert.That(_service.IsEngineUnavailable, Is.True);
        }

        [Test]
        public void PlayOneShot_CalledAfterDispose_DoesNotAttemptToStartTheEngine()
        {
            // GIVEN — a fresh service that has never touched the engine, so an attempt would log ServiceNotPlaying
            // if the disposed check did not run first.
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);
            _service.Dispose();

            // WHEN
            _service.PlayOneShot(AudioCue.ButtonPress);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TryGetCardCueEvent_MappedCard_ResolvesItsEvent()
        {
            // GIVEN
            _service = BuildServiceWithCardCues((AcidCrawlerId, FirstEventPath));

            // WHEN
            bool wasFound = _service.TryGetCardCueEvent(new CardId(AcidCrawlerId), out EventReference reference);

            // THEN
            Assert.That(wasFound, Is.True);
            Assert.That(reference.Path, Is.EqualTo(FirstEventPath));
        }

        [Test]
        public void TryGetCardCueEvent_CardListedTwice_FirstRowWins()
        {
            // GIVEN
            _service = BuildServiceWithCardCues((AcidCrawlerId, FirstEventPath), (AcidCrawlerId, SecondEventPath));

            // WHEN
            _service.TryGetCardCueEvent(new CardId(AcidCrawlerId), out EventReference reference);

            // THEN
            Assert.That(reference.Path, Is.EqualTo(FirstEventPath));
        }

        [Test]
        public void TryGetCardCueEvent_RowAfterABlankIdRow_StillResolves()
        {
            // GIVEN
            _service = BuildServiceWithCardCues(("", FirstEventPath), (CryoStasisId, SecondEventPath));

            // WHEN
            bool wasFound = _service.TryGetCardCueEvent(new CardId(CryoStasisId), out EventReference reference);

            // THEN
            Assert.That(wasFound, Is.True);
            Assert.That(reference.Path, Is.EqualTo(SecondEventPath));
        }

        [Test]
        public void TryGetCardCueEvent_UnmappedCard_ReturnsFalse()
        {
            // GIVEN
            _service = BuildServiceWithCardCues((AcidCrawlerId, FirstEventPath));

            // WHEN
            bool wasFound = _service.TryGetCardCueEvent(new CardId(CryoStasisId), out EventReference _);

            // THEN
            Assert.That(wasFound, Is.False);
        }

        [Test]
        public void TryGetCardCueEvent_NoRowsAuthored_ReturnsFalse()
        {
            // GIVEN
            _service = BuildServiceWithCardCues();

            // WHEN
            bool wasFound = _service.TryGetCardCueEvent(new CardId(AcidCrawlerId), out EventReference _);

            // THEN
            Assert.That(wasFound, Is.False);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void TryGetCardCueEvent_RowWithABlankId_IsSkipped(string blankId)
        {
            // GIVEN
            _service = BuildServiceWithCardCues((blankId, FirstEventPath));

            // WHEN
            bool wasFound = _service.TryGetCardCueEvent(new CardId(blankId), out EventReference _);

            // THEN
            Assert.That(wasFound, Is.False);
        }

        [Test]
        public void TryGetCardCueEvent_NullConfig_ReturnsFalse()
        {
            // GIVEN
            _service = new FmodAudioService(null);

            // WHEN
            bool wasFound = _service.TryGetCardCueEvent(new CardId(AcidCrawlerId), out EventReference _);

            // THEN
            Assert.That(wasFound, Is.False);
        }

        [Test]
        public void PlayCardCue_MappedCardOutsidePlayMode_LogsServiceNotPlayingAndDisablesTheEngine()
        {
            // GIVEN
            _service = BuildServiceWithCardCues((AcidCrawlerId, FirstEventPath));
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceNotPlaying);

            // WHEN
            _service.PlayCardCue(new CardId(AcidCrawlerId));

            // THEN
            Assert.That(_service.IsEngineUnavailable, Is.True);
        }

        [Test]
        public void PlayCardCue_UnmappedCardOutsidePlayMode_LogsNoCardCueWarning()
        {
            // GIVEN
            _service = BuildServiceWithCardCues((AcidCrawlerId, FirstEventPath));
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceNotPlaying);

            // WHEN
            _service.PlayCardCue(new CardId(CryoStasisId));

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayCardCue_NullConfig_LogsServiceConfigMissingAndDisablesTheEngine()
        {
            // GIVEN
            _service = new FmodAudioService(null);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceConfigMissing);

            // WHEN
            _service.PlayCardCue(new CardId(AcidCrawlerId));

            // THEN
            Assert.That(_service.IsEngineUnavailable, Is.True);
        }

        [Test]
        public void PlayCardCue_CalledAfterDispose_LogsNothing()
        {
            // GIVEN
            _service = BuildServiceWithCardCues((AcidCrawlerId, FirstEventPath));
            _service.Dispose();

            // WHEN
            _service.PlayCardCue(new CardId(AcidCrawlerId));

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void PlayMusic_EngineUnavailableOutsidePlayMode_LeavesMusicStateAtNone()
        {
            // GIVEN
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceNotPlaying);

            // WHEN
            _service.PlayMusic(MusicState.Match);

            // THEN
            Assert.That(_service.MusicState, Is.EqualTo(MusicState.None));
        }

        [Test]
        public void PlayStinger_None_LogsNothing()
        {
            // GIVEN
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);

            // WHEN
            _service.PlayStinger(MatchStinger.None);

            // THEN
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void GetVolume_BeforeAnyVolumeSet_ReturnsTheDefaultOfOne()
        {
            // GIVEN
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceNotPlaying);

            // WHEN
            float volume = _service.GetVolume(AudioChannel.Music);

            // THEN
            Assert.That(volume, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void SetVolume_OutOfRangeValue_ClampsBeforeItIsReadBack()
        {
            // GIVEN
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);
            LogAssert.Expect(LogType.Warning, AudioLogMessages.ServiceNotPlaying);

            // WHEN
            _service.SetVolume(AudioChannel.Music, 1.5f);

            // THEN
            Assert.That(_service.GetVolume(AudioChannel.Music), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void LoadBankAsync_TokenAlreadyCancelled_ThrowsOperationCanceledException()
        {
            // GIVEN
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            _service = new FmodAudioService(_config);
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            // WHEN
            Awaitable awaitable = _service.LoadBankAsync(AudioBank.Match, cancellationSource.Token);

            // THEN
            Assert.Throws<OperationCanceledException>(() => awaitable.GetAwaiter().GetResult());
        }

        [Test]
        public void Dispose_CalledASecondTime_DoesNotThrow()
        {
            // GIVEN
            _service = new FmodAudioService(null);
            _service.Dispose();

            // WHEN / THEN
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        private FmodAudioService BuildServiceWithCardCues(params (string CardId, string EventPath)[] rows)
        {
            var json = new StringBuilder("{\"_cardCues\":[");

            for (int i = 0; i < rows.Length; i++)
            {
                json.Append(i == 0 ? "" : ",");
                json.Append("{\"CardId\":\"").Append(rows[i].CardId).Append("\",\"Event\":{\"Path\":\"").Append(rows[i].EventPath).Append("\"}}");
            }

            json.Append("]}");
            _config = ScriptableObject.CreateInstance<AudioConfigSO>();
            JsonUtility.FromJsonOverwrite(json.ToString(), _config);

            return new FmodAudioService(_config);
        }
    }
}
