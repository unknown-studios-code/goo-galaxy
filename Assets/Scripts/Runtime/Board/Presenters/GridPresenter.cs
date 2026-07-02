using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Presenters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnitMovementController))]
    public class GridPresenter : MonoBehaviour
    {
        private static readonly Dictionary<int, GridUnit> _emptyRegistry = new();

        [Tooltip("The grid layout settings defining the radius and obstacles.")]
        [SerializeField]
        private GridLayoutSO _gridLayout;

        [Tooltip("The movement controller holding the registry of active units.")]
        [SerializeField]
        private UnitMovementController _movementController;

        public HexGrid HexGrid { get; private set; }

        private void Awake()
        {
            Debug.Assert(_gridLayout != null, BoardLogMessages.GridLayoutConfigurationMissing, this);

            if (_movementController == null)
            {
                _movementController = GetComponent<UnitMovementController>();
            }

            InitializeHexGrid();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && HexGrid != null && _gridLayout != null)
            {
                if (HexGrid.GridRadius != _gridLayout.GridRadius)
                {
                    InitializeHexGrid();
                }
            }
        }
#endif

        public IReadOnlyDictionary<int, GridUnit> GetActiveUnits()
        {
            if (_movementController == null)
            {
                Debug.LogWarning(BoardLogMessages.UnitMovementControllerMissing);
                return _emptyRegistry;
            }

            return _movementController.ActiveUnits;
        }

        private void InitializeHexGrid()
        {
            if (_gridLayout == null)
            {
                Debug.LogError(BoardLogMessages.GridLayoutConfigurationMissing, this);
                return;
            }

            HexGrid = new HexGrid(_gridLayout);
            StaticGameEvents.OnGridInitialized(HexGrid);
        }
    }
}
