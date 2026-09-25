using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Input.Models
{
    /// <summary>
    /// The one selection a player can have live at a time: which phase it is in, and what it was started from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It decides nothing about the board.</b> Which hexes are legal, whether a card is affordable and
    /// whether a commit succeeds are all answered elsewhere; this type only refuses a transition that does not
    /// follow from the phase it is in, so a gesture can never leave a selection half-built. Every
    /// <c>Try*</c> method leaves both the state and the source exactly as they were when it refuses, which is
    /// what lets a caller attempt a transition without first asking whether it is legal.
    /// </para>
    /// <para>
    /// <b><see cref="Cancel" /> is the only way back to <see cref="InteractionState.Idle" />.</b> One method,
    /// reached from every abandonment the input layer recognises — a release off the grid, a second tap on the
    /// same source, a phase change, the end of the match, the aimed card leaving the hand — so no path can clear
    /// the state while leaving the source behind for the next selection to inherit. That includes
    /// <see cref="InteractionState.SpellTargeting" />, which has no exit of its own to Idle.
    /// </para>
    /// <para>
    /// <b>Engine-free.</b> No <c>UnityEngine</c> type appears in the signature or the body, so a fixture drives
    /// the whole machine in EditMode. Allocation-free on every path.
    /// </para>
    /// </remarks>
    public sealed class InteractionStateMachine
    {
        /// <summary>The phase the live selection is in, or <see cref="InteractionState.Idle" /> when there is none.</summary>
        public InteractionState State { get; private set; } = InteractionState.Idle;

        /// <summary>What the live selection was started from, or <see cref="InteractionSource.None" /> when there is none.</summary>
        public InteractionSource Source { get; private set; } = InteractionSource.None;

        /// <summary>Starts a selection from a card in hand.</summary>
        /// <remarks>Legal only from <see cref="InteractionState.Idle" />, so a live selection is cancelled first rather than replaced.</remarks>
        /// <param name="slotIndex">The zero-based hand slot that was pressed.</param>
        /// <returns>True once the selection is live; false when one already was.</returns>
        public bool TrySelectHandSlot(int slotIndex)
        {
            if (State != InteractionState.Idle)
            {
                return false;
            }

            State = InteractionState.CardSelected;
            Source = InteractionSource.ForHandSlot(slotIndex);

            return true;
        }

        /// <summary>Starts aiming a Protocol from a card in hand the player can already afford.</summary>
        /// <remarks>
        /// Legal only from <see cref="InteractionState.Idle" />, exactly like <see cref="TrySelectHandSlot" />, and
        /// the source it records is the same hand-slot source — a Protocol and a troop differ in how they are aimed,
        /// not in where they came from. A Protocol that cannot be afforded yet is selected through
        /// <see cref="TrySelectHandSlot" /> instead and promoted by <see cref="TryBeginSpellTargeting" /> later.
        /// </remarks>
        /// <param name="slotIndex">The zero-based hand slot that was pressed.</param>
        /// <returns>True once the Protocol is being aimed; false when a selection was already live.</returns>
        public bool TrySelectSpellSlot(int slotIndex)
        {
            if (State != InteractionState.Idle)
            {
                return false;
            }

            State = InteractionState.SpellTargeting;
            Source = InteractionSource.ForHandSlot(slotIndex);

            return true;
        }

        /// <summary>Starts a selection from a unit already on the board.</summary>
        /// <remarks>Legal only from <see cref="InteractionState.Idle" />, so a live selection is cancelled first rather than replaced.</remarks>
        /// <param name="unitId">The unit that was tapped.</param>
        /// <param name="hex">The hex it stands on.</param>
        /// <returns>True once the selection is live; false when one already was.</returns>
        public bool TrySelectBoardUnit(int unitId, HexCoordinates hex)
        {
            if (State != InteractionState.Idle)
            {
                return false;
            }

            State = InteractionState.UnitSelected;
            Source = InteractionSource.ForBoardUnit(unitId, hex);

            return true;
        }

        /// <summary>Reports that the pointer has travelled far enough to be a drag rather than a tap.</summary>
        /// <remarks>Legal from either selected phase, and idempotent in every other phase, which is what lets a caller test the threshold on every pointer move.</remarks>
        /// <returns>True on the move that promoted the selection to a drag; false on every other.</returns>
        public bool TryBeginDrag()
        {
            if (State is not (InteractionState.CardSelected or InteractionState.UnitSelected))
            {
                return false;
            }

            State = InteractionState.Dragging;

            return true;
        }

        /// <summary>Reports that the dragged pointer is now over a hex the selection could commit onto.</summary>
        /// <returns>True on the move that entered the preview; false when the selection is not dragging, or was already previewing.</returns>
        public bool TryBeginPreview()
        {
            if (State != InteractionState.Dragging)
            {
                return false;
            }

            State = InteractionState.Previewing;

            return true;
        }

        /// <summary>Reports that the dragged pointer has left the hex it was previewing.</summary>
        /// <returns>True on the move that left the preview; false when the selection was not previewing.</returns>
        public bool TryEndPreview()
        {
            if (State != InteractionState.Previewing)
            {
                return false;
            }

            State = InteractionState.Dragging;

            return true;
        }

        /// <summary>Promotes a hand selection that was waiting on its cost into aiming a Protocol.</summary>
        /// <remarks>
        /// Legal from <see cref="InteractionState.CardSelected" />, <see cref="InteractionState.Dragging" /> and
        /// <see cref="InteractionState.Previewing" /> while the source is a hand slot — the three phases an
        /// unaffordable card can be waiting in — and keeps that source. A board-unit selection is never promoted.
        /// Whether the card is a Protocol is the caller's to know; this type does not read cards.
        /// </remarks>
        /// <returns>True on the call that entered <see cref="InteractionState.SpellTargeting" />; false otherwise.</returns>
        public bool TryBeginSpellTargeting()
        {
            if (Source.Kind != InteractionSourceKind.HandSlot)
            {
                return false;
            }

            if (State is not (InteractionState.CardSelected or InteractionState.Dragging or InteractionState.Previewing))
            {
                return false;
            }

            State = InteractionState.SpellTargeting;

            return true;
        }

        /// <summary>Returns a Protocol that can no longer be afforded to waiting, keeping it selected.</summary>
        /// <remarks>
        /// Always lands in <see cref="InteractionState.CardSelected" />, even mid-drag. Whether a pointer still down
        /// then promotes it to <see cref="InteractionState.Dragging" /> through <see cref="TryBeginDrag" /> is the
        /// caller's to decide: a drag out of the hand does, a press held on the board must not.
        /// </remarks>
        /// <returns>True on the call that left <see cref="InteractionState.SpellTargeting" />; false when it was not aiming.</returns>
        public bool TryEndSpellTargeting()
        {
            if (State != InteractionState.SpellTargeting)
            {
                return false;
            }

            State = InteractionState.CardSelected;

            return true;
        }

        /// <summary>Abandons the live selection, returning to <see cref="InteractionState.Idle" /> and clearing the source.</summary>
        /// <remarks>Safe to call when nothing is selected, which is what lets every abandonment path call it unconditionally.</remarks>
        public void Cancel()
        {
            State = InteractionState.Idle;
            Source = InteractionSource.None;
        }
    }
}
