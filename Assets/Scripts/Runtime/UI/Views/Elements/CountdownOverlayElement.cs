using System.Globalization;
using GooGalaxy.Runtime.UI.Constants;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views.Elements
{
    /// <summary>
    /// The large centred numeral of the pre-match countdown, drawn over the scrim that dims the HUD behind it.
    /// </summary>
    /// <remarks>
    /// <b>It never takes a pick.</b> The scrim and this numeral are a statement that play has not opened, not a
    /// modal that enforces it — the domain already refuses every play outside the two played phases, so blocking
    /// input here would only add a second place for that rule to be wrong.
    /// </remarks>
    [UxmlElement]
    public partial class CountdownOverlayElement : VisualElement
    {
        // A countdown longer than this composes its numeral instead of indexing the table. The authored
        // countdown is a few seconds; the branch exists so an unexpected value still renders.
        private const int MaxTabulatedSeconds = 9;

        private static readonly string[] _secondTexts = BuildSecondTexts();

        private readonly Label _valueLabel;

        private int _seconds = -1;

        public CountdownOverlayElement()
        {
            AddToClassList(HudSelectors.CountdownOverlay);
            pickingMode = PickingMode.Ignore;

            _valueLabel = new Label { pickingMode = PickingMode.Ignore };
            _valueLabel.AddToClassList(HudSelectors.CountdownOverlayValue);

            Add(_valueLabel);
        }

        /// <summary>The value this overlay last drew, or -1 before it drew anything.</summary>
        public int Seconds => _seconds;

        /// <summary>Draws the seconds left before play opens.</summary>
        /// <param name="seconds">Seconds remaining. Negative values are drawn as zero.</param>
        public void SetSeconds(int seconds)
        {
            if (seconds == _seconds)
            {
                return;
            }

            _seconds = seconds;
            _valueLabel.text = ResolveSecondText(seconds);
        }

        private static string[] BuildSecondTexts()
        {
            string[] texts = new string[MaxTabulatedSeconds + 1];

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = i.ToString(CultureInfo.InvariantCulture);
            }

            return texts;
        }

        private static string ResolveSecondText(int seconds)
        {
            if (seconds <= 0)
            {
                return _secondTexts[0];
            }

            if (seconds > MaxTabulatedSeconds)
            {
                return seconds.ToString(CultureInfo.InvariantCulture);
            }

            return _secondTexts[seconds];
        }
    }
}
