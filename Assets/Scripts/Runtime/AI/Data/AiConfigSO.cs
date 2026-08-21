using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;

namespace GooGalaxy.Runtime.AI.Data
{
    /// <summary>
    /// The authored shape of a machine player: how long it waits between actions, the Energy balance that cuts
    /// that wait short, whether it may cycle a dead hand, and the seed its randomness derives from.
    /// </summary>
    /// <remarks>
    /// Authored configuration only. The controller copies every value into an <see cref="AiConfig" /> when a
    /// match starts, so an Inspector edit made mid-match changes the next match rather than the running one —
    /// the same guarantee <c>MatchConfigSO</c> gives the match and <c>CardDefinition</c> gives card data. It is
    /// also what keeps the enumerator and the strategy free of any <c>ScriptableObject</c>.
    /// <para>
    /// Nothing here describes <i>how</i> the opponent plays. The launch tier picks uniformly among the legal
    /// actions and reads nothing into the board, so difficulty is a different strategy rather than a different
    /// number on this asset.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "NewAiConfig", menuName = "Goo Galaxy/AI/AI Config")]
    public class AiConfigSO : ScriptableObject
    {
        // The shortest authored think interval that is still a pause. Below this the opponent reads as an
        // instant reaction rather than as a player, and a value at or below zero would spin the loop.
        private const float MinimumThinkSeconds = 0.1f;

        [Header("Think Cadence")]
        [Tooltip(
            "Seconds the opponent waits at least before acting again. The GDD's action window is human-paced, so below about 1 it stops reading "
                + "as a player taking a turn and starts reading as a script."
        )]
        [Min(MinimumThinkSeconds)]
        [SerializeField]
        private float _minThinkSeconds = 1.5f;

        [Tooltip(
            "Seconds the opponent waits at most. A ceiling on waiting, not a fixed cadence: the wait is drawn between this and the minimum, and is "
                + "abandoned early whenever the Energy ceiling is crossed. Authored below the minimum it is raised to it."
        )]
        [Min(MinimumThinkSeconds)]
        [SerializeField]
        private float _maxThinkSeconds = 3f;

        [Header("Spending")]
        [Tooltip(
            "Energy balance at which the remaining wait is abandoned and the opponent acts at once, so a balance near the cap is spent rather "
                + "than regenerated into nothing. Read against EnergyConfig's MaxEnergy and RegenRate on the EnergyPresenter: leave roughly one "
                + "maximum think interval of headroom below the cap. Above the cap the truncation never fires; at the cap it fires every time "
                + "Energy tops out."
        )]
        [Min(0f)]
        [SerializeField]
        private float _energyCeilingThreshold = 8f;

        [Tooltip(
            "Whether a think tick that finds no legal action may discard a card instead of doing nothing. Off, an opponent holding four unaffordable "
                + "cards stalls until Energy regenerates rather than cycling the hand."
        )]
        [SerializeField]
        private bool _isDiscardEnabled = true;

        [Header("Determinism")]
        [Tooltip(
            "Seed the opponent's two streams derive from. Zero means derive from the match seed instead, which is what makes a match reproducible "
                + "from its own seed alone; author a non-zero value only to pin one opponent's behaviour across matches while debugging."
        )]
        [SerializeField]
        private int _seed;

        /// <summary>The authored values, as the engine-free tuning the controller passes down.</summary>
        public AiConfig Config => new(_minThinkSeconds, _maxThinkSeconds, _energyCeilingThreshold, _isDiscardEnabled, _seed);

#if UNITY_EDITOR
        protected void OnValidate()
        {
            ValidateAuthoredData();
        }
#endif

        /// <remarks>
        /// Replaces what the Inspector authored, for a caller that has no asset to assign — the same seam
        /// <c>MatchConfigSO.SetAuthoredData</c> exists for. Deliberately skips validation, so a caller can
        /// observe what <see cref="ValidateAuthoredData" /> does to raw input.
        /// </remarks>
        internal void SetAuthoredData(float minThinkSeconds, float maxThinkSeconds, float energyCeilingThreshold, bool isDiscardEnabled, int seed)
        {
            _minThinkSeconds = minThinkSeconds;
            _maxThinkSeconds = maxThinkSeconds;
            _energyCeilingThreshold = energyCeilingThreshold;
            _isDiscardEnabled = isDiscardEnabled;
            _seed = seed;
        }

        /// <remarks>
        /// Runs on every Inspector edit through <c>OnValidate</c>. The <c>[Min]</c> attributes already hold each
        /// interval above the floor, so the only rule left is the one no single field can express: a maximum
        /// below the minimum, which would make every wait exactly the minimum and hide the authoring mistake
        /// behind plausible behaviour.
        /// </remarks>
        internal void ValidateAuthoredData()
        {
            if (_maxThinkSeconds >= _minThinkSeconds)
            {
                return;
            }

            Debug.LogWarning(string.Format(AiLogMessages.AiConfigThinkRangeInvalidFormat, name, _maxThinkSeconds, _minThinkSeconds), this);

            _maxThinkSeconds = _minThinkSeconds;
        }
    }
}
