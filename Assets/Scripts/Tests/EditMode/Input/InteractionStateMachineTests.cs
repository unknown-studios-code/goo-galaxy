using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Shared.Types;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Input
{
    [TestFixture]
    public class InteractionStateMachineTests
    {
        private const int SlotIndex = 2;
        private const int UnitId = 7;

        private static readonly HexCoordinates _unitHex = new(1, -1);

        [Test]
        public void TrySelectHandSlot_FromIdle_EntersCardSelectedWithTheHandSlotSource()
        {
            // GIVEN
            var machine = new InteractionStateMachine();

            // WHEN
            bool wasSelected = machine.TrySelectHandSlot(SlotIndex);

            // THEN
            Assert.That(
                (wasSelected, machine.State, machine.Source),
                Is.EqualTo((true, InteractionState.CardSelected, InteractionSource.ForHandSlot(SlotIndex)))
            );
        }

        [TestCaseSource(nameof(NonIdleStateBuilders))]
        public void TrySelectHandSlot_FromANonIdleState_ReturnsFalseAndLeavesStateAndSourceUnchanged(Func<InteractionStateMachine> buildMachine)
        {
            // GIVEN
            InteractionStateMachine machine = buildMachine();
            InteractionState stateBeforeAttempt = machine.State;
            InteractionSource sourceBeforeAttempt = machine.Source;

            // WHEN
            bool wasSelected = machine.TrySelectHandSlot(SlotIndex);

            // THEN
            Assert.That((wasSelected, machine.State, machine.Source), Is.EqualTo((false, stateBeforeAttempt, sourceBeforeAttempt)));
        }

        [Test]
        public void TrySelectBoardUnit_FromIdle_EntersUnitSelectedWithTheBoardUnitSource()
        {
            // GIVEN
            var machine = new InteractionStateMachine();

            // WHEN
            bool wasSelected = machine.TrySelectBoardUnit(UnitId, _unitHex);

            // THEN
            Assert.That(
                (wasSelected, machine.State, machine.Source),
                Is.EqualTo((true, InteractionState.UnitSelected, InteractionSource.ForBoardUnit(UnitId, _unitHex)))
            );
        }

        [TestCaseSource(nameof(NonIdleStateBuilders))]
        public void TrySelectBoardUnit_FromANonIdleState_ReturnsFalseAndLeavesStateAndSourceUnchanged(Func<InteractionStateMachine> buildMachine)
        {
            // GIVEN
            InteractionStateMachine machine = buildMachine();
            InteractionState stateBeforeAttempt = machine.State;
            InteractionSource sourceBeforeAttempt = machine.Source;

            // WHEN
            bool wasSelected = machine.TrySelectBoardUnit(UnitId, _unitHex);

            // THEN
            Assert.That((wasSelected, machine.State, machine.Source), Is.EqualTo((false, stateBeforeAttempt, sourceBeforeAttempt)));
        }

        [Test]
        public void TryBeginDrag_FromCardSelected_EntersDragging()
        {
            // GIVEN
            InteractionStateMachine machine = BuildCardSelected();

            // WHEN
            bool didBeginDrag = machine.TryBeginDrag();

            // THEN
            Assert.That((didBeginDrag, machine.State), Is.EqualTo((true, InteractionState.Dragging)));
        }

        [Test]
        public void TryBeginDrag_FromUnitSelected_EntersDragging()
        {
            // GIVEN
            InteractionStateMachine machine = BuildUnitSelected();

            // WHEN
            bool didBeginDrag = machine.TryBeginDrag();

            // THEN
            Assert.That((didBeginDrag, machine.State), Is.EqualTo((true, InteractionState.Dragging)));
        }

        [TestCaseSource(nameof(IllegalBeginDragStateBuilders))]
        public void TryBeginDrag_FromAnIllegalState_ReturnsFalseAndLeavesStateAndSourceUnchanged(Func<InteractionStateMachine> buildMachine)
        {
            // GIVEN
            InteractionStateMachine machine = buildMachine();
            InteractionState stateBeforeAttempt = machine.State;
            InteractionSource sourceBeforeAttempt = machine.Source;

            // WHEN
            bool didBeginDrag = machine.TryBeginDrag();

            // THEN
            Assert.That((didBeginDrag, machine.State, machine.Source), Is.EqualTo((false, stateBeforeAttempt, sourceBeforeAttempt)));
        }

        [Test]
        public void TryBeginPreview_FromDragging_EntersPreviewing()
        {
            // GIVEN
            InteractionStateMachine machine = BuildDragging();

            // WHEN
            bool didBeginPreview = machine.TryBeginPreview();

            // THEN
            Assert.That((didBeginPreview, machine.State), Is.EqualTo((true, InteractionState.Previewing)));
        }

        [TestCaseSource(nameof(IllegalBeginPreviewStateBuilders))]
        public void TryBeginPreview_FromAnIllegalState_ReturnsFalseAndLeavesStateAndSourceUnchanged(Func<InteractionStateMachine> buildMachine)
        {
            // GIVEN
            InteractionStateMachine machine = buildMachine();
            InteractionState stateBeforeAttempt = machine.State;
            InteractionSource sourceBeforeAttempt = machine.Source;

            // WHEN
            bool didBeginPreview = machine.TryBeginPreview();

            // THEN
            Assert.That((didBeginPreview, machine.State, machine.Source), Is.EqualTo((false, stateBeforeAttempt, sourceBeforeAttempt)));
        }

        [Test]
        public void TryEndPreview_FromPreviewing_ReturnsToDragging()
        {
            // GIVEN
            InteractionStateMachine machine = BuildPreviewing();

            // WHEN
            bool didEndPreview = machine.TryEndPreview();

            // THEN
            Assert.That((didEndPreview, machine.State), Is.EqualTo((true, InteractionState.Dragging)));
        }

        [TestCaseSource(nameof(IllegalEndPreviewStateBuilders))]
        public void TryEndPreview_FromAnIllegalState_ReturnsFalseAndLeavesStateAndSourceUnchanged(Func<InteractionStateMachine> buildMachine)
        {
            // GIVEN
            InteractionStateMachine machine = buildMachine();
            InteractionState stateBeforeAttempt = machine.State;
            InteractionSource sourceBeforeAttempt = machine.Source;

            // WHEN
            bool didEndPreview = machine.TryEndPreview();

            // THEN
            Assert.That((didEndPreview, machine.State, machine.Source), Is.EqualTo((false, stateBeforeAttempt, sourceBeforeAttempt)));
        }

        [TestCaseSource(nameof(EveryReachableStateBuilder))]
        public void Cancel_FromEveryReachableState_ReturnsToIdleWithTheSourceCleared(Func<InteractionStateMachine> buildMachine)
        {
            // GIVEN
            InteractionStateMachine machine = buildMachine();

            // WHEN
            machine.Cancel();

            // THEN
            Assert.That((machine.State, machine.Source), Is.EqualTo((InteractionState.Idle, InteractionSource.None)));
        }

        private static InteractionStateMachine BuildCardSelected()
        {
            var machine = new InteractionStateMachine();
            machine.TrySelectHandSlot(SlotIndex);

            return machine;
        }

        private static InteractionStateMachine BuildUnitSelected()
        {
            var machine = new InteractionStateMachine();
            machine.TrySelectBoardUnit(UnitId, _unitHex);

            return machine;
        }

        private static InteractionStateMachine BuildDragging()
        {
            InteractionStateMachine machine = BuildCardSelected();
            machine.TryBeginDrag();

            return machine;
        }

        private static InteractionStateMachine BuildPreviewing()
        {
            InteractionStateMachine machine = BuildDragging();
            machine.TryBeginPreview();

            return machine;
        }

        private static IEnumerable<TestCaseData> NonIdleStateBuilders()
        {
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildCardSelected).SetName("CardSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildUnitSelected).SetName("UnitSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildDragging).SetName("Dragging");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildPreviewing).SetName("Previewing");
        }

        private static IEnumerable<TestCaseData> IllegalBeginDragStateBuilders()
        {
            yield return new TestCaseData((Func<InteractionStateMachine>)(() => new InteractionStateMachine())).SetName("Idle");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildDragging).SetName("Dragging");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildPreviewing).SetName("Previewing");
        }

        private static IEnumerable<TestCaseData> IllegalBeginPreviewStateBuilders()
        {
            yield return new TestCaseData((Func<InteractionStateMachine>)(() => new InteractionStateMachine())).SetName("Idle");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildCardSelected).SetName("CardSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildUnitSelected).SetName("UnitSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildPreviewing).SetName("Previewing");
        }

        private static IEnumerable<TestCaseData> IllegalEndPreviewStateBuilders()
        {
            yield return new TestCaseData((Func<InteractionStateMachine>)(() => new InteractionStateMachine())).SetName("Idle");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildCardSelected).SetName("CardSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildUnitSelected).SetName("UnitSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildDragging).SetName("Dragging");
        }

        private static IEnumerable<TestCaseData> EveryReachableStateBuilder()
        {
            yield return new TestCaseData((Func<InteractionStateMachine>)(() => new InteractionStateMachine())).SetName("Idle");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildCardSelected).SetName("CardSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildUnitSelected).SetName("UnitSelected");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildDragging).SetName("Dragging");
            yield return new TestCaseData((Func<InteractionStateMachine>)BuildPreviewing).SetName("Previewing");
        }
    }
}
