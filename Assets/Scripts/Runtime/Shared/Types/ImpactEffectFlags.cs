using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// Which kinds of impact a deployment actually carried out, published on <see cref="AbilityResult.ResolvedEffects" />.
    /// </summary>
    /// <remarks>
    /// A flag is set only for an effect that took hold on the board — a hazard really placed on a vacated hex, a live unit
    /// really marked for self-destruct, a condition a unit really accepted, a fuse really armed — never for an effect the
    /// card merely authored. A Clone with a hazard impact vacates nothing, so it reports no <see cref="SpawnHazard" />.
    /// Distinct from <see cref="ImpactEffectType" />, which names what one authored impact is, because a deployment can
    /// resolve several kinds at once.
    /// </remarks>
    [Flags]
    public enum ImpactEffectFlags
    {
        /// <summary>No impact took hold.</summary>
        None = 0,

        /// <summary>At least one unit accepted a condition, refreshes included.</summary>
        ApplyStatus = 1 << 0,

        /// <summary>A hazard was placed on the hex the acting unit vacated.</summary>
        SpawnHazard = 1 << 1,

        /// <summary>
        /// A live acting unit was marked for removal at self-cleanup, by a self-destruct impact or by a fuse detonated
        /// on a Jump.
        /// </summary>
        SelfDestruct = 1 << 2,

        /// <summary>The acting unit was left standing with a fuse running.</summary>
        ArmFuse = 1 << 3,
    }
}
