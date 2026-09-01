using System;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Views.Elements;
using UnityEngine;
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
    /// <b>No member here restates a contract.</b> Every public member implements <see cref="IMatchHudView" /> or
    /// <see cref="IHandGestureSource" />, each of which documents its own contract, and restating either would
    /// give the two wordings somewhere to drift apart, which is what they had already started doing. The one
    /// exception is the <c>&lt;remarks&gt;</c> on <see cref="OnHandSlotPressed" />, which states an override
    /// obligation neither interface can express.
    /// </para>
    /// <para>
    /// <b>The gesture surface is a second, narrower interface rather than an addition to <see cref="IMatchHudView" />.</b>
    /// That interface is documented as a pure sink of already-decided state, and a hand-slot press is the
    /// opposite of that — an intent the view raises rather than a value it renders. Keeping the two contracts
    /// apart keeps a presenter fixture built against <see cref="IMatchHudView" /> free of gesture concerns it
    /// never asked for.
    /// </para>
    /// </remarks>
    public class MatchHudView : UIToolkitView, IMatchHudView, IHandGestureSource
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
        private VisualElement _discardZone;
        private CardSlotElement _nextCardSlot;
        private VisualElement _countdownScrim;
        private CountdownOverlayElement _countdownOverlay;
        private VisualElement _overtimeBanner;
        private VisualElement _outcomeOverlay;
        private Label _outcomeTitle;
        private Label _outcomeReason;
        private int _drawnCatchUpSeconds = NoDrawnCatchUp;
        private bool _isDiscardZoneArmed;

        public event Action<int> HandSlotPressed;

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

        public void SetDiscardZoneArmed(bool isArmed)
        {
            _isDiscardZoneArmed = isArmed;

            if (!IsPanelReady || (_discardZone == null))
            {
                return;
            }

            _discardZone.EnableInClassList(HudSelectors.DiscardZoneArmed, isArmed);
        }

        public bool IsScreenPointInDiscardZone(Vector2 screenPosition)
        {
            if (!_isDiscardZoneArmed || !IsPanelReady || (_discardZone == null))
            {
                return false;
            }

            // Panel space is top-left origin and screen space is bottom-left, and ScreenToPanel does NOT
            // reconcile them — measured against a live panel, it scales and nothing more: screen (0,0) comes
            // back as panel (0,0), so the bottom of the screen lands on the top of the panel. The Y is flipped
            // first for that reason. Without the flip this zone sits low on screen but tests high, so a drag into
            // it never registers and a drag to the top of the screen discards instead.
            //
            // Deliberately a second copy of BoardPointerResolver.ToPanelPoint rather than a call to it: that
            // method lives in Runtime.Input, which already references Runtime.UI, so reaching back for it would
            // close a cycle. One line duplicated across an assembly boundary beats an edge that cannot exist —
            // but the two must change together, so fix both or neither.
            var flippedPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(Root.panel, flippedPosition);

            return _discardZone.worldBound.Contains(panelPosition);
        }

        protected override void CacheElements(VisualElement root)
        {
            // Reset alongside the references, because the label that carried the drawn text is dropped on every
            // disable and the one cached here is a fresh, empty one.
            _drawnCatchUpSeconds = NoDrawnCatchUp;

            // Reset here too, not left to the next arm/disarm call: a disable mid-drag leaves this true, and the
            // freshly cloned tree below carries no --armed class, so a stale true would let
            // IsScreenPointInDiscardZone accept a release the zone is not drawing. The caller re-arms through
            // SetDiscardZoneArmed on the next drag, so dropping the flag here costs nothing real.
            _isDiscardZoneArmed = false;

            _background = RequireElement<VisualElement>(root, HudSelectors.Background);
            _timerLabel = RequireElement<Label>(root, HudSelectors.MatchTimer);
            _opponentBadge = RequireElement<OpponentBadgeElement>(root, HudSelectors.OpponentBadge);
            _opponentScore = RequireElement<ScoreBadgeElement>(root, HudSelectors.OpponentScore);
            _localScore = RequireElement<ScoreBadgeElement>(root, HudSelectors.LocalScore);
            _catchUpLine = RequireElement<Label>(root, HudSelectors.CatchUpLine);
            _energyGauge = RequireElement<EnergyGaugeElement>(root, HudSelectors.EnergyGauge);
            _discardZone = RequireElement<VisualElement>(root, HudSelectors.DiscardZone);
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
            for (int i = 0; i < _handSlots.Length; i++)
            {
                _handSlots[i]?.RegisterCallback<PointerDownEvent>(HandleHandSlotPointerDown);
            }
        }

        protected override void UnregisterCallbacks()
        {
            for (int i = 0; i < _handSlots.Length; i++)
            {
                _handSlots[i]?.UnregisterCallback<PointerDownEvent>(HandleHandSlotPointerDown);
            }
        }

        /// <remarks>An override that skips this base call drops the event — no subscriber learns which slot was pressed.</remarks>
        protected virtual void OnHandSlotPressed(int slotIndex)
        {
            HandSlotPressed?.Invoke(slotIndex);
        }

        private void SetElementVisible(VisualElement element, bool isVisible)
        {
            if (!IsPanelReady || (element == null))
            {
                return;
            }

            element.EnableInClassList(HudSelectors.IsHidden, !isVisible);
        }

        private void HandleHandSlotPointerDown(PointerDownEvent evt)
        {
            for (int i = 0; i < _handSlots.Length; i++)
            {
                if (!ReferenceEquals(evt.currentTarget, _handSlots[i]))
                {
                    continue;
                }

                // An empty slot has no card to play, so raising it would start a selection nothing could ever
                // commit — a live CardSelected state with zero highlights that only a later press elsewhere
                // would clear.
                if (_handSlots[i].State.IsFilled)
                {
                    OnHandSlotPressed(i);
                }

                return;
            }
        }
    }
}
