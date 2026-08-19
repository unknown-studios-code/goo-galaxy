using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;

namespace GooGalaxy.Runtime.Match.Data
{
    /// <summary>
    /// The authored shape of a match: how long each timed phase lasts, and the opening position both players
    /// start from.
    /// </summary>
    /// <remarks>
    /// Authored configuration only. The orchestrator copies every value it needs into a
    /// <c>MatchConfiguration</c> when a match starts, so an Inspector edit made mid-match changes the next
    /// match rather than the running one — the same guarantee <c>CardDefinition</c> gives for card data.
    /// <para>
    /// The starting position is authored rather than derived. The GDD's opening is a reflection of one pair of
    /// hexes onto another, not a rotation, so no formula this asset could hold would produce it; and a map with
    /// a different symmetry only has to ship a different asset.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewMatchConfig", menuName = "Goo Galaxy/Match/Match Config")]
    public class MatchConfigSO : ScriptableObject
    {
        // The shortest phase that is still a phase. A duration at or below zero is an authoring mistake rather
        // than an instant phase, so validation clamps to this instead of accepting it.
        private const float MinimumPhaseDurationSeconds = 1f;

        private static readonly StartingPlacement[] _noPlacements = Array.Empty<StartingPlacement>();

        [Header("Phase Durations")]
        [Tooltip("Seconds of normal play before the unit counts decide the match. The GDD authors 180 (3:00); shortening it is a playtest tool, not a mode.")]
        [Min(MinimumPhaseDurationSeconds)]
        [SerializeField]
        private float _standardDurationSeconds = 180f;

        [Tooltip(
            "Seconds of pre-match countdown before plays are accepted. The GDD authors 3, which is what '3 2 1 GO' means. "
                + "Counted down one whole second at a time, so a fractional value is rounded up to the next whole tick."
        )]
        [Min(MinimumPhaseDurationSeconds)]
        [SerializeField]
        private float _countdownSeconds = 3f;

        [Tooltip(
            "Seconds of sudden death after a tied comparison. The GDD authors 60 (1:00). Authored now and carried into the match, but unread "
                + "until GOOM-12 implements overtime — changing it today changes nothing."
        )]
        [Min(MinimumPhaseDurationSeconds)]
        [SerializeField]
        private float _overtimeDurationSeconds = 60f;

        [Header("Starting Position")]
        [Tooltip(
            "Units placed on the board before the countdown. Each card id must exist on the CardPresenter roster and each hex must be free, "
                + "or the match refuses to start rather than seeding a partial board. The GDD authors two per player: P1 at (+4,-4) and (-4,+4), "
                + "P2 at (+4,0) and (-4,0)."
        )]
        [SerializeField]
        private StartingPlacement[] _startingPlacements = Array.Empty<StartingPlacement>();

        /// <summary>Seconds of normal play before the unit counts decide the match.</summary>
        public float StandardDurationSeconds => _standardDurationSeconds;

        /// <summary>Seconds of pre-match countdown before plays are accepted.</summary>
        public float CountdownSeconds => _countdownSeconds;

        /// <summary>Seconds of sudden death after a tied comparison. Unread until GOOM-12.</summary>
        public float OvertimeDurationSeconds => _overtimeDurationSeconds;

        /// <summary>
        /// The opening position, in authored order. Never null; an unauthored asset reads as empty.
        /// </summary>
        /// <remarks>
        /// Read with an indexed <c>for</c> loop. The array is this asset's own storage and is handed out
        /// directly rather than copied, so a caller must not write through it — the orchestrator reads it once
        /// per match and never retains it.
        /// </remarks>
        public IReadOnlyList<StartingPlacement> StartingPlacements => _startingPlacements ?? _noPlacements;

#if UNITY_EDITOR
        protected void OnValidate()
        {
            ValidateAuthoredData();
        }
#endif

        /// <remarks>
        /// Replaces what the Inspector authored, for a caller that has no asset to assign — the same seam
        /// <c>KitDataSO.SetAuthoredCards</c> exists for. Deliberately skips validation, so a caller can observe
        /// what <see cref="ValidateAuthoredData" /> does to raw input.
        /// </remarks>
        internal void SetAuthoredData(
            float standardDurationSeconds,
            float countdownSeconds,
            float overtimeDurationSeconds,
            params StartingPlacement[] placements
        )
        {
            _standardDurationSeconds = standardDurationSeconds;
            _countdownSeconds = countdownSeconds;
            _overtimeDurationSeconds = overtimeDurationSeconds;
            _startingPlacements = placements ?? Array.Empty<StartingPlacement>();
        }

        /// <remarks>
        /// Runs on every Inspector edit through <c>OnValidate</c>. Clamps the durations, because a phase of zero
        /// seconds is a match that cannot be played and the runtime has no better value to substitute; reports
        /// the empty opening position rather than inventing one, because only a designer knows where the units
        /// belong.
        /// </remarks>
        internal void ValidateAuthoredData()
        {
            ClampPhaseDuration(ref _standardDurationSeconds, nameof(StandardDurationSeconds));
            ClampPhaseDuration(ref _countdownSeconds, nameof(CountdownSeconds));
            ClampPhaseDuration(ref _overtimeDurationSeconds, nameof(OvertimeDurationSeconds));

            if (StartingPlacements.Count == 0)
            {
                Debug.LogWarning(string.Format(MatchLogMessages.MatchConfigNoPlacementsFormat, name), this);
            }
        }

        private void ClampPhaseDuration(ref float durationSeconds, string fieldName)
        {
            if (durationSeconds >= MinimumPhaseDurationSeconds)
            {
                return;
            }

            string message = string.Format(
                MatchLogMessages.MatchConfigPhaseDurationInvalidFormat,
                name,
                fieldName,
                durationSeconds,
                MinimumPhaseDurationSeconds
            );

            Debug.LogWarning(message, this);

            durationSeconds = MinimumPhaseDurationSeconds;
        }
    }
}
