using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class PlayerSlotTests
    {
        private const int FirstPlayerId = 1;

        [Test]
        public void DefaultConstruction_Always_LeavesTheIdUnassigned()
        {
            // GIVEN

            // WHEN
            var slot = default(PlayerSlot);

            // THEN
            Assert.That(slot.Id, Is.EqualTo(PlayerSlot.UnassignedId));
        }

        [Test]
        public void DefaultConstruction_Always_LeavesTheControlUnassigned()
        {
            // GIVEN

            // WHEN
            var slot = default(PlayerSlot);

            // THEN — a zero-valued control member would make every unfilled seat read as that kind, which is why
            // PlayerControl.Unassigned holds zero and nothing may ever be renumbered onto it.
            Assert.That(slot.Control, Is.EqualTo(PlayerControl.Unassigned));
        }

        [Test]
        public void UnassignedId_Always_SitsBelowTheFirstRealPlayerId()
        {
            // THEN — real player ids start at one throughout this project, so a zero id can never be mistaken
            // for a player numbered zero.
            Assert.That(PlayerSlot.UnassignedId, Is.LessThan(FirstPlayerId));
        }

        [TestCase(PlayerControl.LocalHuman)]
        [TestCase(PlayerControl.RemoteHuman)]
        [TestCase(PlayerControl.Machine)]
        public void Constructor_WithAnIdAndAControl_RoundTripsBoth(PlayerControl control)
        {
            // GIVEN

            // WHEN
            var slot = new PlayerSlot(FirstPlayerId, control);

            // THEN
            Assert.That((slot.Id, slot.Control), Is.EqualTo((FirstPlayerId, control)));
        }

        [Test]
        public void Constructor_WithTheUnassignedId_LeavesTheSeatUnfilled()
        {
            // GIVEN

            // WHEN
            var slot = new PlayerSlot(PlayerSlot.UnassignedId, PlayerControl.Unassigned);

            // THEN — the type's own remarks define an unfilled seat as both halves, so both are asserted.
            Assert.That((slot.Id, slot.Control), Is.EqualTo((PlayerSlot.UnassignedId, PlayerControl.Unassigned)));
        }
    }
}
