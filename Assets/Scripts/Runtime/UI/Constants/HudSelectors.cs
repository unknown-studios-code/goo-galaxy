namespace GooGalaxy.Runtime.UI.Constants
{
    /// <summary>
    /// Every UXML <c>name</c> and every runtime USS class the match HUD is built from. Not all of them are read
    /// from C#: around a dozen have no call site at all and exist as the contract surface the two selector
    /// fixtures reflect over, which is what makes a rename in the markup or the stylesheet fail a test.
    /// </summary>
    /// <remarks>
    /// <b>A renamed element is caught by a test, not by the compiler.</b> Nothing in C# references the markup
    /// by name, so a rename in <c>MatchHudView.uxml</c> cannot fail a build. What catches it is
    /// <c>HudSelectorContractTests</c>, which reflects over the name consts below and resolves every one of them
    /// against the imported <c>VisualTreeAsset</c> — and it only works while the const lives here rather than
    /// being inlined at its call site, which is the discipline this class exists to hold.
    /// <para>
    /// <b>A renamed USS class is caught the same way.</b> <c>HudStylesheetContractTests</c> resolves every const
    /// below that names a class against the selectors Unity parsed out of <c>MatchHudView.uss</c> and
    /// <c>DesignTokens.uss</c>, so a class renamed in one file and not the other fails that fixture instead of
    /// silently styling nothing. It finds those consts by <i>naming form</i> — the <c>Block</c> suffix, the
    /// <c>Is</c> prefix, and the BEM <c>__element</c> and <c>--modifier</c> separators — plus the nine
    /// dual-purpose consts it lists by hand because they escape all three. A class const added later is covered
    /// automatically only while it follows one of those forms; one that does not is read as a UXML name and
    /// resolved against the markup, where it will not be found.
    /// </para>
    /// <para>
    /// <b>One value is never declared twice.</b> Where a block styles the same element it names — the gauge,
    /// the timer, the hand strip and six others — a single const carries both roles under the element-name
    /// form, because two names for one string can be renamed apart, which is the exact failure this class
    /// exists to prevent. A const that is <i>only</i> a USS class is what the <c>Block</c> suffix, the
    /// <c>Is</c> prefix and the BEM <c>__element</c> and <c>--modifier</c> separators mark; nothing outside
    /// those three forms is anything but a UXML <c>name</c>, which is the rule the selector contract test
    /// reflects over to find the names it resolves against the real markup.
    /// </para>
    /// </remarks>
    public static class HudSelectors
    {
        /// <summary>Hand slots the HUD draws, matching <c>DeckState.DefaultHandSize</c>.</summary>
        /// <remarks>
        /// The markup authors this many <see cref="HandSlotZero" />-style elements, so a deck dealt a larger
        /// hand has its surplus dropped rather than rendered. <c>DeckPresenter</c>'s hand size is authored per
        /// scene and this is not, which is why the two are compared at deal time rather than assumed equal.
        /// </remarks>
        public const int HandSlotCount = 4;

        public const string Background = "hud-background";

        public const string SafeArea = "hud-safe-area";

        public const string TopBar = "top-bar";

        public const string OpponentBadge = "opponent-badge";

        public const string OpponentScore = "opponent-score";

        public const string MatchTimer = "match-timer";

        public const string BoardWindow = "board-window";

        public const string BottomBar = "bottom-bar";

        public const string StatusRow = "status-row";

        public const string LocalScore = "local-score";

        public const string EmoteSlot = "emote-slot";

        public const string CatchUpLine = "catch-up-line";

        public const string EnergyGauge = "energy-gauge";

        public const string HandStrip = "hand-strip";

        public const string HandSlotZero = "hand-slot-0";

        public const string HandSlotOne = "hand-slot-1";

        public const string HandSlotTwo = "hand-slot-2";

        public const string HandSlotThree = "hand-slot-3";

        public const string NextCardSlot = "next-card-slot";

        public const string CountdownScrim = "countdown-scrim";

        public const string CountdownOverlay = "countdown-overlay";

        public const string OvertimeBanner = "overtime-banner";

        public const string OutcomeOverlay = "outcome-overlay";

        public const string OutcomeTitle = "outcome-title";

        public const string OutcomeReason = "outcome-reason";

        public const string BackgroundBlock = "hud__background";

        public const string SafeAreaBlock = "hud__safe-area";

        public const string TopBarBlock = "hud__top-bar";

        public const string BoardWindowBlock = "hud__board-window";

        public const string BottomBarBlock = "hud__bottom-bar";

        public const string StatusRowBlock = "hud__status-row";

        public const string ScrimBlock = "hud__scrim";

        public const string MatchTimerUrgent = "match-timer--urgent";

        public const string ScoreBadgeBlock = "score-badge";

        public const string ScoreBadgeValue = "score-badge__value";

        public const string ScoreBadgeFactionOne = "score-badge--faction-one";

        public const string ScoreBadgeFactionTwo = "score-badge--faction-two";

        public const string OpponentBadgeLabel = "opponent-badge__label";

        public const string EnergyGaugeTrack = "energy-gauge__track";

        public const string EnergyGaugeFill = "energy-gauge__fill";

        public const string EnergyGaugeValue = "energy-gauge__value";

        public const string EnergyGaugeAtCap = "energy-gauge--at-cap";

        public const string EnergyGaugeCatchUp = "energy-gauge--catch-up";

        public const string EnergyGaugeOvertime = "energy-gauge--overtime";

        public const string HandStripDivider = "hand-strip__divider";

        public const string CardSlotBlock = "card-slot";

        public const string CardSlotAccent = "card-slot__accent";

        public const string CardSlotName = "card-slot__name";

        public const string CardSlotCost = "card-slot__cost";

        public const string CardSlotScrim = "card-slot__scrim";

        public const string CardSlotNext = "card-slot--next";

        public const string CardSlotEmpty = "card-slot--empty";

        public const string CardSlotUnaffordable = "card-slot--unaffordable";

        public const string CardSlotProtocol = "card-slot--protocol";

        public const string CardSlotAccentBaseline = "card-slot__accent--baseline";

        public const string CardSlotAccentControl = "card-slot__accent--control";

        public const string CardSlotAccentExplosive = "card-slot__accent--explosive";

        public const string CardSlotAccentDefensive = "card-slot__accent--defensive";

        public const string CardSlotAccentCorrosive = "card-slot__accent--corrosive";

        public const string CountdownOverlayValue = "countdown-overlay__value";

        public const string OvertimeBannerLabel = "overtime-banner__label";

        public const string OutcomeOverlayTitle = "outcome-overlay__title";

        public const string OutcomeOverlayReason = "outcome-overlay__reason";

        /// <summary>The state class that removes an element from layout, shared by every block that hides.</summary>
        public const string IsHidden = "is-hidden";

        /// <summary>The state class that hides an element but keeps the space it occupied.</summary>
        /// <remarks>
        /// The opponent score uses this rather than <see cref="IsHidden" />: the top bar centres the timer
        /// between two flexible ends, and dropping one of them out of layout would slide the timer off centre.
        /// </remarks>
        public const string IsInvisible = "is-invisible";

        /// <summary>Resolves the UXML name of a hand slot from its zero-based index.</summary>
        /// <param name="slotIndex">Slot to name. Outside <c>0..HandSlotCount-1</c> there is no element.</param>
        /// <returns>The element name, or <c>null</c> when the index addresses no authored slot.</returns>
        public static string GetHandSlotName(int slotIndex)
        {
            return slotIndex switch
            {
                0 => HandSlotZero,
                1 => HandSlotOne,
                2 => HandSlotTwo,
                3 => HandSlotThree,
                _ => null,
            };
        }
    }
}
