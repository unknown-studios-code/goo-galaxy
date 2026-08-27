namespace GooGalaxy.Runtime.UI.Constants
{
    /// <summary>
    /// The player-facing strings the match HUD renders that no gameplay type supplies.
    /// </summary>
    /// <remarks>
    /// English placeholders for the MVP, held as constants for the same reason log text is: the HUD writes them
    /// into elements at runtime, so a literal scattered through the presenter is a string nobody can find when
    /// localization lands. Replace the values with lookups here rather than at the call sites.
    /// <para>
    /// Nothing here carries a format argument. The two values that do vary are composed where each is drawn,
    /// next to the caching that keeps that composition off the steady-state path: a card's Energy cost in
    /// <c>CardSlotElement</c>, off a lookup table, and the catch-up window's remaining seconds in
    /// <c>MatchHudView.SetCatchUp</c>, gated on the window having actually moved.
    /// </para>
    /// <para>
    /// <b>Only text C# writes at runtime belongs here</b>, which is wider than text the presenter chooses
    /// between: <see cref="EmptySlot" />, <see cref="CatchUpPrefix" /> and <see cref="CatchUpSuffix" /> are
    /// written by the view and by an element, and the presenter never names them. Static chrome that never
    /// varies — the overtime banner and the emote button — is authored in <c>MatchHudView.uxml</c> as a
    /// <c>text</c> attribute and is deliberately not mirrored by a constant, because a constant nothing writes
    /// is one more place for the same string to be edited in only one of the two. The one runtime-written
    /// string that does not live here is the blank timer: <c>HudClockFormatter.Blank</c> sits on the formatter,
    /// beside the values it alternates with.
    /// </para>
    /// </remarks>
    public static class HudText
    {
        public const string OpponentMachine = "AI OPPONENT";

        public const string OpponentRemote = "RIVAL";

        public const string OpponentUnknown = "OPPONENT";

        public const string OutcomeVictory = "VICTORY";

        public const string OutcomeDefeat = "DEFEAT";

        public const string OutcomeDraw = "DRAW";

        public const string ReasonTimeLimit = "Clock ran out";

        public const string ReasonDomination = "Total assimilation";

        public const string ReasonDraw = "Counts level";

        public const string ReasonSurrender = "Expedition recalled";

        public const string ReasonUnknown = "";

        public const string CatchUpPrefix = "CATCH-UP ENERGY ";

        public const string CatchUpSuffix = "s";

        public const string EmptySlot = "";
    }
}
