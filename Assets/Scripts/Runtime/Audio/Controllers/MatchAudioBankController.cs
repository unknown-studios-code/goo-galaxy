using System;
using GooGalaxy.Runtime.Audio.Interfaces;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Audio.Controllers
{
    /// <summary>
    /// Owns the match bank: loads it when a match starts loading, tells every listener once it is ready, and unloads it when
    /// the scene goes away. Music and sound effects only ask it, through <see cref="IAudioBankStatus" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One load per scene, not per match.</b> Because <see cref="IAudioService.UnloadBank" /> is not reference-counted, this
    /// is the bank's only owner: it asks once, keeps the bank for the rest of the scene, and unloads it exactly once, in
    /// <c>OnDestroy</c>. A rematch re-entering
    /// <see cref="MatchPhase.Loading" /> finds the bank already loaded, or already loading, and asks for nothing.
    /// </para>
    /// <para>
    /// <see cref="MatchPhase.Countdown" /> also starts the load when nothing has asked yet, for a match whose loading this
    /// component did not observe — the same fallback <see cref="AdaptiveMusicModel" /> takes for its track.
    /// </para>
    /// <para>
    /// <b>A load that throws is retried by the next match.</b> The fault is logged once and the bank goes back to unrequested,
    /// so the next <see cref="MatchPhase.Loading" /> asks again. A load cancelled by destruction is not a fault: the bank is
    /// unloaded by <c>OnDestroy</c> regardless of how far the load got.
    /// </para>
    /// <para>
    /// Local only, like everything that listens to <see cref="MatchEvents" />.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MatchAudioBankController : MonoBehaviour, IAudioBankStatus
    {
        private IAudioService _audioService;
        private BankLoadState _loadState;
        private bool _hasRequested;

        /// <inheritdoc />
        /// <remarks>Raised at most once per scene for <see cref="AudioBank.Match" />, the only bank this component owns.</remarks>
        public event Action<AudioBank> BankLoaded;

        internal bool IsLoading => _loadState == BankLoadState.Loading;

        private bool IsConfigured => _audioService != null;

        [Inject]
        public void Construct(IAudioService audioService)
        {
            if (audioService == null)
            {
                Debug.LogWarning(AudioLogMessages.BankServiceMissing, this);
                return;
            }

            _audioService = audioService;
        }

        protected void OnEnable()
        {
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
        }

        protected void OnDestroy()
        {
            if (!IsConfigured || !_hasRequested)
            {
                return;
            }

            _loadState = BankLoadState.Unrequested;
            _audioService.UnloadBank(AudioBank.Match);
        }

        public bool IsLoaded(AudioBank bank)
        {
            return (bank == AudioBank.Match) && (_loadState == BankLoadState.Loaded);
        }

        private async Awaitable LoadMatchBankAsync()
        {
            _loadState = BankLoadState.Loading;
            _hasRequested = true;

            try
            {
                await _audioService.LoadBankAsync(AudioBank.Match, destroyCancellationToken);
            }
            catch (OperationCanceledException) when (destroyCancellationToken.IsCancellationRequested)
            {
                // Expected on scene teardown; OnDestroy unloads whatever the load managed to take.
                return;
            }
            catch (Exception exception)
            {
                // Dispatch boundary: HandleMatchPhaseChanged discards this Awaitable, so an escaping exception would go
                // unobserved. A warning, not an error, because audio must never fail a match or the PlayMode suite.
                _loadState = BankLoadState.Unrequested;
                Debug.LogWarning(AudioLogMessages.MatchBankLoadFailed, this);
                Debug.LogException(exception, this);
                return;
            }

            // Deliberately no isActiveAndEnabled check: the bank is loaded in the service whether or not this component is
            // enabled, and IsLoaded must say so, or a listener re-enabled later would wait for an event that never comes.
            if (this == null)
            {
                return;
            }

            _loadState = BankLoadState.Loaded;
            OnBankLoaded(AudioBank.Match);
        }

        protected virtual void OnBankLoaded(AudioBank bank)
        {
            try
            {
                BankLoaded?.Invoke(bank);
            }
            catch (Exception exception)
            {
                // Dispatch boundary into arbitrary subscriber code, on a continuation nothing awaits: a throw here would be
                // unobserved, so it is logged with its stack instead.
                Debug.LogWarning(AudioLogMessages.BankLoadedSubscriberFailed, this);
                Debug.LogException(exception, this);
            }
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            if (!IsConfigured || (_loadState != BankLoadState.Unrequested))
            {
                return;
            }

            if (phase is not (MatchPhase.Loading or MatchPhase.Countdown))
            {
                return;
            }

            _ = LoadMatchBankAsync();
        }

        private enum BankLoadState
        {
            Unrequested = 0,
            Loading = 1,
            Loaded = 2,
        }
    }
}
