using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Interfaces;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
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
    /// on every landing, so they cost a few comparisons and, on a real change, one cached parameter write.
    /// </para>
    /// <para>
    /// <b>It never loads the bank; it waits for it.</b> The match bank belongs to whoever implements
    /// <see cref="IAudioBankStatus" /> — <see cref="MatchAudioBankController" /> in a match scene — because unloading is not
    /// reference-counted, and two owners would release it from under each other. A track the model asks for while the bank
    /// is still loading is held as pending and started by <see cref="IAudioBankStatus.BankLoaded" />; a stop, a disable or a
    /// match ending before then drops the pending start, so a superseded request never plays.
    /// </para>
    /// <para>
    /// <b>Parameters wait for the music</b> (see <see cref="IAudioService.SetParameter" />): changes that arrive while the bank
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
        private IAudioBankStatus _bankStatus;
        private AdaptiveMusicModel _model;
        private bool _isMusicPending;
        private bool _hasMusicStarted;

        internal AdaptiveMusicModel Model => _model;

        internal bool HasMusicStarted => _hasMusicStarted;

        private bool IsConfigured => _model != null;

        /// <remarks>
        /// Copies the authored bands once, so an Inspector edit to the config changes the next scene rather than the running
        /// match. Must run before <c>OnEnable</c>, which is where the bank status is subscribed to — the container injects a
        /// registered scene component while its scope wakes, ahead of the component's own callbacks.
        /// </remarks>
        [Inject]
        public void Construct(IAudioService audioService, AudioConfigSO config, IAudioBankStatus bankStatus)
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

            if (UnityReference.IsUnavailable(bankStatus))
            {
                Debug.LogWarning(AudioLogMessages.MusicBankStatusMissing, this);
                return;
            }

            _audioService = audioService;
            _bankStatus = bankStatus;
            _model = new AdaptiveMusicModel(config.Bands);
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.MatchClockTicked += HandleMatchClockTicked;
            MatchEvents.ScoreChanged += HandleScoreChanged;
            MatchEvents.MatchEnded += HandleMatchEnded;

            if (IsConfigured)
            {
                _bankStatus.BankLoaded += HandleBankLoaded;
            }
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

            // No Unity null check on purpose: the event lives on the managed object, so unsubscribing stays correct even when
            // the bank owner was destroyed first during scene teardown.
            _bankStatus.BankLoaded -= HandleBankLoaded;

            // A scene can switch its match root off mid-countdown and on again later (MatchController supports exactly
            // that), which unsubscribes this component before MatchEvents ever reaches the None phase the abandoned
            // start publishes. Left alone, the model would keep believing a track is playing, and the engine would keep
            // playing it into whatever screen comes next — so both are put back to their fresh-match state here instead.
            _isMusicPending = false;
            _hasMusicStarted = false;
            _audioService.StopMusic(true);
            _model.Reset();
        }

        private void WriteChanges(MusicChange changes)
        {
            if ((changes & MusicChange.Music) != 0)
            {
                ApplyMusicState();
            }

            if (!_hasMusicStarted)
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
                // Drops a start still waiting on the bank, so a match that ends while its bank loads never starts its music.
                _isMusicPending = false;
                _audioService.StopMusic(true);
                return;
            }

            if (UnityReference.IsUnavailable(_bankStatus) || !_bankStatus.IsLoaded(AudioBank.Match))
            {
                _isMusicPending = true;
                return;
            }

            StartMusic(state);
        }

        private void StartMusic(MusicState state)
        {
            _isMusicPending = false;
            _hasMusicStarted = true;
            _audioService.PlayMusic(state);
            _audioService.SetParameter(AudioParameter.MatchIntensity, _model.Intensity);
            _audioService.SetParameter(AudioParameter.TerritoryBalance, _model.Balance);
        }

        private void HandleBankLoaded(AudioBank bank)
        {
            if (!IsConfigured || (bank != AudioBank.Match) || !_isMusicPending)
            {
                return;
            }

            _isMusicPending = false;

            if (_model.MusicState == MusicState.None)
            {
                return;
            }

            StartMusic(_model.MusicState);
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
