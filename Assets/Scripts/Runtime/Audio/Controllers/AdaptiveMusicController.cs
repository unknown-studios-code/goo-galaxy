using System;
using GooGalaxy.Runtime.Audio.Data;
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
    /// Drives the adaptive match music from <see cref="MatchEvents" />: starts the match track as the board loads, shapes it
    /// through the match-intensity and territory-balance parameters as play goes on, and ends it with a stinger for the
    /// local player's result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every decision lives in an <see cref="AdaptiveMusicModel" />; this component only feeds it facts and writes the
    /// outputs it reports as moved to an <see cref="IAudioService" />, so the audio engine sees one call per real change
    /// rather than one per event. Local only: <see cref="MatchEvents" /> replicates nothing, and each device plays its
    /// own music from its own side of the board.
    /// </para>
    /// <para>
    /// <b>The clock and score handlers allocate nothing after warm-up.</b> The clock ticks once a second and a score moves
    /// on every landing, so they cost a few comparisons and, on a real change, one cached parameter write. The phase
    /// handler allocates only when it starts the music — the bank load, once per match, from Loading or from a Countdown
    /// whose Loading was not observed.
    /// </para>
    /// <para>
    /// <b>Parameters wait for the bank</b> (see <see cref="IAudioService.SetParameter" />): changes that arrive while it
    /// loads are held in the model and written all at once when the music starts.
    /// </para>
    /// <para>
    /// A Controller, not a Presenter: it drives a system and binds to no view.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class AdaptiveMusicController : MonoBehaviour
    {
        private IAudioService _audioService;
        private AdaptiveMusicModel _model;
        private int _musicRequest;
        private bool _isBankReady;

        internal AdaptiveMusicModel Model => _model;

        internal bool IsBankReady => _isBankReady;

        private bool IsConfigured => _model != null;

        /// <remarks>
        /// Copies the authored bands once, so an Inspector edit to the config changes the next scene rather than the running
        /// match. A null service or config leaves the component inert, after one warning, rather than failing a match.
        /// </remarks>
        [Inject]
        public void Construct(IAudioService audioService, AudioConfigSO config)
        {
            if (audioService == null)
            {
                Debug.LogWarning(AudioLogMessages.MusicServiceMissing, this);
                return;
            }

            if (config == null)
            {
                Debug.LogWarning(AudioLogMessages.MusicConfigMissing, this);
                return;
            }

            _audioService = audioService;
            _model = new AdaptiveMusicModel(config.Bands);
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.MatchClockTicked += HandleMatchClockTicked;
            MatchEvents.ScoreChanged += HandleScoreChanged;
            MatchEvents.MatchEnded += HandleMatchEnded;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
            MatchEvents.MatchClockTicked -= HandleMatchClockTicked;
            MatchEvents.ScoreChanged -= HandleScoreChanged;
            MatchEvents.MatchEnded -= HandleMatchEnded;

            if (!IsConfigured)
            {
                return;
            }

            // A scene can switch its match root off mid-countdown and on again later (MatchController supports exactly
            // that), which unsubscribes this component before MatchEvents ever reaches the None phase the abandoned
            // start publishes. Left alone, the model would keep believing a track is playing, and the engine would keep
            // playing it into whatever screen comes next — so both are put back to their fresh-match state here instead.
            _musicRequest++;
            _isBankReady = false;
            _audioService.StopMusic(true);
            _model.Reset();
        }

        private void WriteChanges(MusicChange changes)
        {
            if ((changes & MusicChange.Music) != 0)
            {
                ApplyMusicState();
            }

            if (!_isBankReady)
            {
                return;
            }

            if ((changes & MusicChange.Intensity) != 0)
            {
                _audioService.SetParameter(AudioParameter.MatchIntensity, _model.Intensity);
            }

            if ((changes & MusicChange.Balance) != 0)
            {
                _audioService.SetParameter(AudioParameter.TerritoryBalance, _model.Balance);
            }
        }

        private void ApplyMusicState()
        {
            MusicState state = _model.MusicState;

            if (state == MusicState.None)
            {
                // Supersedes a load still in flight, so a match that ends while its bank loads never starts its music.
                _musicRequest++;
                _audioService.StopMusic(true);
                return;
            }

            _ = StartMusicAsync(state);
        }

        private async Awaitable StartMusicAsync(MusicState state)
        {
            int request = ++_musicRequest;

            try
            {
                await _audioService.LoadBankAsync(AudioBank.Match, destroyCancellationToken);

                if ((this == null) || !isActiveAndEnabled)
                {
                    return;
                }

                if ((request != _musicRequest) || (_model.MusicState != state))
                {
                    return;
                }

                _isBankReady = true;
                _audioService.PlayMusic(state);
                _audioService.SetParameter(AudioParameter.MatchIntensity, _model.Intensity);
                _audioService.SetParameter(AudioParameter.TerritoryBalance, _model.Balance);
            }
            catch (OperationCanceledException)
            {
                // Expected on scene teardown or a request this instance itself superseded; nothing to clean up.
            }
            catch (Exception exception)
            {
                // Dispatch boundary, the same one MatchController's countdown catches at: ApplyMusicState discards this
                // Awaitable (`_ = StartMusicAsync(...)`), so an escaping exception here would be unobserved. Logged as a
                // warning, not an error, because audio must never fail a match or the PlayMode suite (see the class
                // remarks on AudioLogMessages) — the controller is left inert for this request rather than retried.
                Debug.LogWarning(AudioLogMessages.MusicStartFailed, this);
                Debug.LogException(exception, this);
            }
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            if (!IsConfigured)
            {
                return;
            }

            WriteChanges(_model.ApplyMatchStarted(in config));
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            if (!IsConfigured)
            {
                return;
            }

            WriteChanges(_model.ApplyPhase(phase));
        }

        private void HandleMatchClockTicked(int remainingSeconds)
        {
            if (!IsConfigured)
            {
                return;
            }

            WriteChanges(_model.ApplyClock(remainingSeconds));
        }

        private void HandleScoreChanged(int playerId, int unitCount)
        {
            if (!IsConfigured)
            {
                return;
            }

            WriteChanges(_model.ApplyScore(playerId, unitCount));
        }

        // The track is stopped with its authored fade-out before the stinger starts, so the stinger plays over the tail
        // rather than the full mix; both come from the match bank.
        private void HandleMatchEnded(MatchOutcome outcome)
        {
            if (!IsConfigured)
            {
                return;
            }

            MusicChange changes = _model.ApplyMatchEnded(in outcome, out MatchStinger stinger);

            WriteChanges(changes);

            if ((changes & MusicChange.Stinger) != 0)
            {
                _audioService.PlayStinger(stinger);
            }
        }
    }
}
