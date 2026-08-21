using System.Collections.Generic;
using GooGalaxy.Runtime.AI.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.AI
{
    [TestFixture]
    public class MoveOptionTests
    {
        private const int ActingPlayerId = 1;
        private const int ActingUnitId = 42;
        private const int SlotIndex = 2;

        private static readonly HexCoordinates _source = new(0, 0);
        private static readonly HexCoordinates _target = new(1, 0);

        [Test]
        public void ForDeploy_Always_ReportsTheBoardMoveKind()
        {
            // GIVEN

            // WHEN
            var option = MoveOption.ForDeploy(SlotIndex, _target);

            // THEN
            Assert.That(option.Kind, Is.EqualTo(MoveOptionKind.BoardMove));
        }

        [Test]
        public void ForClone_Always_CarriesNoHandSlot()
        {
            // GIVEN

            // WHEN
            var option = MoveOption.ForClone(ActingUnitId, _source, _target);

            // THEN
            Assert.That(option.SlotIndex, Is.EqualTo(MoveOption.NoSlot));
        }

        [Test]
        public void ForJump_Always_CarriesNoHandSlot()
        {
            // GIVEN

            // WHEN
            var option = MoveOption.ForJump(ActingUnitId, _source, _target);

            // THEN
            Assert.That(option.SlotIndex, Is.EqualTo(MoveOption.NoSlot));
        }

        [Test]
        public void ForProtocol_Always_CentresTheOptionOnTheFirstHexOfTheCluster()
        {
            // GIVEN
            var cluster = new List<HexCoordinates> { _source, _target };

            // WHEN
            var option = MoveOption.ForProtocol(SlotIndex, new CardId("cryo_stasis"), cluster);

            // THEN
            Assert.That(option.Target, Is.EqualTo(_source));
        }

        [Test]
        public void ForProtocol_Always_ReportsTheProtocolKind()
        {
            // GIVEN
            var cluster = new List<HexCoordinates> { _source, _target };

            // WHEN
            var option = MoveOption.ForProtocol(SlotIndex, new CardId("cryo_stasis"), cluster);

            // THEN
            Assert.That(option.Kind, Is.EqualTo(MoveOptionKind.Protocol));
        }

        [Test]
        public void ToMoveCommand_ForADeploy_CarriesNoUnit()
        {
            // GIVEN
            var option = MoveOption.ForDeploy(SlotIndex, _target);

            // WHEN
            var command = option.ToMoveCommand(ActingPlayerId);

            // THEN
            Assert.That(command.UnitId, Is.EqualTo(MoveCommand.NoUnit));
        }

        [Test]
        public void ToMoveCommand_ForADeploy_CarriesASourceEqualToItsTarget()
        {
            // GIVEN
            var option = MoveOption.ForDeploy(SlotIndex, _target);

            // WHEN
            var command = option.ToMoveCommand(ActingPlayerId);

            // THEN — a Deploy acts with no source unit, so the placement hex stands in for both ends.
            Assert.That(command.Source, Is.EqualTo(command.Target));
        }

        [Test]
        public void ToMoveCommand_ForADeploy_CarriesTheDeployMoveType()
        {
            // GIVEN
            var option = MoveOption.ForDeploy(SlotIndex, _target);

            // WHEN
            var command = option.ToMoveCommand(ActingPlayerId);

            // THEN
            Assert.That(command.Type, Is.EqualTo(MoveType.Deploy));
        }

        [Test]
        public void ToMoveCommand_ForAClone_RoundTripsSourceTargetAndUnitId()
        {
            // GIVEN
            var option = MoveOption.ForClone(ActingUnitId, _source, _target);

            // WHEN
            var command = option.ToMoveCommand(ActingPlayerId);

            // THEN
            Assert.That((command.Type, command.Source, command.Target, command.UnitId), Is.EqualTo((MoveType.Clone, _source, _target, ActingUnitId)));
        }

        [Test]
        public void ToMoveCommand_ForAJump_RoundTripsSourceTargetAndUnitId()
        {
            // GIVEN
            var option = MoveOption.ForJump(ActingUnitId, _source, _target);

            // WHEN
            var command = option.ToMoveCommand(ActingPlayerId);

            // THEN
            Assert.That((command.Type, command.Source, command.Target, command.UnitId), Is.EqualTo((MoveType.Jump, _source, _target, ActingUnitId)));
        }

        [Test]
        public void ToMoveCommand_ForAnyOption_CarriesTheActingPlayer()
        {
            // GIVEN
            var option = MoveOption.ForJump(ActingUnitId, _source, _target);

            // WHEN
            var command = option.ToMoveCommand(ActingPlayerId);

            // THEN
            Assert.That(command.PlayerId, Is.EqualTo(ActingPlayerId));
        }

        [Test]
        public void ToSpellCommand_ForAProtocol_BorrowsTheSameClusterInstance()
        {
            // GIVEN — the ownership contract: the cluster is the caller buffer, never a copy, which is what
            // keeps building an option allocation-free and what makes retaining one past its tick unsafe.
            var cluster = new List<HexCoordinates> { _source, _target };
            var option = MoveOption.ForProtocol(SlotIndex, new CardId("cryo_stasis"), cluster);

            // WHEN
            var command = option.ToSpellCommand(ActingPlayerId);

            // THEN
            Assert.That(command.TargetHexes, Is.SameAs(cluster));
        }

        [Test]
        public void ToSpellCommand_ForAProtocol_CarriesTheCardBeingPlayed()
        {
            // GIVEN
            var cardId = new CardId("cryo_stasis");
            var cluster = new List<HexCoordinates> { _source, _target };
            var option = MoveOption.ForProtocol(SlotIndex, cardId, cluster);

            // WHEN
            var command = option.ToSpellCommand(ActingPlayerId);

            // THEN
            Assert.That(command.CardId, Is.EqualTo(cardId));
        }
    }
}
