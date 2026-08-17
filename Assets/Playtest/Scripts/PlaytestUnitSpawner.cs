using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Playtest
{
    /// <summary>
    /// The minimum spawner a Deploy or a Clone needs: allocates a fresh unit id and reads the spawned card's
    /// armor from the authored roster. <c>MovementResolver</c> always passes the card the unit should carry — the
    /// played card on a Deploy, the source unit's own card on a Clone — so this never has to decide identity
    /// itself, only build the <see cref="GridUnit" /> around it.
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

        public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at)
        {
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

            return new GridUnit(unitId, playerId, cardId, at, hasArmor);
        }
    }
}
