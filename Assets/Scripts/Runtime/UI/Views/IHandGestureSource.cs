using System;
using UnityEngine;

namespace GooGalaxy.Runtime.UI.Views
{
    /// <summary>
    /// The in-match HUD's gesture surface: which hand slot a press landed on, and whether a screen point falls
    /// inside the discard zone the hand strip arms while that press turns into a drag.
    /// </summary>
    /// <remarks>
    /// <b>The hand reports only which slot was pressed, never a position.</b> Every position after the press
    /// comes from the input layer's own pointer source, which is already in screen space. Relaying a position
    /// back out of the view as well would hand the input layer two competing pointer streams to reconcile — the
    /// one it already reads, and one echoed through here — for no benefit, since both would describe the same
    /// pointer. A caller tracks the drag itself and asks <see cref="IsScreenPointInDiscardZone" /> only when it
    /// needs to know where that pointer now sits relative to the zone.
    /// </remarks>
    public interface IHandGestureSource
    {
        /// <summary>Raised when a pointer presses down on a hand slot. Carries the zero-based slot index.</summary>
        public event Action<int> HandSlotPressed;

        /// <summary>Shows or hides the discard zone. Called while a hand-slot drag is live.</summary>
        /// <param name="isArmed">Whether the zone is shown and can accept a discard.</param>
        public void SetDiscardZoneArmed(bool isArmed);

        /// <summary>Whether a screen-space point falls inside the discard zone. False whenever the zone is not armed.</summary>
        /// <param name="screenPosition">The point to test, in screen pixels with the origin bottom-left.</param>
        /// <returns>True when the zone is armed and the point falls inside it.</returns>
        public bool IsScreenPointInDiscardZone(Vector2 screenPosition);
    }
}
