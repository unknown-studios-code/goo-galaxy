using System;
using GooGalaxy.Runtime.Input.Models;
using UnityEngine;

namespace GooGalaxy.Runtime.Input.Interfaces
{
    /// <summary>
    /// The single pointer a match is played with, as the interaction layer sees it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The seam a fake replaces.</b> Everything above this contract is driven by <see cref="PointerSample" />
    /// values, so a PlayMode fixture raises a press, a move and a release directly instead of synthesising a
    /// touch device and waiting for the Input System to deliver it. That is what makes the whole interaction —
    /// select, drag, commit, cancel — testable without a device.
    /// </para>
    /// <para>
    /// <b>One pointer, never two.</b> An implementation reports the first pointer that went down and ignores
    /// every other until it comes up, so a second finger can neither start a second selection nor move a live
    /// one. A subscriber may therefore treat a press as always following a release.
    /// </para>
    /// <para>
    /// Positions are in screen space — pixels, origin bottom-left. Every event carries the position that
    /// <see cref="CurrentScreenPosition" /> reports at that instant, so a subscriber never has to read both.
    /// </para>
    /// </remarks>
    public interface IPointerSource
    {
        /// <summary>Raised when the pointer goes down, carrying the reading taken at that instant.</summary>
        public event Action<PointerSample> PointerPressed;

        /// <summary>Raised when the pointer moves while down, carrying the reading taken at that instant.</summary>
        /// <remarks>Not raised while the pointer is up, so a subscriber tracking a gesture needs no hover filter of its own.</remarks>
        public event Action<PointerSample> PointerMoved;

        /// <summary>Raised when the pointer comes up, carrying the reading taken at that instant.</summary>
        /// <remarks>Raised only for the pointer that <see cref="PointerPressed" /> reported, so a lifted second finger is silent.</remarks>
        public event Action<PointerSample> PointerReleased;

        /// <summary>Where the pointer is now, in screen pixels with the origin bottom-left.</summary>
        public Vector2 CurrentScreenPosition { get; }

        /// <summary>Whether the pointer is currently down.</summary>
        public bool IsPointerDown { get; }
    }
}
