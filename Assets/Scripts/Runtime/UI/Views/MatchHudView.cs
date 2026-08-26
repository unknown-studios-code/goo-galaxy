using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Views.Elements;
using UnityEngine.UIElements;

namespace GooGalaxy.Runtime.UI.Views
{
    /// <summary>
    /// The in-match HUD's rendering surface: it caches every element the panel declares and exposes one typed
    /// setter per thing the screen can show.
    /// </summary>
    /// <remarks>
    /// <b>It decides nothing.</b> No match event reaches it, no domain type beyond <c>Shared.Types</c> is named
    /// in its signatures, and every rule about which phase shows what lives in <c>MatchHudPresenter</c>. What
    /// looks like a decision here — skipping a text write whose value is unchanged — is redraw suppression, and
    /// it changes what is drawn in no case.
    /// <para>
    /// <b>Every setter is safe before the panel exists.</b> A presenter placed ahead of this component by
    /// execution order will call into it during its own <c>OnEnable</c>; those calls are dropped, and the
    /// presenter pushes a full snapshot again when <c>PanelInitialized</c> tells it the panel is up. That is the
    /// dropped-call licence <see cref="IMatchHudView" /> grants, exercised.
    /// </para>
    /// <para>
    /// <b>The members are public and non-virtual on purpose.</b> <see cref="IMatchHudView" /> is the seam a
    /// presenter is written against and a test double implements, so a presenter fixture needs no
    /// <c>UIDocument</c>, no panel and no subclass of this component. That fixture is still a PlayMode one,
    /// because the presenter is only reachable through <c>OnEnable</c> — what the seam removes is the panel,
    /// not the play mode.
    /// </para>
    /// <para>
    /// <b>Not one member here carries an XML doc.</b> Every public member implements
    /// <see cref="IMatchHudView" />, which documents the whole contract; restating it would give the two
    /// wordings somewhere to drift apart, which is what they had already started doing.
    /// </para>
    /// </remarks>
    public class MatchHudView : UIToolkitView, IMatchHudView
    {
        // No catch-up window has been drawn. Negative, so the first real window always composes its line.
        private const int NoDrawnCatchUp = -1;

        private readonly CardSlotElement[] _handSlots = new CardSlotElement[HudSelectors.HandSlotCount];

        private VisualElement _background;
        private Label _timerLabel;
        private OpponentBadgeElement _opponentBadge;
        private ScoreBadgeElement _opponentScore;
        private ScoreBadgeElement _localScore;
        private Label _catchUpLine;
        private EnergyGaugeElement _energyGauge;
        private CardSlotElement _nextCardSlot;
        private VisualElement _countdownScrim;
        private CountdownOverlayElement _countdownOverlay;
        private VisualElement _overtimeBanner;
        private VisualElement _outcomeOverlay;
        private Label _outcomeTitle;
        private Label _outcomeReason;
        private int _drawnCatchUpSeconds = NoDrawnCatchUp;

        public void SetHudVisible(bool isVisible)
        {
            SetElementVisible(_background, isVisible);
        }

        public void SetSeats(int localPlayerId, int opponentPlayerId)
        {
            if (!IsPanelReady)
            {
                return;
            }

            _localScore?.SetPlayer(localPlayerId);
            _opponentScore?.SetPlayer(opponentPlayerId);
        }

        public void SetTimerSeconds(int remainingSeconds)
        {
            if (!IsPanelReady || (_timerLabel == null))
            {
                return;
            }

            _timerLabel.text = HudClockFormatter.Format(remainingSeconds);
        }

        public void ClearTimer()
        {
            if (!IsPanelReady || (_timerLabel == null))
            {
                return;
            }

            _timerLabel.text = HudClockFormatter.Blank;
        }

        public void SetTimerUrgent(bool isUrgent)
        {
            if (!IsPanelReady || (_timerLabel == null))
            {
                return;
            }

            _timerLabel.EnableInClassList(HudSelectors.MatchTimerUrgent, isUrgent);
        }

        public void SetOpponentLabel(string label)
        {
            if (!IsPanelReady)
            {
                return;
            }

            _opponentBadge?.SetLabel(label);
        }

        public void SetLocalScore(int unitCount)
        {
            if (!IsPanelReady)
            {
                return;
            }

            _localScore?.SetScore(unitCount);
        }

        public void SetOpponentScore(int unitCount)
        {
            if (!IsPanelReady)
            {
                return;
            }

            _opponentScore?.SetScore(unitCount);
        }

        public void SetOpponentScoreVisible(bool isVisible)
        {
            if (!IsPanelReady || (_opponentScore == null))
            {
                return;
            }

            _opponentScore.EnableInClassList(HudSelectors.IsInvisible, !isVisible);
        }

        public void SetEnergy(in EnergyGaugeState state)
        {
            if (!IsPanelReady || (_energyGauge == null))
            {
                return;
            }

            _energyGauge.SetState(in state);
        }

