namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// One active condition on a unit together with how long it still lasts.
    /// A value type, so a unit's status list holds no per-marker heap object.
    /// </summary>
    public readonly struct StatusMarker
    {
        public StatusMarker(StatusType type, int remainingDuration)
        {
            Type = type;
            RemainingDuration = remainingDuration;
        }

        public StatusType Type { get; }

        /// <summary>
        /// Turns the condition still lasts, counted in defender action windows. Always one or greater while
        /// the marker is held by a unit; the status system drops the marker instead of storing zero.
        /// </summary>
        public int RemainingDuration { get; }
    }
}
