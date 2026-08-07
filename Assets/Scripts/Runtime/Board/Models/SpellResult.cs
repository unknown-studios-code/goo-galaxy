namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// The outcome of a Protocol deployment request. Every rejection reason is a distinct code so callers
    /// (UI feedback, AI, network reconciliation) can react without re-running validation.
    /// </summary>
    /// <remarks>
    /// Values are explicit and numbered separately from <see cref="MovementResult"/>: the two describe
    /// different actions and are never compared, so sharing a numbering would imply a relationship that does
    /// not exist. Adding a member is safe; renumbering or reordering one silently changes what an older peer
    /// reads once the code travels to the client as a rejection reason.
    /// <para>
    /// Energy is not represented here. Paying for the card is step 1 of the GDD's resolution order and belongs
    /// to the caller, exactly as it does for a troop move — <c>UnitPresenter.ResolveMove</c> does not spend
    /// Energy either.
    /// </para>
    /// </remarks>
    public enum SpellResult
    {
        /// <summary>
        /// The Protocol was legal, its impacts have been applied, and its action window has been closed.
        /// </summary>
        Success = 0,

        /// <summary>
        /// No initialized hex grid or unit registry was available, so the Protocol could not be evaluated.
        /// The board is unchanged.
        /// </summary>
        BoardUnavailable = 1,

        /// <summary>
        /// The card carries no landing impacts, so there was nothing for the deployment to resolve. A card with
        /// no ability capability at all reports the same code — from the board's side the two are identical.
        /// </summary>
        CardHasNoImpacts = 2,

        /// <summary>
        /// The chosen hexes do not form the cluster the card authored: the wrong number of them, a hex that is
        /// not on the board, a repeated hex, or one further from the centre than the impact's radius allows.
        /// </summary>
        InvalidTargets = 3,

        /// <summary>
        /// A deployment is already being resolved. Re-entrant requests from an <c>AbilityResolved</c>
        /// subscriber are rejected so the in-flight payload stays valid; queue the follow-up instead.
        /// </summary>
        ResolverBusy = 4,
    }
}
