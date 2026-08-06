using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// The minimum spawner a Clone needs: allocates a fresh unit id and reads the cloned card's armor from the
    /// authored roster. Stands in until a real deck and unit-factory system exists.
    /// </summary>
    /// <remarks>
    /// Ids are handed out from a counter that starts above any hand-registered starting unit, so a clone can
    /// never collide with a unit the bootstrap placed. The counter is per-instance, so a new match needs a new
    /// spawner rather than a reset.
    /// </remarks>
    internal sealed class PlaytestUnitSpawner : IUnitSpawner
    {
        private readonly CardPresenter _cardPresenter;

        private int _nextUnitId;

        internal PlaytestUnitSpawner(CardPresenter cardPresenter, int firstUnitId)
        {
            _cardPresenter = cardPresenter;
            _nextUnitId = firstUnitId;
        }

        /// <summary>
        /// The card the next Clone should produce, overriding the source unit's identity, or default to keep
        /// the resolver's own choice. Cleared by the caller once the move resolves.
        /// </summary>
        /// <remarks>
        /// Per the GDD a deployment is "validation and payment, then Clone or Jump", so playing a card decides
        /// what the clone becomes. <c>MovementResolver</c> passes the source unit's card instead, and it is not
        /// this harness's place to change movement rules — so the substitution happens here, at the one seam
        /// the movement code already delegates unit creation to.
        /// </remarks>
        internal CardId PendingCardId { get; set; }

        /// <summary>The identifier of the most recently spawned unit, so the caller can finish configuring it.</summary>
        internal int LastSpawnedUnitId { get; private set; } = -1;

        public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
        {
            if (!PendingCardId.Equals(default(CardId)))
            {
                cardId = PendingCardId;
            }

            bool hasArmor = false;

            if (_cardPresenter != null && _cardPresenter.TryGetCard(cardId, out ICardData card))
            {
                hasArmor = card.HasArmor;
            }
            else
            {
                Debug.LogWarning($"PlaytestUnitSpawner: no authored card for '{cardId}'. The clone spawns without armor.");
            }

            int unitId = _nextUnitId;
            _nextUnitId++;
            LastSpawnedUnitId = unitId;

            return new GridUnit(unitId, playerId, cardId, at, hasArmor);
        }
    }
}
