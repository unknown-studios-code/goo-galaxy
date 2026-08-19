using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Interfaces
{
    /// <summary>
    /// An action resolver's entire view of a player's hand: read what is in a slot, then rotate that slot once
    /// the action consuming it has been accepted. Used by Match (play and discard resolution) and Deck (the
    /// cycle itself).
    /// </summary>
    /// <remarks>
    /// Dealing a hand is deliberately absent. An implementation is asked to resolve actions against a hand that
    /// already exists, and naming the players is match setup rather than cycle mechanics — whoever bootstraps
    /// the match holds the deck itself for that. Every member sits on the input path, once per player action, so
    /// implementations must stay allocation-free.
    /// </remarks>
    public interface ICardCycle
    {
        /// <summary>Reads a player's current hand.</summary>
        /// <param name="playerId">The player to read.</param>
        /// <param name="hand">
        /// The cards in hand, in slot order, or null when the player has no deck. The list belongs to the
        /// implementation and reflects every later rotation; read it with an indexed <c>for</c> loop.
        /// </param>
        /// <returns>True when the player has a deck; false when they do not.</returns>
        public bool TryGetHand(int playerId, out IReadOnlyList<CardId> hand);

        /// <summary>Reads a single hand slot without changing anything.</summary>
        /// <param name="playerId">The player to read.</param>
        /// <param name="slotIndex">The zero-based hand slot to read.</param>
        /// <param name="card">The card in that slot, or a default id when the player or the slot is unknown.</param>
        /// <returns>True when the player has a deck and the index names one of its hand slots.</returns>
        /// <remarks>
        /// Answers false both for an unknown player and for an out-of-range slot; a caller that has to tell the
        /// two apart asks <see cref="TryGetHand" /> first.
        /// </remarks>
        public bool TryGetSlot(int playerId, int slotIndex, out CardId card);

        /// <summary>
        /// Rotates one of a player's hand slots, sending the card it held to the back of the cycle and sliding
        /// the queued card into the freed slot.
        /// </summary>
        /// <param name="playerId">The player whose hand rotates.</param>
        /// <param name="slotIndex">The zero-based hand slot to rotate.</param>
        /// <param name="played">The card that left the slot, read at the moment of the mutation, or a default id
        /// when the rotation was refused.</param>
        /// <returns>True once the slot has rotated; false when the player or the slot is unknown, in which case
        /// the hand is left exactly as it was.</returns>
        /// <remarks>
        /// <b>Only legal once the action consuming the slot has been accepted</b> — a play the board resolved or
        /// a discard the ledger has already charged — which is what makes the cycle advance a consequence of a
        /// resolved action rather than of an attempted one. Calling it before that point cycles a card out of
        /// hand for an action that may still be rejected, and nothing in the contract can undo it. This is the
        /// reason the rotation is not on any presenter's public surface: it is reachable only through this
        /// interface, by the resolver that already knows the action stuck.
        /// <para>
        /// Publishes <c>MatchEvents.HandChanged</c> with the rotated hand, synchronously, so a subscriber runs
        /// inside this call; nothing is published when the rotation is refused.
        /// </para>
        /// </remarks>
        public bool TryAdvanceSlot(int playerId, int slotIndex, out CardId played);
    }
}
