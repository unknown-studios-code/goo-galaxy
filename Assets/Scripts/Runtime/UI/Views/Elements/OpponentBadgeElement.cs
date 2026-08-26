using GooGalaxy.Runtime.UI.Constants;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views.Elements
{
    /// <summary>
    /// The opponent's identity in the top bar.
    /// </summary>
    /// <remarks>
    /// A text placeholder for the MVP: the Researcher avatar the art direction calls for needs imported art,
    /// and this task imports none. The block is sized for that portrait, so dropping an image child in front of
    /// the label later moves nothing else in the bar.
    /// </remarks>
    [UxmlElement]
    public partial class OpponentBadgeElement : VisualElement
    {
        private readonly Label _label;

        public OpponentBadgeElement()
        {
            AddToClassList(HudSelectors.OpponentBadge);
            pickingMode = PickingMode.Ignore;

            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(HudSelectors.OpponentBadgeLabel);

            Add(_label);
        }

        public string LabelText => _label.text;

        /// <summary>Names the opponent.</summary>
        /// <param name="label">The text to draw. Null falls back to <see cref="HudText.OpponentUnknown" />.</param>
        public void SetLabel(string label)
        {
            _label.text = label ?? HudText.OpponentUnknown;
        }
    }
}
