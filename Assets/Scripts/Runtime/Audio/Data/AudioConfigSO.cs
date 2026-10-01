using System;
using FMODUnity;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;

namespace GooGalaxy.Runtime.Audio.Data
{
    /// <summary>
    /// The authored shape of the game's audio: the intensity bands the match music moves through, the FMOD events each
    /// cue, track and stinger plays, and the names of the bank and global parameters in the FMOD Studio project.
    /// </summary>
    /// <remarks>
    /// An unset event is a supported state — the FMOD Studio project may not author every sound yet — and plays
    /// nothing. Band edits reach the music at the next scene load (see
    /// <see cref="Controllers.AdaptiveMusicController.Construct" />).
    /// </remarks>
    [CreateAssetMenu(fileName = "NewAudioConfig", menuName = "Goo Galaxy/Audio/Audio Config")]
    public class AudioConfigSO : ScriptableObject
    {
        private const float MinimumIntensity = 0f;
        private const float MaximumIntensity = 1f;

        [Header("Match Intensity")]
        [Tooltip("MatchIntensity while the board loads and the countdown runs. The GDD authors 0: the music waits for play to open.")]
        [Range(MinimumIntensity, MaximumIntensity)]
        [SerializeField]
        private float _idleIntensity;

        [Tooltip("MatchIntensity from the start of Standard play until the early band ends. The GDD authors 0.2.")]
        [Range(MinimumIntensity, MaximumIntensity)]
        [SerializeField]
        private float _earlyIntensity = 0.2f;

        [Tooltip(
            "Seconds of Standard play the early band lasts. The GDD authors 60; that exact second is already mid. At or above the mid band's end, "
                + "the mid band never plays."
        )]
        [Min(0f)]
        [SerializeField]
        private float _earlyUntilElapsedSeconds = 60f;

        [Tooltip("MatchIntensity from the end of the early band until the mid band ends. The GDD authors 0.5.")]
        [Range(MinimumIntensity, MaximumIntensity)]
        [SerializeField]
        private float _midIntensity = 0.5f;

        [Tooltip(
            "Seconds of Standard play after which the mid band gives way to the late band. The GDD authors 120; that exact second is already late. "
                + "Below the early band's end it is raised to it."
        )]
        [Min(0f)]
        [SerializeField]
        private float _midUntilElapsedSeconds = 120f;

        [Tooltip("MatchIntensity for the rest of Standard play, until the final window opens. The GDD authors 0.7.")]
        [Range(MinimumIntensity, MaximumIntensity)]
        [SerializeField]
        private float _lateIntensity = 0.7f;

        [Tooltip("MatchIntensity for the closing seconds of Standard play. The GDD authors 0.85. It overrides every elapsed band.")]
        [Range(MinimumIntensity, MaximumIntensity)]
        [SerializeField]
        private float _finalWindowIntensity = 0.85f;

        [Tooltip(
            "Seconds left on the Standard clock at or below which the final window applies. The GDD authors 15. Set it to the Standard duration "
                + "or longer and all of Standard play runs at the final-window intensity."
        )]
        [Min(0f)]
        [SerializeField]
        private float _finalWindowSeconds = 15f;

        [Tooltip("MatchIntensity for the whole of overtime. The GDD authors 1, the top of the parameter's range.")]
        [Range(MinimumIntensity, MaximumIntensity)]
        [SerializeField]
        private float _overtimeIntensity = 1f;

        [Header("Music")]
        [Tooltip(
            "The adaptive in-match track. Must read the MatchIntensity and TerritoryBalance global parameters to adapt; unset, matches play without music."
        )]
        [SerializeField]
        private EventReference _matchMusic;

        [Header("Stingers")]
        [Tooltip("Played once when the local player wins. Unset, a win ends in silence.")]
        [SerializeField]
        private EventReference _victoryStinger;

        [Tooltip("Played once when the opponent wins. Unset, a loss ends in silence.")]
        [SerializeField]
        private EventReference _defeatStinger;

