using System;
using GooGalaxy.Runtime.Audio.Services;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;

namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>
    /// The engine-free state behind the adaptive match music: which track should play, how intense it is, and which
    /// side of the board it leans towards. It is fed match facts and answers with what moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <c>Apply*</c> method returns the <see cref="MusicChange" /> outputs it moved and nothing else, so a caller
    /// writes to the audio engine only on change. Intensity is compared exactly, which is safe because every value it can
    /// take is copied from the authored <see cref="IntensityBands" /> rather than computed. Balance is computed, so it is
    /// held as a whole number of <see cref="BalanceStep" />s instead, which compares exactly.
    /// </para>
    /// <para>
    /// <b>One instance serves every match of its owner's lifetime.</b> <see cref="ApplyMatchStarted" /> resets all
    /// per-match state, because a rematch reuses the instance. It deliberately keeps the phase: the orchestrator enters
    /// <see cref="MatchPhase.Loading" /> before it announces the match, so the phase has already moved on by the time the
    /// match starts.
    /// </para>
    /// <para>
    /// Allocation-free on every path, so the clock and score handlers that feed it can run on every tick.
    /// </para>
    /// </remarks>
    public class AdaptiveMusicModel
    {
        /// <summary>The granularity <see cref="Balance" /> is quantised to, so a one-unit swing on a crowded board is not a parameter write.</summary>
        public const float BalanceStep = 1f / BalanceStepsPerUnit;

        private const int BalanceStepsPerUnit = 20;

        private readonly IntensityBands _bands;

        private MatchPhase _phase;
        private MusicState _musicState;
        private float _intensity;
        private float _standardDurationSeconds;
        private float _standardRemainingSeconds;
        private int _localPlayerId;
        private int _opponentPlayerId;
        private int _localUnitCount;
        private int _opponentUnitCount;
        private int _balanceSteps;
        private bool _hasMatchStarted;
        private bool _hasLocalSeat;
        private bool _hasMatchEnded;

        /// <summary>Creates a model with no match, no music, idle intensity and an even balance.</summary>
        /// <param name="bands">The thresholds every intensity is read from, captured once for this instance's lifetime.</param>
        public AdaptiveMusicModel(in IntensityBands bands)
        {
            _bands = bands;
            _intensity = bands.Idle;
        }

        public MatchPhase Phase => _phase;

        public MusicState MusicState => _musicState;

        /// <summary>The music intensity, in the range the bands were authored in.</summary>
        public float Intensity => _intensity;

        /// <summary>
        /// Who holds more of the board, from -1 (the opponent holds all of it) to 1 (the local player does), quantised to
        /// <see cref="BalanceStep" />. Zero when neither side holds anything or the local seat could not be resolved.
        /// </summary>
        public float Balance => _balanceSteps / (float)BalanceStepsPerUnit;

        public bool HasMatchStarted => _hasMatchStarted;

        public bool HasMatchEnded => _hasMatchEnded;

        /// <summary>
        /// Whether the current match names a seat driven by the person holding this device. Without one there is no side
        /// to lean towards and no winner to congratulate, so balance stays even and no stinger is chosen.
        /// </summary>
        public bool HasLocalSeat => _hasLocalSeat;

        /// <summary>Starts tracking a newly announced match, discarding everything the previous one left behind.</summary>
        /// <remarks>
        /// Resolves the local seat through <see cref="LocalSeatResolver" />, zeroes both scores, rearms the stinger and
        /// resets the Standard clock to the configured duration. The phase and the music state are kept.
        /// </remarks>
        /// <param name="config">The configuration the match was announced with.</param>
        /// <returns>The outputs the reset moved: <see cref="MusicChange.Balance" /> and <see cref="MusicChange.Intensity" /> at most.</returns>
        public MusicChange ApplyMatchStarted(in MatchConfiguration config)
        {
            _hasMatchStarted = true;
            _hasMatchEnded = false;
            _standardDurationSeconds = config.StandardDurationSeconds;
            _standardRemainingSeconds = config.StandardDurationSeconds;
            _hasLocalSeat = LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out PlayerSlot away);
            _localPlayerId = home.Id;
            _opponentPlayerId = away.Id;
            _localUnitCount = 0;
            _opponentUnitCount = 0;

            MusicChange changes = ChangeBalanceSteps(0);

            return changes | RefreshIntensity();
        }

        /// <summary>Moves the model into a new phase.</summary>
        /// <remarks>
        /// Entering <see cref="MatchPhase.Standard" /> rewinds the Standard clock to its full duration, so the intensity is
        /// right before the phase's opening tick arrives. <see cref="MatchPhase.Loading" /> and
        /// <see cref="MatchPhase.Countdown" /> start the match track when nothing is playing — the second covers a match
        /// whose loading was never observed — and <see cref="MatchPhase.None" />, which an abandoned start publishes, stops
        /// it. Phases the resolver holds keep the current intensity.
        /// </remarks>
        /// <param name="phase">The phase just entered.</param>
        /// <returns>The outputs the transition moved: <see cref="MusicChange.Intensity" /> and <see cref="MusicChange.Music" /> at most.</returns>
        public MusicChange ApplyPhase(MatchPhase phase)
        {
            if (phase == _phase)
            {
                return MusicChange.None;
            }

            _phase = phase;

            if (phase == MatchPhase.Standard)
            {
                _standardRemainingSeconds = _standardDurationSeconds;
            }

            MusicChange changes = RefreshIntensity();

            return changes | ChangeMusicStateForPhase(phase);
        }

        /// <summary>Applies a clock tick.</summary>
        /// <remarks>
        /// Only a Standard tick is read: the countdown and overtime clocks do not move the intensity. A tick that arrives
        /// before any match has been announced is ignored.
        /// </remarks>
        /// <param name="remainingSeconds">Whole seconds left in the current phase.</param>
        /// <returns><see cref="MusicChange.Intensity" /> when the tick crossed into another band; otherwise none.</returns>
        public MusicChange ApplyClock(int remainingSeconds)
        {
            if (!_hasMatchStarted || (_phase != MatchPhase.Standard))
            {
                return MusicChange.None;
            }

            _standardRemainingSeconds = remainingSeconds;

            return RefreshIntensity();
        }

        /// <summary>Applies a player's new unit count.</summary>
        /// <remarks>
        /// Counts for a player who is neither seat, and counts that arrive before any match has been announced, are ignored.
        /// A negative count is read as zero.
        /// </remarks>
        /// <param name="playerId">The player whose count changed.</param>
        /// <param name="unitCount">The number of live units that player now holds.</param>
        /// <returns><see cref="MusicChange.Balance" /> when the quantised balance moved; otherwise none.</returns>
        public MusicChange ApplyScore(int playerId, int unitCount)
        {
            if (!_hasMatchStarted)
            {
                return MusicChange.None;
            }

            int count = Math.Max(0, unitCount);

            if (playerId == _localPlayerId)
            {
                _localUnitCount = count;
            }
            else if (playerId == _opponentPlayerId)
            {
                _opponentUnitCount = count;
            }
            else
            {
                return MusicChange.None;
            }

            return ChangeBalanceSteps(ComputeBalanceSteps());
        }

        /// <summary>Records the end of the match: the music stops, and the stinger for the local player's result is chosen.</summary>
        /// <remarks>
        /// Only the first call per match counts; a repeat returns none until the next <see cref="ApplyMatchStarted" />. A win
        /// for the local seat is <see cref="MatchStinger.Victory" />, a win for anyone else is
        /// <see cref="MatchStinger.Defeat" />, and no winner is <see cref="MatchStinger.Draw" />. With no
        /// local seat there is no side to report for, so no stinger is chosen.
        /// </remarks>
        /// <param name="outcome">Who won and what ended the match.</param>
        /// <param name="stinger">The stinger to play; meaningful only when the result carries <see cref="MusicChange.Stinger" />.</param>
        /// <returns><see cref="MusicChange.Music" /> when a track was playing, and <see cref="MusicChange.Stinger" /> when one was chosen.</returns>
        public MusicChange ApplyMatchEnded(in MatchOutcome outcome, out MatchStinger stinger)
        {
            stinger = default;

            if (_hasMatchEnded)
            {
                return MusicChange.None;
            }

            _hasMatchEnded = true;

            MusicChange changes = ChangeMusicState(MusicState.None);

            if (!_hasLocalSeat)
            {
                return changes;
            }

            stinger = ResolveStinger(in outcome);

            return changes | MusicChange.Stinger;
        }

        /// <remarks>
        /// Test and disable seam: returns every field to the state a freshly constructed instance starts in, phase and
        /// music included — unlike <see cref="ApplyMatchStarted" />, which deliberately keeps the phase for a rematch that
        /// is still being announced. The caller that needs this one is the one that stopped observing
        /// <see cref="Shared.Events.MatchEvents" /> altogether, and so cannot rely on the next event
        /// putting the phase back.
        /// </remarks>
        internal void Reset()
        {
            _phase = MatchPhase.None;
            _musicState = MusicState.None;
            _intensity = _bands.Idle;
            _standardDurationSeconds = 0f;
            _standardRemainingSeconds = 0f;
            _localPlayerId = 0;
            _opponentPlayerId = 0;
            _localUnitCount = 0;
            _opponentUnitCount = 0;
            _balanceSteps = 0;
            _hasMatchStarted = false;
            _hasLocalSeat = false;
            _hasMatchEnded = false;
        }

        private MatchStinger ResolveStinger(in MatchOutcome outcome)
        {
            if (outcome.IsDraw)
            {
                return MatchStinger.Draw;
            }

            return outcome.WinnerPlayerId == _localPlayerId ? MatchStinger.Victory : MatchStinger.Defeat;
        }

        private MusicChange ChangeMusicStateForPhase(MatchPhase phase)
        {
            return phase switch
            {
                MatchPhase.Loading or MatchPhase.Countdown => _musicState == MusicState.None ? ChangeMusicState(MusicState.Match) : MusicChange.None,
                MatchPhase.None => ChangeMusicState(MusicState.None),
                _ => MusicChange.None,
            };
        }

        private MusicChange ChangeMusicState(MusicState state)
        {
            if (state == _musicState)
            {
                return MusicChange.None;
            }

            _musicState = state;

            return MusicChange.Music;
        }

        private MusicChange RefreshIntensity()
        {
            float elapsedSeconds = _standardDurationSeconds - _standardRemainingSeconds;

            if (!MatchIntensityResolver.TryResolve(_phase, elapsedSeconds, _standardRemainingSeconds, in _bands, out float intensity))
            {
                return MusicChange.None;
            }

            if (intensity == _intensity)
            {
                return MusicChange.None;
            }

            _intensity = intensity;

            return MusicChange.Intensity;
        }

        // Rounded away from zero so the two seats of one board read as exact opposites of each other.
        private int ComputeBalanceSteps()
        {
            int totalUnits = _localUnitCount + _opponentUnitCount;

            if (!_hasLocalSeat || (totalUnits == 0))
            {
                return 0;
            }

            float scaledLead = (float)((_localUnitCount - _opponentUnitCount) * BalanceStepsPerUnit) / totalUnits;

            return (int)MathF.Round(scaledLead, MidpointRounding.AwayFromZero);
        }

        private MusicChange ChangeBalanceSteps(int balanceSteps)
        {
            if (balanceSteps == _balanceSteps)
            {
                return MusicChange.None;
            }

            _balanceSteps = balanceSteps;

            return MusicChange.Balance;
        }
    }
}
