using System;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Input.Models
{
    /// <summary>
    /// What a live selection was started from: a card in hand, a unit already on the board, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two selection paths share one value type so the state machine and the presenter branch on
    /// <see cref="Kind" /> once rather than carrying two parallel selections that could both be live. A hand
    /// slot names the slot it was pressed in and nothing else; a board unit names its id and the hex it stands
    /// on, because the second tap that cancels a selection is recognised by that hex.
    /// </para>
    /// <para>
    /// The absent values are <see cref="MoveOption.NoSlot" /> and <see cref="MoveCommand.NoUnit" />, reused
    /// rather than redeclared, so the "no slot" and "no unit" markers a filtered option carries and the ones a
    /// selection carries read as the same numbers wherever the two are compared.
    /// </para>
    /// <para>Carries only value types, so building one allocates nothing and none of its fields box.</para>
    /// </remarks>
    public readonly struct InteractionSource : IEquatable<InteractionSource>
    {
        /// <summary>The empty selection, which is what <see cref="InteractionState.Idle" /> holds.</summary>
        public static readonly InteractionSource None = new(InteractionSourceKind.None, MoveOption.NoSlot, default, MoveCommand.NoUnit);

        private InteractionSource(InteractionSourceKind kind, int slotIndex, HexCoordinates hex, int unitId)
        {
            Kind = kind;
            SlotIndex = slotIndex;
            Hex = hex;
            UnitId = unitId;
        }

        /// <summary>Which of the two selection paths this source started.</summary>
        public InteractionSourceKind Kind { get; }

        /// <summary>The zero-based hand slot the card was pressed in, or <see cref="MoveOption.NoSlot" /> on a board unit.</summary>
        public int SlotIndex { get; }

        /// <summary>The hex the selected unit stands on. Default on a hand slot, which stands nowhere.</summary>
        public HexCoordinates Hex { get; }

        /// <summary>The selected unit, or <see cref="MoveCommand.NoUnit" /> on a hand slot.</summary>
        public int UnitId { get; }

        public static bool operator ==(InteractionSource left, InteractionSource right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InteractionSource left, InteractionSource right)
        {
            return !left.Equals(right);
        }

        /// <summary>Builds the source for a card pressed in hand.</summary>
        /// <param name="slotIndex">The zero-based hand slot that was pressed.</param>
        /// <returns>A hand-slot source whose unit id is <see cref="MoveCommand.NoUnit" />.</returns>
        public static InteractionSource ForHandSlot(int slotIndex)
        {
            return new InteractionSource(InteractionSourceKind.HandSlot, slotIndex, default, MoveCommand.NoUnit);
        }

        /// <summary>Builds the source for a unit tapped on the board.</summary>
        /// <param name="unitId">The unit that was tapped.</param>
        /// <param name="hex">The hex it stands on, which is what a second tap on it is recognised by.</param>
        /// <returns>A board-unit source whose slot index is <see cref="MoveOption.NoSlot" />.</returns>
        public static InteractionSource ForBoardUnit(int unitId, HexCoordinates hex)
        {
            return new InteractionSource(InteractionSourceKind.BoardUnit, MoveOption.NoSlot, hex, unitId);
        }

        public bool Equals(InteractionSource other)
        {
            return Kind == other.Kind && SlotIndex == other.SlotIndex && Hex == other.Hex && UnitId == other.UnitId;
        }

        public override bool Equals(object obj)
        {
            return obj is InteractionSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Kind, SlotIndex, Hex, UnitId);
        }
    }

    /// <summary>Which selection path an <see cref="InteractionSource" /> started.</summary>
    public enum InteractionSourceKind
    {
        /// <summary>Nothing is selected. What <see cref="InteractionSource.None" /> carries.</summary>
        None = 0,

        /// <summary>A card pressed in hand, which highlights every hex it could be deployed onto.</summary>
        HandSlot = 1,

        /// <summary>A unit tapped on the board, which highlights its Clone and Jump targets.</summary>
        BoardUnit = 2,
    }
}
