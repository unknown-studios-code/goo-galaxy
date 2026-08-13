using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Views
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class CellView : MonoBehaviour
    {
        [Tooltip("The color tint applied when this cell is highlighted.")]
        [SerializeField]
        private Color _highlightColor = new(1f, 1f, 0.5f, 1f);

        private SpriteRenderer _spriteRenderer;
        private HexCoordinates _cellCoordinates;
        private Color _defaultColor = Color.white;
        private bool _isHighlighted;

        public HexCoordinates CellCoordinates => _cellCoordinates;

        public bool IsHighlighted => _isHighlighted;

        protected void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void InitializeCell(HexCoordinates coords)
        {
            _cellCoordinates = coords;
            gameObject.name = $"Cell_{coords.Q}_{coords.R}";
        }

        public void SetCellColor(Color color)
        {
            _defaultColor = color;
            ApplyColorToRenderer(GetTargetRenderColor());
        }

        public void SetHighlightState(bool active)
        {
            _isHighlighted = active;
            ApplyColorToRenderer(GetTargetRenderColor());
        }

        private Color GetTargetRenderColor()
        {
            return _isHighlighted ? _highlightColor : _defaultColor;
        }

        // SpriteRenderer.color is already a per-instance value the 2D renderer batches, so this needs no
        // MaterialPropertyBlock — unlike a MeshRenderer, where tinting would otherwise instantiate a material.
        private void ApplyColorToRenderer(Color color)
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            _spriteRenderer.color = color;
        }
    }
}
