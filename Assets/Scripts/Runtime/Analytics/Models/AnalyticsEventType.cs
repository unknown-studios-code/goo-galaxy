namespace GooGalaxy.Runtime.Analytics.Models
{
    /// <summary>
    /// What one <see cref="AnalyticsRecord" /> describes. Each member is written to the session file by name, as the
    /// snake_case event key <see cref="AnalyticsRecordFormatter" /> maps it to.
    /// </summary>
    /// <remarks>
    /// Values are explicit so <see cref="None" /> is pinned to zero, which keeps a default-constructed record from
    /// claiming to be a real event; the formatter writes nothing for it. The number itself never leaves the process
    /// — the file carries the event name — so renumbering the other members changes no output.
    /// </remarks>
    public enum AnalyticsEventType
    {
        /// <summary>Not an event. What a default-constructed <see cref="AnalyticsRecord" /> carries.</summary>
        None = 0,

        /// <summary>The capture session opened. Carries the device, OS and app version from the session.</summary>
        SessionStart = 1,

        /// <summary>The capture session closed. Carries its duration and how many matches it saw.</summary>
        SessionEnd = 2,

        /// <summary>A match was announced. Carries its seed, the two seats' control, and the phase durations.</summary>
        MatchStart = 3,

        /// <summary>A match ended. Carries the winner, the reason, both scores, the duration and whether it went to Overtime.</summary>
        MatchEnd = 4,

        /// <summary>A card play succeeded. Carries the card, the first target hex and the card's Energy cost.</summary>
        CardDeployed = 5,

        /// <summary>A card play was refused. Carries the card, when known, and the rejection reason.</summary>
        CardPlayRejected = 6,

        /// <summary>An Energy spend was attempted. Carries the balance after it and whether it succeeded.</summary>
        EnergySpent = 7,

        /// <summary>A landing converted or stripped units. Carries both counts.</summary>
        ConversionEvent = 8,

        /// <summary>
        /// A deployment's impacts resolved. Carries the affected unit count split into the acting player's own units
        /// and everyone else's, the affected hex count, and the destroyed unit count.
        /// </summary>
        AbilityResolved = 9,

        /// <summary>A card was discarded from hand. Carries the card and the slot it left.</summary>
        CardDiscarded = 10,

        /// <summary>The match entered a new phase. Carries the phase.</summary>
        PhaseChanged = 11,

        /// <summary>
        /// A unit already on the board executed a Clone or a Jump. Carries the move type, the source and target
        /// hexes, and the unit that moved. A Deploy is not captured here — see <see cref="CardDeployed" />.
        /// </summary>
        MoveExecuted = 12,

        /// <summary>
        /// A deployment's impact put a condition on a unit, refreshes included. Carries the unit, its owner, the
        /// condition and how many action windows it lasts; the record's player is the one who deployed.
        /// </summary>
        StatusApplied = 13,

        /// <summary>
        /// A condition's duration ran out and it dropped off a unit. Carries the unit and the condition; the record's
        /// player is the unit's owner.
        /// </summary>
        StatusExpired = 14,
    }
}
