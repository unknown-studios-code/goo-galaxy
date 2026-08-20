using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;

namespace GooGalaxy.Runtime.Match.Data
{
    /// <summary>
    /// The authored shape of a match: how long each timed phase lasts, how long an overtime lead must be held
    /// to win outright, and the opening position both players start from.
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
        // The shortest authored interval that is still meaningful — a phase duration, or the overtime lead hold.
        // A value at or below zero is an authoring mistake rather than an instant one, so validation clamps to
        // this instead of accepting it.
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
            "Seconds of sudden death after a tied comparison. The GDD authors 60 (1:00). Overtime opens when normal play ends on level unit "
                + "counts, and whoever is ahead when this runs out wins it."
        )]
        [Min(MinimumPhaseDurationSeconds)]
        [SerializeField]
        private float _overtimeDurationSeconds = 60f;

        [Tooltip(
            "Seconds a unit-count lead must be held unbroken to win overtime outright. The GDD authors 3, and 1 is the enforced floor. Below "
                + "about 2 a single conversion settles overtime before the other player can answer it; set it above the overtime duration and "
                + "the hold can never complete, so the overtime clock decides every match."
        )]
        [Min(MinimumPhaseDurationSeconds)]
        [SerializeField]
        private float _overtimeLeadHoldSeconds = 3f;

        [Header("Starting Position")]
        [Tooltip(
            "Units placed on the board before the countdown. Each card id must exist on the CardPresenter roster and each hex must be free, "
                + "or the match refuses to start rather than seeding a partial board. The GDD authors two per player: P1 at (+4,-4) and (-4,+4), "
                + "P2 at (+4,0) and (-4,0)."
        )]
        [SerializeField]
        private StartingPlacement[] _startingPlacements = Array.Empty<StartingPlacement>();

        [Header("Catch-Up Bonus")]
        [SerializeField]
        private CatchUpConfig _catchUp = new(0.4f, 1.15f, 20f, 60f);

        /// <summary>Seconds of normal play before the unit counts decide the match.</summary>
        public float StandardDurationSeconds => _standardDurationSeconds;

        /// <summary>Seconds of pre-match countdown before plays are accepted.</summary>
        public float CountdownSeconds => _countdownSeconds;

        /// <summary>Seconds of sudden death after normal play ended on level unit counts.</summary>
        public float OvertimeDurationSeconds => _overtimeDurationSeconds;

        /// <summary>Seconds a unit-count lead must be held unbroken to win overtime outright.</summary>
        public float OvertimeLeadHoldSeconds => _overtimeLeadHoldSeconds;

        /// <summary>
        /// The opening position, in authored order. Never null; an unauthored asset reads as empty.
        /// </summary>
        /// <remarks>
        /// Read with an indexed <c>for</c> loop. The array is this asset's own storage and is handed out
        /// directly rather than copied, so a caller must not write through it — the orchestrator reads it once
        /// per match and never retains it.
        /// </remarks>
        public IReadOnlyList<StartingPlacement> StartingPlacements => _startingPlacements ?? _noPlacements;

        /// <summary>
        /// Authored parameters for the catch-up Energy bonus — see <see cref="CatchUpConfig" /> for the four
        /// fields and the band each one is authorable within.
        /// </summary>
        public CatchUpConfig CatchUp => _catchUp;

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
            float overtimeLeadHoldSeconds,
            params StartingPlacement[] placements
        )
        {
            _standardDurationSeconds = standardDurationSeconds;
            _countdownSeconds = countdownSeconds;
            _overtimeDurationSeconds = overtimeDurationSeconds;
            _overtimeLeadHoldSeconds = overtimeLeadHoldSeconds;
            _startingPlacements = placements ?? Array.Empty<StartingPlacement>();
        }

        /// <remarks>
        /// The catch-up counterpart to <see cref="SetAuthoredData" />, and separate from it only because that
        /// method ends in a <c>params</c> array, which nothing can follow. Skips validation on the same terms,
        /// so a caller can observe what <see cref="ValidateAuthoredData" /> does to an out-of-band value.
        /// </remarks>
        internal void SetAuthoredCatchUp(CatchUpConfig catchUp)
        {
            _catchUp = catchUp;
        }

        /// <remarks>
        /// Runs on every Inspector edit through <c>OnValidate</c>. Clamps the three phase durations and the
        /// overtime lead hold on the same floor — a phase of zero seconds is a match that cannot be played, a
        /// hold of zero settles overtime on the first conversion, and the runtime has no better value to
        /// substitute for either; reports the empty opening position rather than inventing one, because only a
        /// designer knows where the units belong; then clamps every <see cref="CatchUp" /> field into its own
        /// authored band.
        /// </remarks>
        internal void ValidateAuthoredData()
        {
            ClampPhaseDuration(ref _standardDurationSeconds, nameof(StandardDurationSeconds));
            ClampPhaseDuration(ref _countdownSeconds, nameof(CountdownSeconds));
            ClampPhaseDuration(ref _overtimeDurationSeconds, nameof(OvertimeDurationSeconds));
            ClampPhaseDuration(ref _overtimeLeadHoldSeconds, nameof(OvertimeLeadHoldSeconds));

            if (StartingPlacements.Count == 0)
            {
                Debug.LogWarning(string.Format(MatchLogMessages.MatchConfigNoPlacementsFormat, name), this);
            }

            ValidateCatchUp();
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

        // Reconstructed rather than mutated in place: CatchUpConfig's properties carry a private setter, the
        // same shape EnergyConfig uses, so a clamped value is assembled locally and only written back if at
        // least one field actually moved. The bands come from CatchUpConfig itself rather than being restated
        // here, so this clamp and the [Range] attributes it backs up can never disagree.
        private void ValidateCatchUp()
        {
            float thresholdRatio = _catchUp.ThresholdRatio;
            float regenMultiplier = _catchUp.RegenMultiplier;
            float durationSeconds = _catchUp.DurationSeconds;
            float cooldownSeconds = _catchUp.CooldownSeconds;

            bool wasClamped = ClampCatchUpField(
                ref thresholdRatio,
                nameof(CatchUpConfig.ThresholdRatio),
                CatchUpConfig.MinThresholdRatio,
                CatchUpConfig.MaxThresholdRatio
            );
            wasClamped |= ClampCatchUpField(
                ref regenMultiplier,
                nameof(CatchUpConfig.RegenMultiplier),
                CatchUpConfig.MinRegenMultiplier,
                CatchUpConfig.MaxRegenMultiplier
            );
            wasClamped |= ClampCatchUpField(
                ref durationSeconds,
                nameof(CatchUpConfig.DurationSeconds),
                CatchUpConfig.MinDurationSeconds,
                CatchUpConfig.MaxDurationSeconds
            );
            wasClamped |= ClampCatchUpField(
                ref cooldownSeconds,
                nameof(CatchUpConfig.CooldownSeconds),
                CatchUpConfig.MinCooldownSeconds,
                CatchUpConfig.MaxCooldownSeconds
            );

            if (!wasClamped)
            {
                return;
            }

            _catchUp = new CatchUpConfig(thresholdRatio, regenMultiplier, durationSeconds, cooldownSeconds);
        }

        private bool ClampCatchUpField(ref float value, string fieldName, float min, float max)
        {
            float clamped = Mathf.Clamp(value, min, max);

            if (clamped == value)
            {
                return false;
            }

            string message = string.Format(MatchLogMessages.MatchConfigCatchUpFieldInvalidFormat, name, fieldName, value, min, max, clamped);

            Debug.LogWarning(message, this);

            value = clamped;

            return true;
        }
    }
}
