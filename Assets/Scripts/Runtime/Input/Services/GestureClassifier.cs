using UnityEngine;

namespace GooGalaxy.Runtime.Input.Services
{
    /// <summary>
    /// Decides what a pointer is doing from where it went: a tap that leaves a selection live, a drag that
    /// carries it, and the commit or abandonment a release settles on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Stateless, and the only place the tap-versus-drag line is drawn.</b> Both selection paths ask the same
    /// question of the same threshold, so a card dragged out of hand and a unit dragged across the board feel
    /// alike, and re-tuning the threshold is one authored number rather than two.
    /// </para>
    /// <para>
    /// <b>The threshold is authored in density-independent pixels, never in screen pixels.</b> A raw pixel
    /// threshold is a different physical distance on every device — the same eight pixels are a comfortable
    /// slop on a low-density tablet and an unnoticeable twitch on a high-density phone, so a drag would be
    /// impossible to start on one and impossible to avoid on the other. The conversion multiplies by the screen
    /// density, and <see cref="Screen.dpi" /> reports zero on the platforms that cannot answer, which is
    /// substituted with <see cref="FallbackScreenDpi" /> rather than collapsing the threshold to nothing.
    /// </para>
    /// <para>
    /// Distances are compared squared, so no square root is taken on a path that runs once per pointer move.
    /// Allocation-free on every path.
    /// </para>
    /// </remarks>
    public static class GestureClassifier
    {
        /// <summary>
        /// The density a screen reporting none is treated as having. The Android baseline, where one
        /// density-independent pixel is exactly one screen pixel, so an authored threshold passes through
        /// unchanged rather than being scaled by a number nobody measured.
        /// </summary>
        public const float FallbackScreenDpi = 160f;

        /// <summary>Converts a density-independent distance into the screen pixels it covers on this device.</summary>
        /// <remarks>
        /// Exposed so a caller that classifies several times against one threshold can convert once. Reads
        /// <see cref="Screen.dpi" /> on every call, which is a cached native value rather than a measurement.
        /// </remarks>
        /// <param name="distanceInDp">The distance in density-independent pixels. Negative values pass through.</param>
        /// <returns>The same distance in screen pixels.</returns>
        public static float ConvertDpToPixels(float distanceInDp)
        {
            return ConvertDpToPixels(distanceInDp, Screen.dpi);
        }

        /// <summary>Classifies a pointer that is still down.</summary>
        /// <param name="pressOrigin">Where the pointer went down, in screen pixels.</param>
        /// <param name="currentPosition">Where it is now, in screen pixels.</param>
        /// <param name="dragThresholdInDp">How far it must travel to be a drag, in density-independent pixels.</param>
        /// <returns>
        /// <see cref="PointerGesture.Drag" /> once it has travelled past the threshold, and
        /// <see cref="PointerGesture.Tap" /> until then.
        /// </returns>
        public static PointerGesture ClassifyHold(Vector2 pressOrigin, Vector2 currentPosition, float dragThresholdInDp)
        {
            return HasLeftThreshold(pressOrigin, currentPosition, dragThresholdInDp) ? PointerGesture.Drag : PointerGesture.Tap;
        }

        /// <summary>Classifies a pointer that has just come up.</summary>
        /// <remarks>
        /// A release that never left the threshold is a <see cref="PointerGesture.Tap" /> whatever it is over,
        /// and a tap does not settle a selection — it is what leaves one live so the player can tap a target
        /// next. Only a release that travelled settles anything, and it settles on where it landed.
        /// </remarks>
        /// <param name="pressOrigin">Where the pointer went down, in screen pixels.</param>
        /// <param name="releasePosition">Where it came up, in screen pixels.</param>
        /// <param name="dragThresholdInDp">How far it must have travelled to be a drag, in density-independent pixels.</param>
        /// <param name="isOverCommitTarget">Whether it came up over something the live selection can commit onto.</param>
        /// <returns>
        /// <see cref="PointerGesture.Tap" /> when it never left the threshold, <see cref="PointerGesture.Commit" />
        /// when it travelled and landed on a target, and <see cref="PointerGesture.Cancel" /> when it travelled
        /// and landed anywhere else.
        /// </returns>
        public static PointerGesture ClassifyRelease(Vector2 pressOrigin, Vector2 releasePosition, float dragThresholdInDp, bool isOverCommitTarget)
        {
            if (!HasLeftThreshold(pressOrigin, releasePosition, dragThresholdInDp))
            {
                return PointerGesture.Tap;
            }

            return isOverCommitTarget ? PointerGesture.Commit : PointerGesture.Cancel;
        }

        /// <remarks>
        /// Test-only seam: takes the reported DPI as a parameter instead of reading <see cref="Screen.dpi" />
        /// directly. <see cref="Screen.dpi" /> is machine-specific — this Editor reports 458, never 0 — so the
        /// zero-dpi fallback this method substitutes <see cref="FallbackScreenDpi" /> for is otherwise
        /// unreachable from a fixture on any real display. Applies the same fallback substitution the public
        /// overload uses.
        /// </remarks>
        internal static float ConvertDpToPixels(float distanceInDp, float reportedDpi)
        {
            float dpi = reportedDpi > 0f ? reportedDpi : FallbackScreenDpi;

            return distanceInDp * (dpi / FallbackScreenDpi);
        }

        /// <remarks>
        /// Test-only seam: takes the reported DPI as a parameter for the same reason the matching
        /// <see cref="ConvertDpToPixels(float, float)" /> overload does. <see cref="Screen.dpi" /> is
        /// machine-specific and never 0 on a real display, so a fixture pins it here to a literal — both to
        /// exercise the threshold boundary deterministically and to reach the zero-dpi fallback
        /// <see cref="ConvertDpToPixels(float, float)" /> substitutes <see cref="FallbackScreenDpi" /> for, which
        /// is otherwise unreachable.
        /// </remarks>
        internal static bool HasLeftThreshold(Vector2 pressOrigin, Vector2 currentPosition, float dragThresholdInDp, float reportedDpi)
        {
            float thresholdInPixels = ConvertDpToPixels(dragThresholdInDp, reportedDpi);

            return (currentPosition - pressOrigin).sqrMagnitude > (thresholdInPixels * thresholdInPixels);
        }

        private static bool HasLeftThreshold(Vector2 pressOrigin, Vector2 currentPosition, float dragThresholdInDp)
        {
            return HasLeftThreshold(pressOrigin, currentPosition, dragThresholdInDp, Screen.dpi);
        }
    }

    /// <summary>What a pointer reading means for the live selection.</summary>
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