        [Tooltip("Played once when the match ends with no winner. Unset, a draw ends in silence.")]
        [SerializeField]
        private EventReference _drawStinger;

        [Header("Cues")]
        [Tooltip("The FMOD event each one-shot cue plays. A cue with no row, or a row with no event, stays silent.")]
        [SerializeField]
        private AudioCueEntry[] _cues = Array.Empty<AudioCueEntry>();

        [Header("FMOD Project Names")]
        [Tooltip(
            "Bank holding the match's music, stingers and effects, exactly as FMOD Studio builds it and without the .bank extension. A name "
                + "that matches no built bank leaves the match silent."
        )]
        [SerializeField]
        private string _matchBankName = "Match";

        [Tooltip("Global parameter the match intensity drives, exactly as named in FMOD Studio. It must be a global parameter, not an event one.")]
        [SerializeField]
        private string _matchIntensityParameterName = "MatchIntensity";

        [Tooltip("Global parameter the territory balance drives, exactly as named in FMOD Studio. It must be a global parameter, not an event one.")]
        [SerializeField]
        private string _territoryBalanceParameterName = "TerritoryBalance";

        public IntensityBands Bands =>
            new(
                _idleIntensity,
                _earlyIntensity,
                _earlyUntilElapsedSeconds,
                _midIntensity,
                _midUntilElapsedSeconds,
                _lateIntensity,
                _finalWindowIntensity,
                _finalWindowSeconds,
                _overtimeIntensity
            );

#if UNITY_EDITOR
        protected void OnValidate()
        {
            ValidateAuthoredData();
        }
#endif

        /// <remarks>
        /// Stateless so it can be tested without an asset. Raising the mid band's end to the early band's end is the only
        /// order no single field's attribute can enforce. Intensities that fall as the match goes on are reported but kept,
        /// because a designer may want a quieter overtime.
        /// </remarks>
        internal static IntensityBandIssue ValidateBands(in IntensityBands authored, out IntensityBands validated)
        {
            IntensityBandIssue issues = IntensityBandIssue.None;

            float idle = ClampIntensity(authored.Idle, ref issues);
            float early = ClampIntensity(authored.Early, ref issues);
            float mid = ClampIntensity(authored.Mid, ref issues);
            float late = ClampIntensity(authored.Late, ref issues);
            float finalWindow = ClampIntensity(authored.FinalWindow, ref issues);
            float overtime = ClampIntensity(authored.Overtime, ref issues);

            float earlyUntil = ClampThreshold(authored.EarlyUntilElapsedSeconds, 0f, ref issues);
            float midUntil = ClampThreshold(authored.MidUntilElapsedSeconds, earlyUntil, ref issues);
            float finalWindowSeconds = ClampThreshold(authored.FinalWindowSeconds, 0f, ref issues);

            bool isAscending = (idle <= early) && (early <= mid) && (mid <= late) && (late <= finalWindow) && (finalWindow <= overtime);

            if (!isAscending)
            {
                issues |= IntensityBandIssue.IntensitiesNotAscending;
            }

            validated = new IntensityBands(idle, early, earlyUntil, mid, midUntil, late, finalWindow, finalWindowSeconds, overtime);

            return issues;
        }

        internal void SetAuthoredBands(in IntensityBands bands)
        {
            _idleIntensity = bands.Idle;
            _earlyIntensity = bands.Early;
            _earlyUntilElapsedSeconds = bands.EarlyUntilElapsedSeconds;
            _midIntensity = bands.Mid;
            _midUntilElapsedSeconds = bands.MidUntilElapsedSeconds;
            _lateIntensity = bands.Late;
            _finalWindowIntensity = bands.FinalWindow;
            _finalWindowSeconds = bands.FinalWindowSeconds;
            _overtimeIntensity = bands.Overtime;
        }

