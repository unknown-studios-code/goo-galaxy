namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// One authored landing impact, resolved into its immutable runtime form: what it does, what it applies,
    /// how far it reaches, how long it lasts, who it hits, and how many targets it may take.
    /// Lives in Shared so the board's ability rules can read a card's impacts without depending on Cards.
    /// </summary>
    /// <remarks>
    /// Pure data with no behavior: every rule that reads these fields lives in the board's ability resolver, so
    /// a card is expressible as authored values alone and no card needs its own code path.
    /// </remarks>
    public readonly struct ImpactEffect
    {
        public ImpactEffect(
            ImpactEffectType type,
            StatusType status,
            int radius,
            int duration,
            TargetFilter target,
            int clusterSize,
            ImpactDurationUnit durationUnit = ImpactDurationUnit.ActionWindows
        )
        {
            Type = type;
            Status = status;
            Radius = radius;
            Duration = duration;
            Target = target;
            ClusterSize = clusterSize;
            DurationUnit = durationUnit;
        }

        /// <summary>The kind of work this impact performs, and the value the resolver dispatches on.</summary>
        public ImpactEffectType Type { get; }

        /// <summary>
        /// The condition applied to every selected unit. Only meaningful for
        /// <see cref="ImpactEffectType.ApplyStatus"/>; other impact types ignore it.
        /// </summary>
        public StatusType Status { get; }

        /// <summary>
        /// Hex rings around the landing hex the impact reaches. Zero covers the landing hex alone, one adds its
        /// six neighbours, and so on.
        /// </summary>
        public int Radius { get; }

        /// <summary>
        /// How long the result lasts, counted in whatever <see cref="ImpactDurationUnit"/> says. A value below one
        /// leaves the impact with nothing to apply and it is skipped.
        /// </summary>
        /// <remarks>
        /// The unit is not implied by the number: a status counts defender action windows, a hazard counts owner
        /// action windows, and a fuse counts seconds of scaled match time. An impact whose type and
        /// <see cref="ImpactDurationUnit"/> disagree is an authoring error — the resolver skips that one impact and
        /// reports it rather than guessing which of the two the designer meant.
        /// </remarks>
        public int Duration { get; }

        /// <summary>
        /// What <see cref="Duration"/> counts. <see cref="ImpactDurationUnit.Seconds"/> only ever pairs with
        /// <see cref="ImpactEffectType.ArmFuse"/>; every other impact type is measured in action windows.
        /// </summary>
        public ImpactDurationUnit DurationUnit { get; }

        /// <summary>Which units inside <see cref="Radius"/> the impact applies to.</summary>
        public TargetFilter Target { get; }

        /// <summary>
        /// On a troop impact, the most units it may affect, or zero for no ceiling.
        /// On a Protocol, additionally the <b>exact number of hexes the player must pick</b>, and zero is
        /// invalid rather than unlimited.
        /// </summary>
        /// <remarks>
        /// On a Protocol this is the exact hex count the player picks, so zero is invalid rather than unlimited
        /// and the deployment is rejected as <c>InvalidTargets</c>. The GDD's "3-hex cluster (1 center + 2
        /// adjacent)" is this field at three with <see cref="Radius"/> at one.
        /// </remarks>
        public int ClusterSize { get; }
    }
}
