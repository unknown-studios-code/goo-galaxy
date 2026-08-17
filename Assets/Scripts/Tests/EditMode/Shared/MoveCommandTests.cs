using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Shared
{
    [TestFixture]
    public class MoveCommandTests
    {
        private const int PlayerId = 1;
        private const int UnitId = 5;

        private static readonly HexCoordinates _source = new(0, 0);
        private static readonly HexCoordinates _target = new(2, 0);

        [Test]
        public void Equals_IdenticalArguments_ReturnsTrue()
        {
            // GIVEN
            var first = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);
            var second = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);

            // WHEN
            bool result = first.Equals(second);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void GetHashCode_IdenticalArguments_ReturnsSameHash()
        {
            // GIVEN
            var first = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);
            var second = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);

            // WHEN
            int firstHash = first.GetHashCode();
            int secondHash = second.GetHashCode();

            // THEN
            Assert.That(secondHash, Is.EqualTo(firstHash));
        }

        [TestCase(MoveType.Clone, 0, 0, 2, 0, 1, 5, "Type")]
        [TestCase(MoveType.Jump, 1, 0, 2, 0, 1, 5, "Source")]
        [TestCase(MoveType.Jump, 0, 0, 3, 0, 1, 5, "Target")]
        [TestCase(MoveType.Jump, 0, 0, 2, 0, 2, 5, "PlayerId")]
        [TestCase(MoveType.Jump, 0, 0, 2, 0, 1, 6, "UnitId")]
        public void Equals_OneMemberDiffers_ReturnsFalse(
            MoveType type,
            int sourceQ,
            int sourceR,
            int targetQ,
            int targetR,
            int playerId,
            int unitId,
            string changedMember
        )
        {
            // GIVEN
            var baseline = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);
            var other = new MoveCommand(type, new HexCoordinates(sourceQ, sourceR), new HexCoordinates(targetQ, targetR), playerId, unitId);

            // WHEN
            bool result = baseline.Equals(other);

            // THEN
            Assert.That(result, Is.False, $"Expected a differing {changedMember} to break equality.");
        }

        [Test]
        public void EqualityOperator_EqualCommands_AgreesWithEquals()
        {
            // GIVEN
            var first = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);
            var second = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);

            // WHEN
            bool operatorResult = first == second;
            bool equalsResult = first.Equals(second);

            // THEN
            Assert.That(operatorResult, Is.EqualTo(equalsResult));
        }

        [Test]
        public void InequalityOperator_DifferentCommands_AgreesWithEquals()
        {
            // GIVEN
            var first = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);
            var second = new MoveCommand(MoveType.Clone, _source, _target, PlayerId, UnitId);

            // WHEN
            bool operatorResult = first != second;
            bool equalsResult = !first.Equals(second);

            // THEN
            Assert.That(operatorResult, Is.EqualTo(equalsResult));
        }

        [Test]
        public void Equals_BoxedObject_WrongType_ReturnsFalse()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);

            // WHEN
            bool result = command.Equals("not a MoveCommand");

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void Equals_Null_ReturnsFalse()
        {
            // GIVEN
            var command = new MoveCommand(MoveType.Jump, _source, _target, PlayerId, UnitId);

            // WHEN
            bool result = command.Equals(null);

            // THEN
            Assert.That(result, Is.False);
        }

        [Test]
        public void ForDeploy_SameArguments_ReturnsEqualCommands()
        {
            // GIVEN
            var first = MoveCommand.ForDeploy(_target, PlayerId);
            var second = MoveCommand.ForDeploy(_target, PlayerId);

            // WHEN
            bool result = first.Equals(second);

            // THEN
            Assert.That(result, Is.True);
        }

        [Test]
        public void ForDeploy_Always_YieldsSourceEqualToTargetAndNoSourceUnit()
        {
            // GIVEN
            var expected = new MoveCommand(MoveType.Deploy, _target, _target, PlayerId, MoveCommand.NoUnit);

            // WHEN
            var command = MoveCommand.ForDeploy(_target, PlayerId);

            // THEN
            Assert.That(command, Is.EqualTo(expected));
        }

        [Test]
        public void NoUnit_EqualsHexCellNoOccupant()
        {
            // THEN — "no unit here" and "no unit acted" must read as the same sentinel, mirroring
            // AbilityContextTests.NoActingUnit_EqualsHexCellNoOccupant.
            Assert.That(MoveCommand.NoUnit, Is.EqualTo(HexCell.NoOccupant));
        }
    }
}
