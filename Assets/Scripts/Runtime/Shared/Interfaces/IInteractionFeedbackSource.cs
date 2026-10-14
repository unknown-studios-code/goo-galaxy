using System;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// Reports the steps of the local player's own selection gesture, so feedback such as sound can acknowledge a tap or
    /// a drag without depending on the input layer.
    /// </summary>
    /// <remarks>
    /// Raised synchronously from inside the input callback that took the step, on the main thread. A subscriber reads the
    /// value and must not change the selection from its handler. Nothing here is replicated: it describes this device's
    /// pointer, not a board fact.
    /// </remarks>
    public interface IInteractionFeedbackSource
    {
        /// <summary>
        /// Raised at most once per press, move or release, for the steps worth acknowledging; never with
        /// <see cref="InteractionFeedback.None" />.
        /// </summary>
        public event Action<InteractionFeedback> FeedbackRaised;
    }
}
