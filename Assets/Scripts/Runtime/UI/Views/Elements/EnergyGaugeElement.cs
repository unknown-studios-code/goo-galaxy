using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views.Elements
{
    /// <summary>
    /// The local player's Energy bar: a fill that tracks the total continuously, a numeric readout beside it,
    /// and a border that reports whichever single state currently applies.
    /// </summary>
    /// <remarks>
    /// <b>Allocation-free on the Energy path.</b> Energy publishes roughly seven times a second, so the fill is
    /// a percentage write — a struct assignment — while the readout is only rewritten when its whole number
    /// actually moves, and then only by indexing a table composed once per Energy cap.
    /// <para>
    /// <b>The border is the only state channel.</b> USS has no <c>box-shadow</c>, so nothing here can glow; see
    /// <see cref="EnergyGaugeAccent" /> for the precedence that follows from having one channel and three
    /// states that can hold at once.
    /// </para>
    /// </remarks>
    [UxmlElement]
    public partial class EnergyGaugeElement : VisualElement
    {
        private const float FullPercent = 100f;

        // A cap above this composes its readout instead of indexing a table. Nothing authors an Energy cap near
        // it; the branch exists so an unexpected value still renders.
        private const int MaxTabulatedCap = 99;

        // What the readout shows before a match has configured an Energy cap. Reached on every cold enable, not
        // only as a safety branch: the presenter pushes Energy from its own OnEnable, before the ledger holds a
        // state to read a cap off.
        private const string UnconfiguredValueText = "0/0";

        private readonly VisualElement _track;
        private readonly VisualElement _fill;
        private readonly Label _valueLabel;

        private string[] _valueTexts;
        private EnergyGaugeState _state = EnergyGaugeState.Empty;
        private int _tabulatedCap = -1;
        private bool _isDrawn;

        public EnergyGaugeElement()
        {
            AddToClassList(HudSelectors.EnergyGauge);
            pickingMode = PickingMode.Ignore;

            _track = new VisualElement { pickingMode = PickingMode.Ignore };
            _track.AddToClassList(HudSelectors.EnergyGaugeTrack);

            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.AddToClassList(HudSelectors.EnergyGaugeFill);
            _track.Add(_fill);

            _valueLabel = new Label { pickingMode = PickingMode.Ignore };
            _valueLabel.AddToClassList(HudSelectors.EnergyGaugeValue);

            Add(_track);
            Add(_valueLabel);
        }

        public EnergyGaugeState State => _state;

        /// <summary>Draws one frame of Energy.</summary>
        /// <param name="state">What to draw. A fill outside 0..1 is clamped rather than rejected.</param>
        public void SetState(in EnergyGaugeState state)
        {
            // PERF: gated like the two writes below it. The write allocates nothing, but it dirties layout, and an
            // idle or capped bar publishes the same fill several times a second.
            if (!_isDrawn || !Mathf.Approximately(state.NormalizedFill, _state.NormalizedFill))
            {
                _fill.style.width = Length.Percent(Mathf.Clamp01(state.NormalizedFill) * FullPercent);
            }

            if (!_isDrawn || (state.WholeEnergy != _state.WholeEnergy) || (state.MaxEnergy != _state.MaxEnergy))
            {
                _valueLabel.text = ResolveValueText(state.WholeEnergy, state.MaxEnergy);
            }

            if (!_isDrawn || (state.Accent != _state.Accent))
            {
                ApplyAccent(state.Accent);
            }

            _state = state;
            _isDrawn = true;
        }

        private static string[] BuildValueTexts(int maxEnergy)
        {
            string[] texts = new string[maxEnergy + 1];

            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = $"{i}/{maxEnergy}";
            }

            return texts;
        }

        private string ResolveValueText(int wholeEnergy, int maxEnergy)
        {
            if (maxEnergy <= 0)
            {
                return UnconfiguredValueText;
            }

            if (maxEnergy > MaxTabulatedCap)
            {
                return $"{wholeEnergy}/{maxEnergy}";
            }

            if (maxEnergy != _tabulatedCap)
            {
                _valueTexts = BuildValueTexts(maxEnergy);
                _tabulatedCap = maxEnergy;
            }

            return _valueTexts[Mathf.Clamp(wholeEnergy, 0, maxEnergy)];
        }

        private void ApplyAccent(EnergyGaugeAccent accent)
        {
            EnableInClassList(HudSelectors.EnergyGaugeAtCap, accent == EnergyGaugeAccent.AtCap);
            EnableInClassList(HudSelectors.EnergyGaugeCatchUp, accent == EnergyGaugeAccent.CatchUp);
            EnableInClassList(HudSelectors.EnergyGaugeOvertime, accent == EnergyGaugeAccent.Overtime);
        }
    }
}
