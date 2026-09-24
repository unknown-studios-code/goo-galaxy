namespace GooGalaxy.Runtime.Input.Models
{
    /// <summary>What a pointer reading means for the live selection.</summary>
    /// <remarks>
    /// Lives beside the other input value types rather than next to <c>GestureClassifier</c>, which produces it:
    /// <c>Services/</c> holds stateless rules and an enum is not one, the same split <c>MovementResult</c> and
    /// <c>MovementValidator</c> already follow.
    /// </remarks>
    public enum PointerGesture
    {
        /// <summary>Nothing to act on.</summary>
        None = 0,

        /// <summary>The pointer has not travelled far enough to be a drag. A selection it ends stays live.</summary>
        Tap = 1,

        /// <summary>The pointer has travelled past the threshold and is carrying the selection.</summary>
        Drag = 2,

        /// <summary>The pointer came up over something the selection can commit onto.</summary>
        Commit = 3,

        /// <summary>The pointer came up somewhere the selection cannot commit onto, abandoning it.</summary>
        Cancel = 4,
    }
}
