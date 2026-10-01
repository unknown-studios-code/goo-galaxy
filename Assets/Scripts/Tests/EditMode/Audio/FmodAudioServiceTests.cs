using System;
using System.Threading;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Audio.Services;
using GooGalaxy.Runtime.Shared.Constants;
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
    }
}
