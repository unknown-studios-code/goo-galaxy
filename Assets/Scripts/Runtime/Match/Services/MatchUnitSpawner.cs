using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Match.Services
{
    /// <remarks>
    /// The spawner a Deploy and a Clone build their new unit through: it allocates a fresh unit id and reads the
    /// spawned card's armor from the authored roster. <c>MovementResolver</c> always passes the card the unit
    /// should carry — the played card on a Deploy, the source unit's own card on a Clone — so this never has to
    /// decide identity itself, only build the <see cref="GridUnit" /> around it.
    /// <para>
    /// Ids are handed out from a counter that starts above every seeded starting unit, so a spawned unit can
    /// never collide with one the opening position placed. <b>The counter is per-instance, so a new match needs
    /// a new spawner rather than a reset one</b> — that is what makes ids restart from the same base every match
    /// and keeps a stale id from a previous match out of a fresh board.
    /// </para>
    /// </remarks>
    internal sealed class MatchUnitSpawner : IUnitSpawner
    {
        private readonly CardPresenter _cardPresenter;

        private int _nextUnitId;

        internal MatchUnitSpawner(CardPresenter cardPresenter, int firstUnitId)
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
                // The roster is the object to open, so it is the context this message points at. A unit still
                // spawns: refusing one here would fail a move the board has already validated and charged for.
                Debug.LogWarning(string.Format(MatchLogMessages.SpawnedUnitCardMissingFormat, cardId), _cardPresenter);
            }

            int unitId = _nextUnitId;
            _nextUnitId++;

            return new GridUnit(unitId, playerId, cardId, at, hasArmor);
        }
    }
}
