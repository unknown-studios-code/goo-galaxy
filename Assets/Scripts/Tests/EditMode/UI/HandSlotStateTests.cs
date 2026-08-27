using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.UI
{
    // Resolving an unfilled slot from an unresolved CardId is MatchHudPresenter.BuildSlotState's behavior, not
    // this struct's — the struct never looks a card up, it only carries whatever it is constructed with. That
    // case is covered in MatchHudPresenterTests against the presenter's public event surface instead.
    [TestFixture]
    public class HandSlotStateTests
    {
        [Test]
        public void IsFilled_EmptyState_ReturnsFalse()
        {
            // GIVEN

            // WHEN / THEN
            Assert.That(HandSlotState.Empty.IsFilled, Is.False);
        }

        [Test]
        public void IsFilled_StateWithARealKind_ReturnsTrue()
        {
            // GIVEN
            var state = new HandSlotState(new CardId("subject_alpha"), "Subject Alpha", 2, HandSlotKind.Specimen, CardAccent.None);

            // WHEN / THEN
            Assert.That(state.IsFilled, Is.True);
        }

        [Test]
        public void Constructor_GivenValues_PreservesEveryField()
        {
            // GIVEN
            var cardId = new CardId("subject_alpha");

            // WHEN
            var state = new HandSlotState(cardId, "Subject Alpha", 2, HandSlotKind.Protocol, CardAccent.Control);

            // THEN
            Assert.That(
                (state.CardId, state.DisplayName, state.EnergyCost, state.Kind, state.Accent),
                Is.EqualTo((cardId, "Subject Alpha", 2, HandSlotKind.Protocol, CardAccent.Control))
            );
        }

        [Test]
        public void Accent_EmptyState_ReturnsNone()
        {
            // GIVEN

            // WHEN / THEN
            Assert.That(HandSlotState.Empty.Accent, Is.EqualTo(CardAccent.None));
        }
    }
}
