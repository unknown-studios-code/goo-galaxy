namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// The outcome of a movement request. Every rejection reason is a distinct code so callers
    /// (UI feedback, AI, network reconciliation) can react without re-running validation.
    /// </summary>
    /// <remarks>
    /// Values are explicit because the code travels to the client as a rejection reason: adding a member is
    /// safe, renumbering or reordering one silently changes what an older peer reads.
    /// </remarks>
    public enum MovementResult
    {
        /// <summary>
        /// The move was legal and has been applied to the board.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The source coordinate is outside the grid or holds no unit.
        /// </summary>
        SourceEmpty = 1,

        /// <summary>
        /// The unit on the source cell belongs to another player.
        /// </summary>
        SourceNotOwned = 2,

        /// <summary>
        /// The unit is under Cryo-Stasis, which bars it from both Clone and Jump until the status expires.
        /// </summary>
        SourceFrozen = 3,

        /// <summary>
        /// The commanded unit is not registered, or is not the unit standing on the source cell.
        /// </summary>
        UnitNotFound = 4,

        /// <summary>
        /// The target cell already holds a unit.
        /// </summary>
        TargetOccupied = 5,

        /// <summary>
        /// The target coordinate is outside the grid or is an impassable cell.
        /// </summary>
        TargetBlocked = 6,

        /// <summary>
        /// The hex distance between source and target does not match the requested move type.
        /// </summary>
        OutOfRange = 7,

        /// <summary>
        /// The unit's card data does not permit the requested move type.
        /// </summary>
        CapabilityMissing = 8,

        /// <summary>
        /// The command carries a move type that is not defined.
        /// </summary>
        InvalidCommand = 9,

        /// <summary>
        /// No initialized hex grid was available, so the move could not be evaluated. The board is unchanged.
        /// </summary>
        BoardUnavailable = 10,

        /// <summary>
        /// The move was legal, but the unit spawner failed to produce the cloned unit. The board is unchanged.
        /// </summary>
        SpawnFailed = 11,

        /// <summary>
        /// A move is already being resolved. Re-entrant requests from a <c>MoveExecuted</c> subscriber are rejected
        /// so the in-flight affected-coordinate payload stays valid; queue the follow-up move instead.
        /// </summary>
        ResolverBusy = 12,
    }
}
