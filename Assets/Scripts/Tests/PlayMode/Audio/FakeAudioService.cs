using System;
using System.Collections.Generic;
using System.Threading;
using GooGalaxy.Runtime.Audio.Interfaces;
using GooGalaxy.Runtime.Audio.Models;
using UnityEngine;

namespace GooGalaxy.Tests.PlayMode.Audio
{
    internal sealed class FakeAudioService : IAudioService
    {
        private const int LogCapacity = 256;

        private readonly Dictionary<AudioChannel, float> _volumes = new();

        public List<AudioServiceCall> CallLog { get; } = new(LogCapacity);

        public List<AudioCue> PlayedCues { get; } = new(LogCapacity);

        public List<MusicState> PlayedMusicStates { get; } = new(LogCapacity);

        public List<bool> StopMusicFadeOutFlags { get; } = new(LogCapacity);

        public List<MatchStinger> PlayedStingers { get; } = new(LogCapacity);

        public List<(AudioParameter Parameter, float Value)> ParameterWrites { get; } = new(LogCapacity);

        public List<AudioBank> LoadBankRequests { get; } = new(LogCapacity);

        public List<AudioBank> UnloadBankRequests { get; } = new(LogCapacity);

        public bool IsBankLoadPending { get; private set; }

        /// <remarks>
        /// When true, <see cref="LoadBankAsync" /> completes on the calling frame instead of waiting for
        /// <see cref="CompleteBankLoad" />, the way a bank an earlier match already loaded behaves for a rematch.
        /// </remarks>
        public bool CompletesLoadBankAsyncSynchronously { get; set; }

        /// <remarks>Set before a call, and cleared by the test afterwards; makes the next <see cref="LoadBankAsync" /> fail instead of loading.</remarks>
        public Exception LoadBankAsyncExceptionToThrow { get; set; }

        public void PlayOneShot(AudioCue cue)
        {
            CallLog.Add(AudioServiceCall.PlayOneShot);
            PlayedCues.Add(cue);
        }

        public void PlayMusic(MusicState state)
        {
            CallLog.Add(AudioServiceCall.PlayMusic);
            PlayedMusicStates.Add(state);
        }

        public void StopMusic(bool allowFadeOut)
        {
            CallLog.Add(AudioServiceCall.StopMusic);
            StopMusicFadeOutFlags.Add(allowFadeOut);
        }

        public void PlayStinger(MatchStinger stinger)
        {
            CallLog.Add(AudioServiceCall.PlayStinger);
            PlayedStingers.Add(stinger);
        }

        public void SetParameter(AudioParameter parameter, float value)
        {
            CallLog.Add(AudioServiceCall.SetParameter);
            ParameterWrites.Add((parameter, value));
        }

        public void SetVolume(AudioChannel channel, float linearVolume)
        {
            _volumes[channel] = Mathf.Clamp01(linearVolume);
        }

        public float GetVolume(AudioChannel channel)
        {
            return _volumes.TryGetValue(channel, out float volume) ? volume : 1f;
        }

        /// <remarks>
        /// Stays pending until a test calls <see cref="CompleteBankLoad" />, so a fixture can hold a bank "loading"
        /// across several frames the way the real service does while FMOD streams sample data in — unless
        /// <see cref="CompletesLoadBankAsyncSynchronously" /> or <see cref="LoadBankAsyncExceptionToThrow" /> says otherwise.
        /// </remarks>
        public Awaitable LoadBankAsync(AudioBank bank, CancellationToken token)
        {
            CallLog.Add(AudioServiceCall.LoadBankAsync);
            LoadBankRequests.Add(bank);

            if (LoadBankAsyncExceptionToThrow != null)
            {
                return FaultedAsync(LoadBankAsyncExceptionToThrow);
            }

            if (CompletesLoadBankAsyncSynchronously)
            {
                return CompletedAsync();
            }

            IsBankLoadPending = true;

            return WaitForBankLoadAsync(token);
        }

        public void UnloadBank(AudioBank bank)
        {
            CallLog.Add(AudioServiceCall.UnloadBank);
            UnloadBankRequests.Add(bank);
        }

        /// <summary>Lets a pending <see cref="LoadBankAsync" /> complete on its next polled frame.</summary>
        public void CompleteBankLoad()
        {
            IsBankLoadPending = false;
        }

        private static Awaitable CompletedAsync()
        {
            var completionSource = new AwaitableCompletionSource();
            completionSource.SetResult();

            return completionSource.Awaitable;
        }

        private static Awaitable FaultedAsync(Exception exception)
        {
            var completionSource = new AwaitableCompletionSource();
            completionSource.SetException(exception);

            return completionSource.Awaitable;
        }

        private async Awaitable WaitForBankLoadAsync(CancellationToken token)
        {
            while (IsBankLoadPending)
            {
                await Awaitable.NextFrameAsync(token);
            }
        }
    }
}
