using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Board
{
    [TestFixture]
    public class AbilityContextTests
    {
        private const int ActingPlayerId = 1;
        private const int ActingUnitId = 1;

        private static readonly HexCoordinates _landingHex = new(0, 0);
        private static readonly HexCoordinates _secondTargetHex = new(1, 0);

        [Test]
        public void ForLanding_WithActingUnitId_HasActingUnitIsTrue()
        {
            // GIVEN

            // WHEN
            var context = AbilityContext.ForLanding(ActingPlayerId, ActingUnitId, _landingHex, false, default, default);

            // THEN
            Assert.That(context.HasActingUnit, Is.True);
        }

        [Test]
        public void ForLanding_WithNoActingUnitId_HasActingUnitIsFalse()
        {
            // GIVEN

            // WHEN — the shape AbilityController builds when a Jump's target hex turned out empty.
            var context = AbilityContext.ForLanding(ActingPlayerId, AbilityContext.NoActingUnit, _landingHex, false, default, default);

            // THEN
            Assert.That(context.HasActingUnit, Is.False);
        }

        [Test]
        public void ForLanding_HasExplicitTargetsIsFalse()
        {
            // GIVEN

            // WHEN — a troop derives its impact area from the landing hex, it is never handed one.
            var context = AbilityContext.ForLanding(ActingPlayerId, ActingUnitId, _landingHex, false, default, default);

            // THEN
            Assert.That(context.HasExplicitTargets, Is.False);
        }

        [Test]
        public void ForSpell_HasActingUnitIsFalse()
        {
            // GIVEN

            // WHEN — a Protocol puts no unit on the board, regardless of the targets it is given.
            var context = AbilityContext.ForSpell(ActingPlayerId, new List<HexCoordinates> { _landingHex });

            // THEN
            Assert.That(context.HasActingUnit, Is.False);
        }

        [Test]
        public void ForSpell_WithTargetHexes_HasExplicitTargetsIsTrue()
        {
            // GIVEN

            // WHEN
            var context = AbilityContext.ForSpell(ActingPlayerId, new List<HexCoordinates> { _landingHex });

            // THEN
            Assert.That(context.HasExplicitTargets, Is.True);
        }

        [Test]
        public void ForSpell_WithEmptyTargetHexes_HasExplicitTargetsIsTrue()
        {
            // GIVEN

            // WHEN — regression: HasExplicitTargets is stored by the factory, never inferred from list
            // content. An empty list used to read as "derive the area", turning a targetless spell into an AoE
            // centred on (0, 0). It must still read as "the area was handed over", even though that area is empty,
            // so GatherArea takes the spell branch and resolves nothing rather than falling back to a radius.
            var context = AbilityContext.ForSpell(ActingPlayerId, new List<HexCoordinates>());

            // THEN
            Assert.That(context.HasExplicitTargets, Is.True);
        }

        [Test]
        public void ForSpell_WithNullTargetHexes_HasExplicitTargetsIsTrue()
        {
            // GIVEN

            // WHEN — regression: same as the empty-list case, a null target list must not make this read
            // as a troop landing. See ForSpell_WithEmptyTargetHexes_HasExplicitTargetsIsTrue for the full rationale.
            var context = AbilityContext.ForSpell(ActingPlayerId, null);

            // THEN
            Assert.That(context.HasExplicitTargets, Is.True);
        }

        [Test]
        public void ForSpell_WithTargetHexes_SetsOriginHexToTheFirstTarget()
        {
            // GIVEN

            // WHEN — the centre every impact's radius is measured against is targets[0], not a derived midpoint.
            var context = AbilityContext.ForSpell(ActingPlayerId, new List<HexCoordinates> { _secondTargetHex, _landingHex });

            // THEN
            Assert.That(context.OriginHex, Is.EqualTo(_secondTargetHex));
        }

        [Test]
        public void ForSpell_WithNullTargetHexes_OriginHexIsDefault()
        {
            // GIVEN

            // WHEN
            var context = AbilityContext.ForSpell(ActingPlayerId, null);

            // THEN
            Assert.That(context.OriginHex, Is.EqualTo(default(HexCoordinates)));
        }

        [Test]
        public void NoActingUnit_EqualsHexCellNoOccupant()
        {
            // GIVEN

            // WHEN — "no unit here" and "no unit acting" must read as the same sentinel.

            // THEN
            Assert.That(AbilityContext.NoActingUnit, Is.EqualTo(HexCell.NoOccupant));
        }
    }
}
