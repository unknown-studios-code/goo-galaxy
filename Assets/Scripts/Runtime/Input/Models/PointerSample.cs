using UnityEngine;

namespace GooGalaxy.Runtime.Input.Models
{
    /// <summary>
    /// One reading of the single pointer the match is played with: where it was, what it was doing, and when.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only shape that crosses the view seam, which is what lets a PlayMode fake drive the whole interaction
    /// without a real touch device behind it. Nothing from <c>UnityEngine.InputSystem</c> appears here on
    /// purpose: a sample is a plain value, so a fixture builds one rather than synthesising a device event.
    /// </para>
    /// <para>
    /// <see cref="ScreenPosition" /> is in screen space — pixels, origin bottom-left — which is the space the
    /// board resolver and <c>IHandGestureSource.IsScreenPointInDiscardZone</c> both accept, and is <b>not</b>
    /// panel space. <see cref="TimestampSeconds" /> is unscaled, so a paused match does not freeze the gesture
    /// clock along with the board.
    /// </para>
    /// <para>Carries only value types, so building one allocates nothing and none of its fields box.</para>
    /// </remarks>
    public readonly struct PointerSample
    {
        /// <summary>Builds one reading of the pointer.</summary>
        /// <param name="screenPosition">Where the pointer was, in screen pixels with the origin bottom-left.</param>
        /// <param name="phase">What the pointer was doing.</param>
        /// <param name="timestampSeconds">Unscaled seconds since startup, as the gesture clock reads them.</param>
        public PointerSample(Vector2 screenPosition, PointerPhase phase, float timestampSeconds)
        {
            ScreenPosition = screenPosition;
            Phase = phase;
            TimestampSeconds = timestampSeconds;
        }

        /// <summary>Where the pointer was, in screen pixels with the origin bottom-left.</summary>
        public Vector2 ScreenPosition { get; }

        /// <summary>What the pointer was doing when this reading was taken.</summary>
        public PointerPhase Phase { get; }

        /// <summary>Unscaled seconds since startup at which this reading was taken.</summary>
        public float TimestampSeconds { get; }
    }

    /// <summary>What the pointer was doing when a <see cref="PointerSample" /> was taken.</summary>
    public enum PointerPhase
    {
        /// <summary>The pointer is not down. What a default-constructed sample carries.</summary>
        None = 0,

        /// <summary>The pointer has just gone down.</summary>
        Pressed = 1,

        /// <summary>The pointer has moved while down.</summary>
        Moved = 2,

        /// <summary>The pointer has just come up.</summary>
        Released = 3,
    }
}
