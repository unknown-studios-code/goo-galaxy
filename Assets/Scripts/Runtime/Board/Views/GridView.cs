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

        public IReadOnlyDictionary<HexCoordinates, CellView> CellViews => _cellViews;

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

        private void HandleGridInitialized(IHexGrid gridObject)
        {
            if (gridObject is HexGrid grid)
            {
                BuildVisualGrid(grid);
            }
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
        }
    }
}
