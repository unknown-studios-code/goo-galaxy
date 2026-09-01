using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Views
{
    [DisallowMultipleComponent]
    public class GridView : MonoBehaviour
    {
        [Header("Cells")]
        [SerializeField]
        private CellView _cellPrefab;

        [Tooltip("Distance from a cell's center to its corner vertex, in world units. Must match the prefab mesh or cells overlap.")]
        [SerializeField]
        private float _cellVisualSize = 1.0f;

        [Header("Colors")]
        [Tooltip("Tint applied to standard playable cells.")]
        [SerializeField]
        private Color _defaultCellColor = Color.white;

        [Tooltip("Tint applied to blocked, impassable cells.")]
        [SerializeField]
        private Color _blockedCellColor = Color.gray;

        private readonly Dictionary<HexCoordinates, CellView> _cellViews = new();

        private HexGrid _builtGrid;

        public IReadOnlyDictionary<HexCoordinates, CellView> CellViews => _cellViews;

        /// <summary>The size the cells were projected at — center to corner vertex, in world units.</summary>
        /// <remarks>
        /// Exposed so anything inverting the projection — turning a screen point back into a hex — reads the
        /// value the board was actually drawn with. A second authored copy of it would put every hit test on the
        /// wrong hex the first time one of the two was retuned.
        /// </remarks>
        public float CellVisualSize => _cellVisualSize;

        protected void Awake()
        {
            Debug.Assert(_cellPrefab != null, BoardLogMessages.CellViewPrefabNotAssigned, this);
        }

        protected void OnEnable()
        {
            MatchEvents.GridInitialized += HandleGridInitialized;
        }

        protected void OnDisable()
        {
            MatchEvents.GridInitialized -= HandleGridInitialized;
        }

        /// <remarks>Assigns the cell prefab and its world size, so it must run before the view builds a grid.</remarks>
        internal void SetViewConfiguration(CellView cellPrefab, float cellVisualSize)
        {
            _cellPrefab = cellPrefab;
            _cellVisualSize = cellVisualSize;
        }

        private void BuildVisualGrid(HexGrid grid)
        {
            DestroyVisualGrid();

            if (_cellPrefab == null)
            {
                Debug.LogError(BoardLogMessages.CellViewPrefabNotAssigned, this);
                return;
            }

            foreach (HexCell cell in grid.CellValues)
            {
                Vector3 worldPos = HexMathUtils.ProjectToWorldSpace(cell.Coordinates, _cellVisualSize);

                CellView tileInstance = Instantiate(_cellPrefab, worldPos, Quaternion.identity, transform);
                tileInstance.InitializeCell(cell.Coordinates);

                Color color = cell.IsBlocked ? _blockedCellColor : _defaultCellColor;
                tileInstance.SetCellColor(color);

                _cellViews[cell.Coordinates] = tileInstance;
            }

            // Claimed only once the cells actually exist, so a build that returned on a missing prefab does not
            // leave the view believing it has already rendered this grid.
            _builtGrid = grid;
        }

        // Returns the existing cells to the state a fresh build would produce: authored tint restored and any
        // highlight the previous match left dropped, so a rematch never inherits a selection from the last one.
        private void ResetVisualGrid(HexGrid grid)
        {
            foreach (HexCell cell in grid.CellValues)
            {
                if (!_cellViews.TryGetValue(cell.Coordinates, out CellView cellView) || cellView == null)
                {
                    // A cell went missing since the build — destroyed from outside, or a coordinate the grid
                    // gained. Nothing can be reset onto it, so fall back to the full rebuild.
                    BuildVisualGrid(grid);
                    return;
                }

                cellView.SetHighlightState(false);
                cellView.SetCellColor(cell.IsBlocked ? _blockedCellColor : _defaultCellColor);
            }
        }

        private void DestroyVisualGrid()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            _cellViews.Clear();

            // Cleared with the cells it describes. The editor's OnValidate rebuild constructs a genuinely new
            // HexGrid, so it never matches this anyway — but a view that kept a reference to a grid it no longer
            // renders would answer the next announcement with a reset over an empty dictionary.
            _builtGrid = null;
        }

        private void HandleGridInitialized(IHexGrid gridObject)
        {
            if (gridObject is not HexGrid grid)
            {
                return;
            }

            // PERF: reference identity, not equality. Every match start re-announces the board, and GridPresenter
            // only ever constructs a new HexGrid when the authored radius changes — so a rematch hands back the
            // very instance already on screen, and rebuilding would Destroy and Instantiate 61 cell prefabs,
            // each carrying a SpriteRenderer, a 13-point PolygonCollider2D and a CellView, for a board that did
            // not move. The reset below is what a rematch actually needs: the same cells, clean.
            if (ReferenceEquals(grid, _builtGrid))
            {
                ResetVisualGrid(grid);
                return;
            }

            BuildVisualGrid(grid);
        }
    }
}
