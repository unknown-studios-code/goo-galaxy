using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Views
{
    [DisallowMultipleComponent]
    public class GridView : MonoBehaviour
    {
        [Tooltip("The prefab used to represent a single hexagonal cell.")]
        [SerializeField]
        private CellView _cellPrefab;

        [Tooltip("The size of each hex cell (distance from center to corner vertex).")]
        [SerializeField]
        private float _cellVisualSize = 1.0f;

        [Tooltip("Color tint applied to standard playable cells.")]
        [SerializeField]
        private Color _defaultCellColor = Color.white;

        [Tooltip("Color tint applied to blocked, impassable cells.")]
        [SerializeField]
        private Color _blockedCellColor = Color.gray;

        private readonly Dictionary<HexCoordinates, CellView> _cellViews = new();

        public IReadOnlyDictionary<HexCoordinates, CellView> CellViews => _cellViews;

        private void Awake()
        {
            Debug.Assert(_cellPrefab != null, BoardLogMessages.CellViewPrefabNotAssigned, this);
        }

        private void OnEnable()
        {
            StaticGameEvents.GridInitialized += OnGridInitialized;
        }

        private void OnDisable()
        {
            StaticGameEvents.GridInitialized -= OnGridInitialized;
        }

        private void OnGridInitialized(IHexGrid gridObject)
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

            foreach (KeyValuePair<HexCoordinates, HexCell> cellKvp in grid.Cells)
            {
                HexCell cell = cellKvp.Value;
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
