using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Interfaces
{
    /// <summary>
    /// Factory contract for bringing new units onto the board.
    /// Movement code owns the rules of a Clone but never the knowledge of decks, card assets, or
    /// identifier allocation, so it delegates creation here.
    /// </summary>
    public interface IUnitSpawner
    {
        /// <summary>
        /// Creates a new unit for the given player on the given coordinate.
        /// The implementation owns identifier allocation and is expected to return a unit in clean
        /// starting state (alive, freshly positioned, carrying no residual state from any other unit).
        /// </summary>
        /// <param name="playerId">The player who will own the new unit.</param>
        /// <param name="cardId">The card the new unit is an instance of.</param>
        /// <param name="at">The coordinate the new unit occupies.</param>
        /// <returns>The newly created unit, or null if the spawner could not create one.</returns>
        public GridUnit SpawnUnit(int playerId, CardId cardId, HexCoordinates at);
    }
}
