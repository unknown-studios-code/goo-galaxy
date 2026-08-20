using System;
using UnityEngine;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <summary>
    /// Authored parameters for the catch-up Energy bonus: how far behind a player must fall, how much their
    /// regeneration speeds up, how long the window lasts, and how long it locks out afterward.
    /// </summary>
    [Serializable]
    public struct CatchUpConfig
    {
        // The authorable band for each field, enforced in the Inspector rather than only described in the
        // tooltip. It bounds what a designer can dial in; it does not bind the constructor, which tests drive
        // past both ends on purpose — see EnergyConfig for the same convention.
        //
        // Internal rather than private because MatchConfigSO clamps the same four fields on the paths [Range]
        // cannot reach, and a second copy of the band declared over there would be free to drift out of step
        // with the attributes these bind.
        internal const float MinThresholdRatio = 0.1f;

        // Deliberately just under a half. At exactly 0.5 an even split puts BOTH players at or below the
        // threshold — 5 of 10 satisfies 5 <= 10 * 0.5f on each side — which opens both windows, boosts both
        // players to no relative effect, and burns both cooldowns. The exclusive bound is what makes
        // CatchUpTracker's "at most one player at a time" invariant true rather than merely usual.
        internal const float MaxThresholdRatio = 0.49f;

        internal const float MinRegenMultiplier = 1f;

        internal const float MaxRegenMultiplier = 1.5f;

        internal const float MinDurationSeconds = 5f;

        internal const float MaxDurationSeconds = 60f;

        internal const float MinCooldownSeconds = 0f;

        internal const float MaxCooldownSeconds = 180f;

        public CatchUpConfig(float thresholdRatio, float regenMultiplier, float durationSeconds, float cooldownSeconds)
        {
            ThresholdRatio = thresholdRatio;
            RegenMultiplier = regenMultiplier;
            DurationSeconds = durationSeconds;
            CooldownSeconds = cooldownSeconds;
        }

        [field: Tooltip(
            "Share of live units at or below which a player is in a deficit and the bonus can open. 0.4 means 40% or fewer. Near the 0.49 ceiling "
                + "it fires for a one-unit deficit, which is normal play and not a deficit; below about 0.1 the game is already lost by the time it arrives."
        )]
        [field: Range(MinThresholdRatio, MaxThresholdRatio)]
        [field: SerializeField]
        public float ThresholdRatio { get; private set; }

        [field: Tooltip(
            "Energy regeneration multiplier while the bonus is active. 1.15 is +15%. 1.0 disables the mechanic entirely; the GDD's correction "
                + "band is +10% to +20%, and past roughly 1.5 the bonus decides matches outright instead of opening a comeback window."
        )]
        [field: Range(MinRegenMultiplier, MaxRegenMultiplier)]
        [field: SerializeField]
        public float RegenMultiplier { get; private set; }

        [field: Tooltip(
            "Seconds the bonus stays active once it opens. One whole Energy takes 18.7 seconds of active bonus in Standard, so below about 5 "
                + "seconds the window buys nothing meaningful; at 60 it covers a third of the match."
        )]
        [field: Range(MinDurationSeconds, MaxDurationSeconds)]
        [field: SerializeField]
        public float DurationSeconds { get; private set; }

        [field: Tooltip(
            "Seconds after the bonus expires before it can re-arm, even if the player is still below threshold. At 0 the window closes for a "
                + "single tick and re-opens on the next, so a player held below threshold is boosted almost continuously and CatchUpChanged "
                + "publishes an open/close pair every DurationSeconds — the oscillation this cooldown exists to stop. At 180 the bonus can fire "
                + "at most once in a standard match."
        )]
        [field: Range(MinCooldownSeconds, MaxCooldownSeconds)]
        [field: SerializeField]
        public float CooldownSeconds { get; private set; }
    }
}
