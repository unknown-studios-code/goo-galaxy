using System;

namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// The conditions an ability resolution ran into that a human should hear about. Reported as flags rather
    /// than logged where they happen, so the rules stay a pure function and the presenter keeps the console.
    /// </summary>
    /// <remarks>
    /// None of these aborts the resolution: every remaining impact of the landing still resolves. A set flag
    /// means either an authoring mistake in a card asset or board state that drifted, never a rule the player
    /// broke — those are movement results, not diagnostics.
    /// </remarks>
    [Flags]
    internal enum AbilityDiagnostic
    {
        /// <summary>Nothing to report; the impacts resolved exactly as authored.</summary>
        None = 0,

        /// <summary>
        /// A hazard was spawned on a hex that already carried one. The previous remaining duration was
        /// discarded rather than extended.
        /// </summary>
        HazardOverwritten = 1,

        /// <summary>
        /// A self-destruct impact resolved on a unit that was already dead or no longer registered, so there
        /// was nothing to mark for cleanup.
        /// </summary>
        SelfDestructOnDeadUnit = 2,

        /// <summary>
        /// A card authored an impact type the resolver has no case for. That impact was skipped and the rest
        /// of the card's impacts still resolved.
        /// </summary>
        UnknownEffectType = 4,

        /// <summary>
        /// A hazard impact resolved on a deployment that vacated no hex — a Clone, or any Protocol. There was
        /// nowhere to put the trail, so the impact was skipped.
        /// </summary>
        HazardWithoutVacatedHex = 8,

        /// <summary>
        /// A self-destruct impact resolved on a deployment with no unit acting on the board, which is every
        /// Protocol. There was nothing to destroy, so the impact was skipped.
        /// </summary>
        SelfDestructWithoutActingUnit = 16,

        /// <summary>
        /// A card authored an impact whose duration unit its type cannot read — a status or hazard in seconds,
        /// or a fuse in action windows. That impact was skipped rather than reinterpreted, and the rest of the
        /// card's impacts still resolved.
        /// </summary>
        DurationUnitMismatch = 32,

        /// <summary>
        /// A fuse impact resolved on a deployment with no unit acting on the board, which is every Protocol.
        /// There was nothing to arm, so the impact was skipped.
        /// </summary>
        FuseWithoutActingUnit = 64,
    }
}
