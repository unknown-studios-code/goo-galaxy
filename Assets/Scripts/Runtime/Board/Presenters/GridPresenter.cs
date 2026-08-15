using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Presenters
{
    /// <summary>
    /// Owns the match's hex grid: builds it from the authored layout on <c>Awake</c> and announces it through
    /// <c>MatchEvents.GridInitialized</c>. Units are tracked by <see cref="UnitPresenter" />, which reads the
    /// grid from here — the dependency points one way.
    /// </summary>
    [DisallowMultipleComponent]
    public class GridPresenter : MonoBehaviour
    {
        [Tooltip("The grid layout settings defining the radius and obstacles.")]
        [SerializeField]
        private GridLayoutSO _gridLayout;

        /// <summary>The grid built from the authored layout, or null while the layout is missing.</summary>
        public HexGrid HexGrid { get; private set; }

        protected void Awake()
        {
            Debug.Assert(_gridLayout != null, BoardLogMessages.GridLayoutConfigurationMissing, this);

            InitializeHexGrid();
        }

#if UNITY_EDITOR
        protected void OnValidate()
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

        /// <remarks>Assigns the layout asset that <c>Awake</c> builds the grid from, so it must run before it.</remarks>
        internal void SetGridLayout(GridLayoutSO gridLayout)
        {
            _gridLayout = gridLayout;
        }

        private void InitializeHexGrid()
        {
            if (_gridLayout == null)
            {
                Debug.LogError(BoardLogMessages.GridLayoutConfigurationMissing, this);
                return;
            }

            HexGrid = new HexGrid(_gridLayout);
            MatchEvents.RaiseGridInitialized(HexGrid);
        }
    }
}
