using GooGalaxy.Runtime.Input.Services;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Input
{
    [TestFixture]
    public class GestureClassifierTests
    {
        // A threshold of zero dp converts to zero pixels regardless of the device's reported density, which is
        // what keeps ClassifyHold and ClassifyRelease's own tests independent of the Editor's real Screen.dpi.
        private const float ZeroThresholdInDp = 0f;

        // FallbackScreenDpi makes ConvertDpToPixels's ratio exactly one, so the threshold-in-pixels below equals
        // the authored dp value as a literal rather than a value only the code under test could produce.
        private const float DragThresholdInDp = 10f;

        private static readonly Vector2 _origin = Vector2.zero;

        [Test]
        public void ClassifyHold_PointerHasNotMoved_ReturnsTap()
        {
            // GIVEN

            // WHEN
            PointerGesture gesture = GestureClassifier.ClassifyHold(_origin, _origin, ZeroThresholdInDp);

            // THEN
            Assert.That(gesture, Is.EqualTo(PointerGesture.Tap));
        }

        [Test]
        public void ClassifyHold_PointerHasMovedAtAllPastAZeroThreshold_ReturnsDrag()
        {
            // GIVEN
            var currentPosition = new Vector2(1f, 0f);

            // WHEN
            PointerGesture gesture = GestureClassifier.ClassifyHold(_origin, currentPosition, ZeroThresholdInDp);

            // THEN
            Assert.That(gesture, Is.EqualTo(PointerGesture.Drag));
        }

        [Test]
        public void ClassifyRelease_PointerNeverLeftTheThreshold_ReturnsTapEvenOverACommitTarget()
        {
            // GIVEN — a release that never travelled settles nothing, whatever it landed on.

            // WHEN
            PointerGesture gesture = GestureClassifier.ClassifyRelease(_origin, _origin, ZeroThresholdInDp, isOverCommitTarget: true);

            // THEN
            Assert.That(gesture, Is.EqualTo(PointerGesture.Tap));
        }

        [Test]
        public void ClassifyRelease_PointerTravelledAndLandedOnACommitTarget_ReturnsCommit()
        {
            // GIVEN
            var releasePosition = new Vector2(1f, 0f);

            // WHEN
            PointerGesture gesture = GestureClassifier.ClassifyRelease(_origin, releasePosition, ZeroThresholdInDp, isOverCommitTarget: true);

            // THEN
            Assert.That(gesture, Is.EqualTo(PointerGesture.Commit));
        }

        [Test]
        public void ClassifyRelease_PointerTravelledAndLandedOffACommitTarget_ReturnsCancel()
        {
            // GIVEN
            var releasePosition = new Vector2(1f, 0f);

            // WHEN
            PointerGesture gesture = GestureClassifier.ClassifyRelease(_origin, releasePosition, ZeroThresholdInDp, isOverCommitTarget: false);

            // THEN
            Assert.That(gesture, Is.EqualTo(PointerGesture.Cancel));
        }

        [Test]
        public void HasLeftThreshold_DistanceExactlyAtTheThreshold_ReturnsFalse()
        {
            // GIVEN — reportedDpi equal to FallbackScreenDpi makes the dp-to-pixel ratio exactly one, so the
            // threshold in pixels is the authored 10 dp with no conversion left to distrust.
            var currentPosition = new Vector2(DragThresholdInDp, 0f);

            // WHEN
            bool hasLeftThreshold = GestureClassifier.HasLeftThreshold(_origin, currentPosition, DragThresholdInDp, GestureClassifier.FallbackScreenDpi);

            // THEN — the comparison is strict greater-than, so a distance equal to the threshold has not left it.
            Assert.That(hasLeftThreshold, Is.False);
        }

        [Test]
        public void HasLeftThreshold_DistanceJustUnderTheThreshold_ReturnsFalse()
        {
            // GIVEN
            var currentPosition = new Vector2(DragThresholdInDp - 0.01f, 0f);

            // WHEN
            bool hasLeftThreshold = GestureClassifier.HasLeftThreshold(_origin, currentPosition, DragThresholdInDp, GestureClassifier.FallbackScreenDpi);

            // THEN
            Assert.That(hasLeftThreshold, Is.False);
        }

        [Test]
        public void HasLeftThreshold_DistanceJustOverTheThreshold_ReturnsTrue()
        {
            // GIVEN
            var currentPosition = new Vector2(DragThresholdInDp + 0.01f, 0f);

            // WHEN
            bool hasLeftThreshold = GestureClassifier.HasLeftThreshold(_origin, currentPosition, DragThresholdInDp, GestureClassifier.FallbackScreenDpi);

            // THEN
            Assert.That(hasLeftThreshold, Is.True);
        }

        [Test]
        public void ConvertDpToPixels_ReportedDpiIsZero_SubstitutesTheFallbackDpiInsteadOfDividingByZero()
        {
            // GIVEN — some devices report zero when they cannot answer Screen.dpi. FallbackScreenDpi makes the
            // substituted ratio exactly one, so the authored distance passes through unchanged.
            const float distanceInDp = 80f;

            // WHEN
            float pixels = GestureClassifier.ConvertDpToPixels(distanceInDp, 0f);

            // THEN
            Assert.That(pixels, Is.EqualTo(distanceInDp).Within(0.0001f));
        }
    }
}