        /// <remarks>
        /// Runs through <c>OnValidate</c> — on every Inspector edit, and whenever the asset loads in the Editor. Writes
        /// back what <see cref="ValidateBands" /> corrected, with one warning per kind of fault, and warns about an empty
        /// bank or parameter name, which no value could sensibly replace.
        /// </remarks>
        internal void ValidateAuthoredData()
        {
            IntensityBandIssue issues = ValidateBands(Bands, out IntensityBands validated);

            SetAuthoredBands(in validated);
            ReportBandIssues(issues);
            ReportMissingName(_matchBankName, nameof(_matchBankName));
            ReportMissingName(_matchIntensityParameterName, nameof(_matchIntensityParameterName));
            ReportMissingName(_territoryBalanceParameterName, nameof(_territoryBalanceParameterName));
        }

        /// <remarks>Linear scan of a table a handful of rows long; allocation-free. The first row naming the cue wins.</remarks>
        internal bool TryGetCueEvent(AudioCue cue, out EventReference reference)
        {
            AudioCueEntry[] cues = _cues ?? Array.Empty<AudioCueEntry>();

            for (int i = 0; i < cues.Length; i++)
            {
                if (cues[i].Cue == cue)
                {
                    reference = cues[i].Event;
                    return true;
                }
            }

            reference = default;
            return false;
        }

        internal EventReference GetMusicEvent(MusicState state)
        {
            return state == MusicState.Match ? _matchMusic : default;
        }

        internal EventReference GetStingerEvent(MatchStinger stinger)
        {
            return stinger switch
            {
                MatchStinger.Victory => _victoryStinger,
                MatchStinger.Defeat => _defeatStinger,
                MatchStinger.Draw => _drawStinger,
                _ => default,
            };
        }

        internal string GetBankName(AudioBank bank)
        {
            return bank == AudioBank.Match ? _matchBankName : null;
        }

        internal string GetParameterName(AudioParameter parameter)
        {
            return parameter switch
            {
                AudioParameter.MatchIntensity => _matchIntensityParameterName,
                AudioParameter.TerritoryBalance => _territoryBalanceParameterName,
                _ => null,
            };
        }

        private static float ClampIntensity(float intensity, ref IntensityBandIssue issues)
        {
            if (intensity is >= MinimumIntensity and <= MaximumIntensity)
            {
                return intensity;
            }

            issues |= IntensityBandIssue.IntensityOutOfRange;

            return Mathf.Clamp(intensity, MinimumIntensity, MaximumIntensity);
        }

        private static float ClampThreshold(float seconds, float floorSeconds, ref IntensityBandIssue issues)
        {
            if (seconds >= floorSeconds)
            {
                return seconds;
            }

            issues |= IntensityBandIssue.ThresholdsOutOfOrder;

            return floorSeconds;
        }

        private void ReportBandIssues(IntensityBandIssue issues)
        {
            if ((issues & IntensityBandIssue.IntensityOutOfRange) != 0)
            {
                Debug.LogWarning(string.Format(AudioLogMessages.ConfigIntensityOutOfRangeFormat, name), this);
            }

            if ((issues & IntensityBandIssue.ThresholdsOutOfOrder) != 0)
            {
                Debug.LogWarning(string.Format(AudioLogMessages.ConfigThresholdsOutOfOrderFormat, name), this);
            }

            if ((issues & IntensityBandIssue.IntensitiesNotAscending) != 0)
            {
                Debug.LogWarning(string.Format(AudioLogMessages.ConfigIntensitiesNotAscendingFormat, name), this);
            }
        }

        private void ReportMissingName(string value, string fieldName)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return;
            }

#if UNITY_EDITOR
            string displayName = UnityEditor.ObjectNames.NicifyVariableName(fieldName);
#else
            string displayName = fieldName;
#endif

            Debug.LogWarning(string.Format(AudioLogMessages.ConfigNameMissingFormat, name, displayName), this);
        }
    }
}
