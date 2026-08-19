using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Presenters
{
    /// <summary>
    /// Owns the match's hex grid, building it from the authored layout on <c>Awake</c>. Units are tracked by
    /// <see cref="UnitPresenter" />, which reads the grid from here — the dependency points one way.
    /// </summary>
    /// <remarks>
    /// <b>It builds the grid and does not announce it.</b> <c>MatchEvents.GridInitialized</c> is published by
    /// <c>MatchInitializer</c>, from the <c>Start</c>-time setup sequence <c>MatchController</c> drives.
    /// Publishing it from <c>Awake</c> here is exactly what that hand-off replaced: every subscriber registers
    /// in <c>OnEnable</c>, so on a cold scene load the event went out before <see cref="Views.GridView" /> had
    /// subscribed and no cell was ever built.
    /// A caller that needs the grid before the announcement reads <see cref="HexGrid" /> directly, which is
    /// populated from <c>Awake</c> and never waits.
    /// <para>
    /// The one exception is the editor-only rebuild below, which re-announces because it runs in play mode long
    /// after every subscriber exists. It is a live-tuning affordance, not part of the startup path.
    /// </para>
    /// </remarks>
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

                    // Announced from here, unlike Awake: this runs in play mode, so every view has long since
                    // subscribed and the board a designer just resized would otherwise keep rendering the old
                    // cells. Rebuilding the visuals is the whole point of the edit.
                    MatchEvents.RaiseGridInitialized(HexGrid);
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
        }
    }
}
