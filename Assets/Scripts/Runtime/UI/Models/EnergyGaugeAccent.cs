namespace GooGalaxy.Runtime.UI.Models
{
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