        public void SetCatchUp(bool isActive, int remainingSeconds)
        {
            if (!IsPanelReady || (_catchUpLine == null))
            {
                return;
            }

            // PERF: composed only when the window it reports moves. Every full push re-states an open window
            // unchanged, and the authored config opens every window at the same duration, so without this the
            // line rebuilds a byte-identical string on each one — two allocations, because Unity carries no
            // interpolated-string handler and the interpolation lowers to a ToString plus a string.Concat.
            if (isActive && (remainingSeconds != _drawnCatchUpSeconds))
            {
                _drawnCatchUpSeconds = remainingSeconds;
                _catchUpLine.text = $"{HudText.CatchUpPrefix}{remainingSeconds}{HudText.CatchUpSuffix}";
            }

            SetElementVisible(_catchUpLine, isActive);
        }

        public void SetHandSlot(int slotIndex, in HandSlotState state)
        {
            if (!IsPanelReady || (slotIndex < 0) || (slotIndex >= _handSlots.Length))
            {
                return;
            }

            _handSlots[slotIndex]?.SetState(in state);
        }

        public void SetHandSlotAffordable(int slotIndex, bool isAffordable)
        {
            if (!IsPanelReady || (slotIndex < 0) || (slotIndex >= _handSlots.Length))
            {
                return;
            }

            _handSlots[slotIndex]?.SetAffordable(isAffordable);
        }

        public void SetNextCard(in HandSlotState state)
        {
            if (!IsPanelReady || (_nextCardSlot == null))
            {
                return;
            }

            _nextCardSlot.SetState(in state);
        }

        public void ClearHand()
        {
            if (!IsPanelReady)
            {
                return;
            }

            for (int i = 0; i < _handSlots.Length; i++)
            {
                _handSlots[i]?.SetState(HandSlotState.Empty);
                _handSlots[i]?.SetAffordable(true);
            }

            _nextCardSlot?.SetState(HandSlotState.Empty);
        }

        public void SetCountdownVisible(bool isVisible)
        {
            SetElementVisible(_countdownScrim, isVisible);
            SetElementVisible(_countdownOverlay, isVisible);
        }

        public void SetCountdownSeconds(int seconds)
        {
            if (!IsPanelReady)
            {
                return;
            }

            _countdownOverlay?.SetSeconds(seconds);
        }

        public void SetOvertimeBannerVisible(bool isVisible)
        {
            SetElementVisible(_overtimeBanner, isVisible);
        }

        public void SetOutcome(string title, string reason)
        {
            if (!IsPanelReady)
            {
                return;
            }

            if (_outcomeTitle != null)
            {
                _outcomeTitle.text = title;
            }

            if (_outcomeReason != null)
            {
                _outcomeReason.text = reason;
            }

            SetElementVisible(_outcomeOverlay, true);
        }

        public void ClearOutcome()
        {
            SetElementVisible(_outcomeOverlay, false);
        }

        protected override void CacheElements(VisualElement root)
        {
            // Reset alongside the references, because the label that carried the drawn text is dropped on every
            // disable and the one cached here is a fresh, empty one.
            _drawnCatchUpSeconds = NoDrawnCatchUp;

            _background = RequireElement<VisualElement>(root, HudSelectors.Background);
            _timerLabel = RequireElement<Label>(root, HudSelectors.MatchTimer);
            _opponentBadge = RequireElement<OpponentBadgeElement>(root, HudSelectors.OpponentBadge);
            _opponentScore = RequireElement<ScoreBadgeElement>(root, HudSelectors.OpponentScore);
            _localScore = RequireElement<ScoreBadgeElement>(root, HudSelectors.LocalScore);
            _catchUpLine = RequireElement<Label>(root, HudSelectors.CatchUpLine);
            _energyGauge = RequireElement<EnergyGaugeElement>(root, HudSelectors.EnergyGauge);
            _nextCardSlot = RequireElement<CardSlotElement>(root, HudSelectors.NextCardSlot);
            _countdownScrim = RequireElement<VisualElement>(root, HudSelectors.CountdownScrim);
            _countdownOverlay = RequireElement<CountdownOverlayElement>(root, HudSelectors.CountdownOverlay);
            _overtimeBanner = RequireElement<VisualElement>(root, HudSelectors.OvertimeBanner);
            _outcomeOverlay = RequireElement<VisualElement>(root, HudSelectors.OutcomeOverlay);
            _outcomeTitle = RequireElement<Label>(root, HudSelectors.OutcomeTitle);
            _outcomeReason = RequireElement<Label>(root, HudSelectors.OutcomeReason);

            for (int i = 0; i < _handSlots.Length; i++)
            {
                _handSlots[i] = RequireElement<CardSlotElement>(root, HudSelectors.GetHandSlotName(i));
            }
        }

        protected override void RegisterCallbacks()
        {
            // Nothing yet, deliberately: this task renders the HUD and raises no intent from it.
            // TODO (GOOM-17): register the hand, emote and card-detail gestures here.
        }

        protected override void UnregisterCallbacks()
        {
            // Symmetric with RegisterCallbacks, which registers nothing yet.
        }

        private void SetElementVisible(VisualElement element, bool isVisible)
        {
            if (!IsPanelReady || (element == null))
            {
                return;
            }

            element.EnableInClassList(HudSelectors.IsHidden, !isVisible);
        }
    }
}
