using System;

namespace GooGalaxy.Runtime.Shared.Types
{
    /// <summary>
    /// One attempt to play a card from hand and how it ended, published on
    /// <see cref="Events.MatchEvents.CardPlayAttempted" />. See that event for when each field is filled.
    /// </summary>
    public readonly struct CardPlayAttempt : IEquatable<CardPlayAttempt>
    {
        /// <summary>Describes one play attempt.</summary>
        /// <param name="playerId">The player who attempted the play.</param>
        /// <param name="cardId">The card in the played slot, or <see cref="CardId.Empty" /> when the slot was never read.</param>
        /// <param name="target">The first target hex, or <c>default</c> when no targets were given.</param>
        /// <param name="energyCost">The card's authored Energy cost, or zero when its data never resolved.</param>
        /// <param name="result">How the attempt ended.</param>
        public CardPlayAttempt(int playerId, CardId cardId, HexCoordinates target, int energyCost, CardPlayResult result)
        {
            PlayerId = playerId;
            CardId = cardId;
            Target = target;
            EnergyCost = energyCost;
            Result = result;
        }

        public int PlayerId { get; }

        public CardId CardId { get; }

        public HexCoordinates Target { get; }

        public int EnergyCost { get; }

        public CardPlayResult Result { get; }

        public static bool operator ==(CardPlayAttempt left, CardPlayAttempt right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CardPlayAttempt left, CardPlayAttempt right)
        {
            return !left.Equals(right);
        }

        public bool Equals(CardPlayAttempt other)
        {
            return (PlayerId == other.PlayerId)
                && (CardId == other.CardId)
                && (Target == other.Target)
                && (EnergyCost == other.EnergyCost)
                && (Result == other.Result);
        }

        public override bool Equals(object obj)
        {
            return obj is CardPlayAttempt other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, CardId, Target, EnergyCost, Result);
        }

        public override string ToString()
        {
            return $"Player {PlayerId} played '{CardId}' at {Target} (cost {EnergyCost}): {Result}";
        }
    }
}
