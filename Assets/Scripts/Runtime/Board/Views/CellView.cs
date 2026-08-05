using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Views
{
    [RequireComponent(typeof(MeshRenderer))]
    public class CellView : MonoBehaviour
    {
        private static readonly int _colorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("The color tint applied when this cell is highlighted.")]
        [SerializeField]
        private Color _highlightColor = new(1f, 1f, 0.5f, 1f);

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private HexCoordinates _cellCoordinates;
        private Color _defaultColor = Color.white;
        private bool _isHighlighted;

        public HexCoordinates CellCoordinates => _cellCoordinates;

        public bool IsHighlighted => _isHighlighted;

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
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

        private void ApplyColorToRenderer(Color color)
        {
            if (_meshRenderer == null)
            {
                return;
            }

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(_colorId, color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
