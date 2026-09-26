using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Views.Elements;
using UnityEngine.UIElements;

namespace GooGalaxy.Tests.Utils
{
    /// <summary>
    /// Builds a code-only element tree carrying every named element <c>MatchHudView.CacheElements</c> looks up,
    /// with no UXML asset behind it.
    /// </summary>
    /// <remarks>
    /// For a fixture that only needs <c>MatchHudView</c> to activate without a missing-element error — because
    /// the HUD's own rendering is not what it exercises — this replaces loading the authored
    /// <c>MatchHudView.uxml</c> through <c>UnityEditor.AssetDatabase</c>, which does not exist in a player build.
    /// <c>VisualElement.Q</c> searches the whole subtree regardless of nesting, so only the names and types below
    /// have to match <see cref="HudSelectors" /> and <c>CacheElements</c> — the hierarchy mirrors the production
    /// markup for readability, not because <c>CacheElements</c> requires it.
    /// </remarks>
    public static class HudTestTreeBuilder
    {
        /// <summary>Adds the named element tree <c>MatchHudView.CacheElements</c> requires under <paramref name="root" />.</summary>
        /// <param name="root">The panel root to build onto, typically a <c>UIDocument.rootVisualElement</c>.</param>
        public static void Build(VisualElement root)
        {
            var background = new VisualElement { name = HudSelectors.Background };
            var topBar = new VisualElement { name = HudSelectors.TopBar };
            var opponentScore = new ScoreBadgeElement { name = HudSelectors.OpponentScore };
            var matchTimer = new Label { name = HudSelectors.MatchTimer };
            var opponentBadge = new OpponentBadgeElement { name = HudSelectors.OpponentBadge };
            topBar.Add(opponentScore);
            topBar.Add(matchTimer);
            topBar.Add(opponentBadge);

            var bottomBar = new VisualElement { name = HudSelectors.BottomBar };
            var statusRow = new VisualElement { name = HudSelectors.StatusRow };
            var localScore = new ScoreBadgeElement { name = HudSelectors.LocalScore };
            statusRow.Add(localScore);

            var catchUpLine = new Label { name = HudSelectors.CatchUpLine };
            var energyGauge = new EnergyGaugeElement { name = HudSelectors.EnergyGauge };
            var discardZone = new VisualElement { name = HudSelectors.DiscardZone };
            var handStrip = new VisualElement { name = HudSelectors.HandStrip };

            for (int i = 0; i < HudSelectors.HandSlotCount; i++)
            {
                handStrip.Add(new CardSlotElement { name = HudSelectors.GetHandSlotName(i) });
            }

            handStrip.Add(new CardSlotElement { name = HudSelectors.NextCardSlot });

            bottomBar.Add(statusRow);
            bottomBar.Add(catchUpLine);
            bottomBar.Add(energyGauge);
            bottomBar.Add(discardZone);
            bottomBar.Add(handStrip);

            background.Add(topBar);
            background.Add(bottomBar);

            var countdownScrim = new VisualElement { name = HudSelectors.CountdownScrim };
            var countdownOverlay = new CountdownOverlayElement { name = HudSelectors.CountdownOverlay };
            var overtimeBanner = new VisualElement { name = HudSelectors.OvertimeBanner };

            var outcomeOverlay = new VisualElement { name = HudSelectors.OutcomeOverlay };
            var outcomeTitle = new Label { name = HudSelectors.OutcomeTitle };
            var outcomeReason = new Label { name = HudSelectors.OutcomeReason };
            outcomeOverlay.Add(outcomeTitle);
            outcomeOverlay.Add(outcomeReason);

            background.Add(countdownScrim);
            background.Add(countdownOverlay);
            background.Add(overtimeBanner);
            background.Add(outcomeOverlay);

            root.Add(background);
        }
    }
}
