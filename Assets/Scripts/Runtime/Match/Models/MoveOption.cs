using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Match.Models
{
    /// <summary>
    /// One action a player could legally take right now, in the shape the entry points already accept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named for the player, not for what drives them. The machine picks one of these to move with, and the
    /// input layer highlights the board from the same set — which is what makes a tap and an AI move provably
    /// agree about what is legal.
    /// </para>
    /// <para>
    /// Four action shapes share one value type so a strategy can pick uniformly across all of them without
    /// knowing what it picked. A board move — Deploy, Clone or Jump — carries its move type, source, target and
    /// unit id; a Deploy additionally carries the hand slot it is played from, and <see cref="MoveCommand.NoUnit" />
    /// as its unit id because it acts with no source unit. A Protocol carries its hand slot, its card, and the
    /// hexes it was aimed at.
    /// </para>
    /// <para>
    /// <b>Ownership of <see cref="TargetCluster" />.</b> A Protocol's cluster is a buffer the <i>caller</i> owns —
    /// the same contract <see cref="SpellCommand.TargetHexes" /> states. This struct only borrows it, so it stays
    /// valid exactly as long as that buffer does: until the buffer is next filled, which is the following
    /// enumeration pass. Never retain a <see cref="MoveOption" /> past the tick that produced it, and never
    /// publish one on the event bus without copying the cluster first. Borrowing rather than owning is what keeps
    /// the struct allocation-free — an option that owned its cluster would allocate a list per Protocol per tick.
    /// </para>
    /// <para>
    /// Carries only value types and one borrowed reference, so building one allocates nothing and none of its
    /// fields box.
    /// </para>
    /// </remarks>
    public readonly struct MoveOption
    {
        /// <summary>
        /// The value <see cref="SlotIndex" /> carries on an action that is not played from hand — a Clone or a
        /// Jump, both of which act with a unit already on the board.
        /// </summary>
        public const int NoSlot = -1;

        private MoveOption(
            MoveOptionKind kind,
            MoveType moveType,
            HexCoordinates source,
            HexCoordinates target,
            int unitId,
            int slotIndex,
            CardId cardId,
            IReadOnlyList<HexCoordinates> targetCluster
        )
        {
            Kind = kind;
            MoveType = moveType;
            Source = source;
            Target = target;
            UnitId = unitId;
            SlotIndex = slotIndex;
            CardId = cardId;
            TargetCluster = targetCluster;
        }

        /// <summary>Which of the two submission paths this option takes.</summary>
        public MoveOptionKind Kind { get; }

        /// <summary>
        /// The board action this option performs. <see cref="MoveType.Deploy" /> on a
        /// <see cref="MoveOptionKind.Protocol" />, which is also the type the ledger prices a Protocol by.
        /// </summary>
        public MoveType MoveType { get; }

        /// <summary>
        /// The hex the acting unit stands on. Equal to <see cref="Target" /> on a Deploy and on a Protocol,
        /// neither of which has a source unit.
        /// </summary>
        public HexCoordinates Source { get; }

        /// <summary>The hex the action lands on, or the cluster centre on a <see cref="MoveOptionKind.Protocol" />.</summary>
        public HexCoordinates Target { get; }

        /// <summary>The unit being commanded, or <see cref="MoveCommand.NoUnit" /> on a Deploy and on a Protocol.</summary>
        public int UnitId { get; }

        /// <summary>The zero-based hand slot the card is played from, or <see cref="NoSlot" /> on a Clone or a Jump.</summary>
        public int SlotIndex { get; }

        /// <summary>
        /// The card being played. Only meaningful on a <see cref="MoveOptionKind.Protocol" />; a board move
        /// carries a default id, because a Deploy names its card by slot and a Clone or Jump by unit.
        /// </summary>
        public CardId CardId { get; }

        /// <summary>The hexes a Protocol was aimed at, centre first, or null on a board move.</summary>
        /// <remarks>
        /// Borrowed from the caller's buffer and only valid until that buffer is next filled — see the type
        /// remarks. Read it with an indexed <c>for</c> loop; <c>foreach</c> over the interface boxes the backing
        /// enumerator.
        /// </remarks>
        public IReadOnlyList<HexCoordinates> TargetCluster { get; }

        /// <summary>Builds the option for placing a new unit from a hand slot onto an empty hex.</summary>
        /// <param name="slotIndex">The zero-based hand slot the card is played from.</param>
        /// <param name="target">The hex the new unit lands on.</param>
        /// <returns>A Deploy option whose source equals its target and whose unit id is <see cref="MoveCommand.NoUnit" />.</returns>
        public static MoveOption ForDeploy(int slotIndex, HexCoordinates target)
        {
            return new MoveOption(MoveOptionKind.BoardMove, MoveType.Deploy, target, target, MoveCommand.NoUnit, slotIndex, CardId.Empty, null);
        }

        /// <summary>Builds the option for duplicating a unit onto a nearby hex, leaving the original in place.</summary>
        public static MoveOption ForClone(int unitId, HexCoordinates source, HexCoordinates target)
        {
            return new MoveOption(MoveOptionKind.BoardMove, MoveType.Clone, source, target, unitId, NoSlot, CardId.Empty, null);
        }

        /// <summary>Builds the option for relocating a unit to a nearby hex.</summary>
        public static MoveOption ForJump(int unitId, HexCoordinates source, HexCoordinates target)
        {
            return new MoveOption(MoveOptionKind.BoardMove, MoveType.Jump, source, target, unitId, NoSlot, CardId.Empty, null);
        }

        /// <summary>Builds the option for deploying a Protocol from a hand slot onto a cluster of hexes.</summary>
        /// <param name="slotIndex">The zero-based hand slot the card is played from.</param>
        /// <param name="cardId">The Protocol being played.</param>
        /// <param name="targetCluster">
        /// The hexes it is aimed at, centre first, holding at least the centre. Borrowed and never copied — see
        /// the type remarks for how long it stays valid.
        /// </param>
        /// <returns>A Protocol option centred on the cluster's first hex.</returns>
        public static MoveOption ForProtocol(int slotIndex, CardId cardId, IReadOnlyList<HexCoordinates> targetCluster)
        {
            HexCoordinates centre = targetCluster[0];

            return new MoveOption(MoveOptionKind.Protocol, MoveType.Deploy, centre, centre, MoveCommand.NoUnit, slotIndex, cardId, targetCluster);
        }

        /// <summary>Describes this option as the board command a move resolver accepts.</summary>
        /// <remarks>
        /// Only meaningful for <see cref="MoveOptionKind.BoardMove" />. A Protocol yields a Deploy onto its
        /// cluster centre, which is what its move type and target already say, but nothing resolves a Protocol
        /// that way — <see cref="ToSpellCommand" /> is its command.
        /// </remarks>
        /// <param name="playerId">The player acting.</param>
        /// <returns>The equivalent command.</returns>
        public MoveCommand ToMoveCommand(int playerId)
        {
            if (MoveType == MoveType.Deploy)
            {
                return MoveCommand.ForDeploy(Target, playerId);
            }

            return new MoveCommand(MoveType, Source, Target, playerId, UnitId);
        }

        /// <summary>Describes this option as the Protocol deployment request a spell resolver accepts.</summary>
        /// <remarks>
        /// Only meaningful for <see cref="MoveOptionKind.Protocol" />. A board move carries no cluster, so the
        /// command it yields borrows a null target list, which every resolver rejects.
        /// </remarks>
        /// <param name="playerId">The player acting.</param>
        /// <returns>The equivalent command, borrowing this option's cluster on the same terms.</returns>
        public SpellCommand ToSpellCommand(int playerId)
        {
            return new SpellCommand(playerId, CardId, TargetCluster);
        }
    }

    /// <summary>Which submission path a <see cref="MoveOption" /> takes.</summary>
    public enum MoveOptionKind
    {
        /// <summary>A Deploy, a Clone or a Jump: one unit onto one hex, resolved as a <see cref="MoveCommand" />.</summary>
        BoardMove = 0,

        /// <summary>A Protocol played onto a cluster of hexes, resolved as a <see cref="SpellCommand" />.</summary>
        Protocol = 1,
    }
}
