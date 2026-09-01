using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class LocalSeatResolverTests
    {
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        [Test]
        public void TryResolve_PlayerOneIsLocalHuman_ReturnsTrueWithPlayerOneAsHomeAndPlayerTwoAsAway()
        {
            // GIVEN
            var config = new MatchConfiguration(
                0,
                new PlayerSlot(PlayerOneId, PlayerControl.LocalHuman),
                new PlayerSlot(PlayerTwoId, PlayerControl.Machine),
                0f,
                0f,
                0f
            );

            // WHEN
            bool wasResolved = LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out PlayerSlot away);

            // THEN
            Assert.That((wasResolved, home.Id, away.Id), Is.EqualTo((true, PlayerOneId, PlayerTwoId)));
        }

        [Test]
        public void TryResolve_PlayerTwoIsLocalHuman_ReturnsTrueWithPlayerTwoAsHomeAndPlayerOneAsAway()
        {
            // GIVEN
            var config = new MatchConfiguration(
                0,
                new PlayerSlot(PlayerOneId, PlayerControl.Machine),
                new PlayerSlot(PlayerTwoId, PlayerControl.LocalHuman),
                0f,
                0f,
                0f
            );

            // WHEN
            bool wasResolved = LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out PlayerSlot away);

            // THEN — the seat order is swapped: player two comes home and player one becomes the opponent.
            Assert.That((wasResolved, home.Id, away.Id), Is.EqualTo((true, PlayerTwoId, PlayerOneId)));
        }

        [Test]
        public void TryResolve_NeitherSeatIsLocalHuman_ReturnsFalseButStillFillsBothOutputs()
        {
            // GIVEN — a machine-versus-machine debug match, where neither seat is driven locally.
            var config = new MatchConfiguration(
                0,
                new PlayerSlot(PlayerOneId, PlayerControl.Machine),
                new PlayerSlot(PlayerTwoId, PlayerControl.RemoteHuman),
                0f,
                0f,
                0f
            );

            // WHEN
            bool wasResolved = LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out PlayerSlot away);

            // THEN — the fallback pairing is still usable without branching on the result.
            Assert.That((wasResolved, home.Id, away.Id), Is.EqualTo((false, PlayerOneId, PlayerTwoId)));
        }

        [Test]
        public void TryResolve_UnassignedIds_SubstitutesFallbackIdsWhileKeepingEachSeatsAuthoredControl()
        {
            // GIVEN
            var config = new MatchConfiguration(
                0,
                new PlayerSlot(PlayerSlot.UnassignedId, PlayerControl.LocalHuman),
                new PlayerSlot(PlayerSlot.UnassignedId, PlayerControl.Machine),
                0f,
                0f,
                0f
            );

            // WHEN
            LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out PlayerSlot away);

            // THEN
            Assert.That(
                (home.Id, home.Control, away.Id, away.Control),
                Is.EqualTo((LocalSeatResolver.FallbackHomePlayerId, PlayerControl.LocalHuman, LocalSeatResolver.FallbackAwayPlayerId, PlayerControl.Machine))
            );
        }
    }
}
