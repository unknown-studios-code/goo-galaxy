namespace GooGalaxy.Runtime.UI.Models
{
    /// <summary>
    /// One frame of the local player's Energy, as the gauge draws it: how full the bar is, the numbers beside
    /// it, and which single state the bar's border reports.
    /// </summary>
    /// <remarks>
    /// A value type carried by reference (<c>in</c>) on the Energy path, which publishes on
    /// <c>EnergyPresenter</c>'s regeneration quantum rather than per frame: nothing here allocates, and the
    /// gauge compares the whole numbers against the state it last drew so the text is only rewritten when it
    /// actually changed. <c>EnergyGaugeElement</c> carries the rate that follows from that quantum.
    /// </remarks>
    public readonly struct EnergyGaugeState
    {
        /// <summary>The state a gauge holds before a match has configured one.</summary>
        public static readonly EnergyGaugeState Empty = new(0f, 0, 0, EnergyGaugeAccent.None);

        /// <summary>Builds the state the gauge renders.</summary>
        /// <param name="normalizedFill">How full the bar is, from 0 to 1. Values outside are clamped by the gauge.</param>
        /// <param name="wholeEnergy">The Energy total, floored, as the numeric readout shows it.</param>
        /// <param name="maxEnergy">The player's Energy cap, floored. Zero while no match has configured one.</param>
        /// <param name="accent">The single state the border reports.</param>
        public EnergyGaugeState(float normalizedFill, int wholeEnergy, int maxEnergy, EnergyGaugeAccent accent)
        {
            NormalizedFill = normalizedFill;
            WholeEnergy = wholeEnergy;
            MaxEnergy = maxEnergy;
            Accent = accent;
        }

        /// <summary>How full the bar is, from 0 to 1.</summary>
        public float NormalizedFill { get; }

        /// <summary>The Energy total, floored, as the numeric readout shows it.</summary>
        public int WholeEnergy { get; }

        /// <summary>The player's Energy cap, floored. Zero while no match has configured one.</summary>
        public int MaxEnergy { get; }

        /// <summary>The single state the border reports.</summary>
        public EnergyGaugeAccent Accent { get; }
    }

    /// <summary>
    /// What the Energy gauge's border reports. Exactly one applies at a time.
    /// </summary>
    /// <remarks>
    /// <b>USS has no <c>box-shadow</c>, so a box cannot glow</b> — the border is the gauge's only state channel,
    /// which is what forces these to be mutually exclusive rather than additive. They are declared in
    /// increasing precedence and the presenter resolves the winner: overtime outranks a catch-up window, which
    /// outranks a full bar. Nothing is lost when a lower one is masked, because the catch-up window keeps its
    /// own text line above the gauge and a full bar is visible from the fill.
    /// </remarks>
    public enum EnergyGaugeAccent
    {
        /// <summary>Ordinary play. The border recedes.</summary>
        None = 0,

        /// <summary>The bar is at the cap, so regeneration is being wasted.</summary>
        AtCap = 1,

        /// <summary>A catch-up window is open for this player, boosting regeneration.</summary>
        CatchUp = 2,

        /// <summary>The match is in overtime, which doubles regeneration for both players.</summary>
        Overtime = 3,
    }
}
