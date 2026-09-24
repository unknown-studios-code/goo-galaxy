using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Input.Services;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Input
{
    [TestFixture]
    public class SelectionTargetResolverTests
    {
        private const int OwnUnitId = 1;
        private const int OtherUnitId = 2;
        private const int OwnSlotIndex = 0;
        private const int OtherSlotIndex = 1;

        private static readonly HexCoordinates _unitHex = new(0, 0);
        private static readonly HexCoordinates _target = new(1, 0);
        private static readonly HexCoordinates _otherTarget = new(0, 1);
        private static readonly HexCoordinates _unreachedTarget = new(-1, 1);

        private readonly List<HexCoordinates> _cluster = new() { _target };
        private readonly List<HexCoordinates> _targets = new();

        [TestCaseSource(nameof(EverySourceKindBuilder))]
        public void IsOptionForSource_ProtocolOption_ReturnsFalseRegardlessOfSourceKind(Func<InteractionSource> buildSource)
        {
            // GIVEN
            var option = MoveOption.ForProtocol(OwnSlotIndex, CardId.Empty, _cluster);
            InteractionSource source = buildSource();

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.False);
        }

        [Test]
        public void IsOptionForSource_BoardUnitSourceWithMatchingUnitId_ReturnsTrue()
        {
            // GIVEN
            var option = MoveOption.ForClone(OwnUnitId, _unitHex, _target);
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.True);
        }

        [Test]
        public void IsOptionForSource_BoardUnitSourceWithDifferentUnitId_ReturnsFalse()
        {
            // GIVEN
            var option = MoveOption.ForClone(OtherUnitId, _unitHex, _target);
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.False);
        }

        [Test]
        public void IsOptionForSource_HandSlotSourceWithMatchingSlotDeploy_ReturnsTrue()
        {
            // GIVEN
            var option = MoveOption.ForDeploy(OwnSlotIndex, _target);
            var source = InteractionSource.ForHandSlot(OwnSlotIndex);

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.True);
        }

        [Test]
        public void IsOptionForSource_HandSlotSourceWithDeployFromAnotherSlot_ReturnsFalse()
        {
            // GIVEN — the bug that motivated the extraction: a Deploy played from a different slot must never be
            // mistaken for one this selection can commit.
            var option = MoveOption.ForDeploy(OtherSlotIndex, _target);
            var source = InteractionSource.ForHandSlot(OwnSlotIndex);

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.False);
        }

        [Test]
        public void IsOptionForSource_HandSlotSourceWithACloneOption_ReturnsFalse()
        {
            // GIVEN — a hand slot only ever plays a Deploy, so a Clone can never match it, whatever slot it carries.
            var option = MoveOption.ForClone(OwnUnitId, _unitHex, _target);
            var source = InteractionSource.ForHandSlot(OwnSlotIndex);

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.False);
        }

        [Test]
        public void IsOptionForSource_NoneSource_ReturnsFalseForABoardMoveOption()
        {
            // GIVEN
            var option = MoveOption.ForClone(OwnUnitId, _unitHex, _target);
            InteractionSource source = InteractionSource.None;

            // WHEN
            bool isForSource = SelectionTargetResolver.IsOptionForSource(in option, in source);

            // THEN
            Assert.That(isForSource, Is.False);
        }

        [Test]
        public void CollectTargets_BufferHoldsPreexistingEntries_ClearsThemFirst()
        {
            // GIVEN
            var options = new List<MoveOption>();
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);
            _targets.Add(_unreachedTarget);

            // WHEN
            SelectionTargetResolver.CollectTargets(options, in source, _targets);

            // THEN
            Assert.That(_targets, Is.Empty);
        }

        [Test]
        public void CollectTargets_MixOfMatchingAndNonMatchingOptions_CollectsOnlyTheMatchingTargets()
        {
            // GIVEN
            var options = new List<MoveOption>
            {
                MoveOption.ForClone(OwnUnitId, _unitHex, _target),
                MoveOption.ForJump(OwnUnitId, _unitHex, _otherTarget),
                MoveOption.ForClone(OtherUnitId, _unitHex, _unreachedTarget),
            };
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);

            // WHEN
            SelectionTargetResolver.CollectTargets(options, in source, _targets);

            // THEN
            Assert.That(_targets, Is.EquivalentTo(new[] { _target, _otherTarget }));
        }

        [Test]
        public void TryFindOptionForTarget_CloneAndJumpBothReachTheTarget_ReturnsTheCloneOption()
        {
            // GIVEN — the documented tie-break: the enumerator adds Clone options ahead of Jump options, so the
            // first match for a shared target is the Clone.
            var options = new List<MoveOption> { MoveOption.ForClone(OwnUnitId, _unitHex, _target), MoveOption.ForJump(OwnUnitId, _unitHex, _target) };
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);

            // WHEN
            bool wasFound = SelectionTargetResolver.TryFindOptionForTarget(options, in source, _target, out MoveOption option);

            // THEN
            Assert.That((wasFound, option.MoveType), Is.EqualTo((true, MoveType.Clone)));
        }

        [Test]
        public void TryFindOptionForTarget_NoOptionReachesTheTarget_ReturnsFalse()
        {
            // GIVEN
            var options = new List<MoveOption> { MoveOption.ForClone(OwnUnitId, _unitHex, _target) };
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);

            // WHEN
            bool wasFound = SelectionTargetResolver.TryFindOptionForTarget(options, in source, _unreachedTarget, out MoveOption option);

            // THEN
            Assert.That((wasFound, option), Is.EqualTo((false, default(MoveOption))));
        }

        [Test]
        public void TryFindOptionForTarget_TargetReachedOnlyByAnotherSourcesOption_ReturnsFalse()
        {
            // GIVEN
            var options = new List<MoveOption> { MoveOption.ForClone(OtherUnitId, _unitHex, _target) };
            var source = InteractionSource.ForBoardUnit(OwnUnitId, _unitHex);

            // WHEN
            bool wasFound = SelectionTargetResolver.TryFindOptionForTarget(options, in source, _target, out MoveOption option);

            // THEN
            Assert.That((wasFound, option), Is.EqualTo((false, default(MoveOption))));
        }

        private static IEnumerable<TestCaseData> EverySourceKindBuilder()
        {
            yield return new TestCaseData((Func<InteractionSource>)(() => InteractionSource.ForBoardUnit(OwnUnitId, _unitHex))).SetName("BoardUnit");
            yield return new TestCaseData((Func<InteractionSource>)(() => InteractionSource.ForHandSlot(OwnSlotIndex))).SetName("HandSlot");
            yield return new TestCaseData((Func<InteractionSource>)(() => InteractionSource.None)).SetName("None");
        }
    }
}
