using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Commands
{
    /// <summary>
    /// Immutable description of a requested Protocol deployment: who is playing it, which card, and the hexes
    /// the player picked. The board's spell equivalent of <see cref="MoveCommand"/>.
    /// </summary>
    /// <remarks>
    /// <b>Ownership:</b> <see cref="TargetHexes"/> is owned by the caller and is only borrowed for the duration
    /// of the call it is passed to. The resolver reads it and never retains it, so the caller may reuse the
    /// backing buffer for the next spell. Do not publish a <see cref="SpellCommand"/> on the event bus without
    /// first copying the targets.
    /// </remarks>
    public readonly struct SpellCommand
    {
        /// <summary>Builds one Protocol deployment request.</summary>
        /// <param name="playerId">The player deploying the Protocol.</param>
        /// <param name="cardId">The authored card being played, for logging and telemetry.</param>
        /// <param name="targetHexes">
        /// The hexes the player picked, centre first. Borrowed for the duration of the call and never retained.
        /// </param>
        public SpellCommand(int playerId, CardId cardId, IReadOnlyList<HexCoordinates> targetHexes)
        {
            PlayerId = playerId;
            CardId = cardId;
            TargetHexes = targetHexes;
        }

        public int PlayerId { get; }

        public CardId CardId { get; }

        /// <summary>
        /// The hexes the player picked. The first entry is the cluster centre every other target is measured
        /// against, which is what makes the GDD's "1 center hex + 2 adjacent" expressible as authored data.
        /// </summary>
        /// <remarks>
        /// Owned by the caller and only valid for the duration of the call. Read it with an indexed
        /// <c>for</c> loop — <c>foreach</c> over the interface boxes the backing enumerator.
        /// </remarks>
        public IReadOnlyList<HexCoordinates> TargetHexes { get; }
    }
}
