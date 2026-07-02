using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Interfaces;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Data
{
    [CreateAssetMenu(fileName = "NewGridLayout", menuName = "Goo Galaxy/Board/Grid Layout", order = 0)]
    public class GridLayoutSO : ScriptableObject, IGridLayout
    {
        [Tooltip("The number of hex rings extending outward from the center (0, 0).")]
        [SerializeField]
        private int _gridRadius = 4;

        [Tooltip("List of axial coordinates (q = X, r = Y) that are blocked/impassable obstacles.")]
        [SerializeField]
        private Vector2Int[] _blockedCoordinates = Array.Empty<Vector2Int>();

        private ReadOnlySet<HexCoordinates> _blockedCoordinatesWrapper;

        public int GridRadius => _gridRadius;

        public IReadOnlySet<HexCoordinates> BlockedCoordinates
        {
            get
            {
                if (_blockedCoordinatesWrapper == null)
                {
                    InitializeBlockedCoordinates();
                }

                return _blockedCoordinatesWrapper;
            }
        }

        private void OnValidate()
        {
            ClampGridRadius();
            ValidateBlockedCoordinates();
            InitializeBlockedCoordinates();
        }

        private void InitializeBlockedCoordinates()
        {
            var set = new HashSet<HexCoordinates>(_blockedCoordinates != null ? _blockedCoordinates.Length : 0);

            if (_blockedCoordinates != null)
            {
                foreach (Vector2Int tile in _blockedCoordinates)
                {
                    set.Add(new HexCoordinates(tile.x, tile.y));
                }
            }

            _blockedCoordinatesWrapper = new ReadOnlySet<HexCoordinates>(set);
        }

        private void ClampGridRadius()
        {
            _gridRadius = Mathf.Max(0, _gridRadius);
        }

        private void ValidateBlockedCoordinates()
        {
            if (_blockedCoordinates == null || _blockedCoordinates.Length <= 1)
            {
                return;
            }

            Vector2Int last = _blockedCoordinates[^1];
            Vector2Int secondLast = _blockedCoordinates[^2];

            if (last == secondLast)
            {
                if (secondLast == Vector2Int.zero)
                {
                    Array.Resize(ref _blockedCoordinates, _blockedCoordinates.Length - 1);
                    Debug.LogWarning(string.Format(BoardLogMessages.CannotAddBlockedCoordinateFormat, name), this);
                }
                else
                {
                    _blockedCoordinates[^1] = Vector2Int.zero;
                    DeduplicateBlockedCoordinates(_blockedCoordinates.Length - 1);
                }
            }
            else
            {
                DeduplicateBlockedCoordinates(_blockedCoordinates.Length);
            }
        }

        private void DeduplicateBlockedCoordinates(int countToProcess)
        {
            var unique = new HashSet<Vector2Int>();
            var deduplicated = new List<Vector2Int>(countToProcess);

            for (int i = 0; i < countToProcess; i++)
            {
                if (unique.Add(_blockedCoordinates[i]))
                {
                    deduplicated.Add(_blockedCoordinates[i]);
                }
            }

            if (deduplicated.Count < countToProcess)
            {
                if (countToProcess < _blockedCoordinates.Length)
                {
                    deduplicated.Add(Vector2Int.zero);
                }

                _blockedCoordinates = deduplicated.ToArray();
                Debug.LogWarning(string.Format(BoardLogMessages.DuplicateBlockedCoordinatesFormat, name), this);
            }
        }
    }
}
