using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Presenters
{
    public class UnitMovementController : MonoBehaviour
    {
        private readonly Dictionary<int, GridUnit> _activeUnits = new();

        public IReadOnlyDictionary<int, GridUnit> ActiveUnits => _activeUnits;

        // Placeholder for future movement resolution methods (ValidateMove, ExecuteMove, etc.)
    }
}
