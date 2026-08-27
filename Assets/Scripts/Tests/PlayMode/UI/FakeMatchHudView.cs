using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Views;

namespace GooGalaxy.Tests.PlayMode.UI
{
    // Hand-written double per the testing rules: records every call MatchHudPresenter makes so a test can
    // assert on the exact state pushed, the order calls arrived in, or that none arrived at all. Shared by
    // MatchHudPresenterTests, GameLifetimeScopeTests and PveLifetimeScopeTests rather than copied into each —
    // the two DI-wiring fixtures pass one only to keep MatchHudPresenter's mandatory Start() from logging
    // UiLogMessages.HudViewMissing, and never read CallLog or any other recorded state; MatchHudPresenterTests
    // is the one that asserts against it.
    internal sealed class FakeMatchHudView : IMatchHudView
    {
        public readonly List<string> CallLog = new();
        public readonly HandSlotState[] HandSlots = new HandSlotState[HudSelectors.HandSlotCount];
        public readonly bool[] HandSlotAffordability = new bool[HudSelectors.HandSlotCount];

        public event Action PanelInitialized;

        public bool IsPanelReady { get; set; } = true;

        public bool? IsHudVisible { get; private set; }

        public int LocalPlayerId { get; private set; }

        public int OpponentPlayerId { get; private set; }

        public int? TimerSeconds { get; private set; }

        public bool? IsTimerUrgent { get; private set; }

        public string OpponentLabel { get; private set; }

        public int LocalScore { get; private set; }

        public int OpponentScore { get; private set; }

        public bool? IsOpponentScoreVisible { get; private set; }

        public EnergyGaugeState EnergyState { get; private set; } = EnergyGaugeState.Empty;

        public int SetEnergyCallCount { get; private set; }

        public bool IsCatchUpActive { get; private set; }

        public int CatchUpRemainingSeconds { get; private set; }

        public int SetCatchUpCallCount { get; private set; }

        public HandSlotState NextCard { get; private set; } = HandSlotState.Empty;

        public bool? IsCountdownVisible { get; private set; }

        public int CountdownSeconds { get; private set; }

        public bool? IsOvertimeBannerVisible { get; private set; }

        public string OutcomeTitle { get; private set; }

        public string OutcomeReason { get; private set; }

        public bool? IsOutcomeVisible { get; private set; }

        public void SetHudVisible(bool isVisible)
        {
            IsHudVisible = isVisible;
            CallLog.Add(nameof(SetHudVisible));
        }

        public void SetSeats(int localPlayerId, int opponentPlayerId)
        {
            LocalPlayerId = localPlayerId;
            OpponentPlayerId = opponentPlayerId;
            CallLog.Add(nameof(SetSeats));
        }

        public void SetTimerSeconds(int remainingSeconds)
        {
            TimerSeconds = remainingSeconds;
            CallLog.Add(nameof(SetTimerSeconds));
        }

        public void ClearTimer()
        {
            TimerSeconds = null;
            CallLog.Add(nameof(ClearTimer));
        }

        public void SetTimerUrgent(bool isUrgent)
        {
            IsTimerUrgent = isUrgent;
            CallLog.Add(nameof(SetTimerUrgent));
        }

        public void SetOpponentLabel(string label)
        {
            OpponentLabel = label;
            CallLog.Add(nameof(SetOpponentLabel));
        }

        public void SetLocalScore(int unitCount)
        {
            LocalScore = unitCount;
            CallLog.Add(nameof(SetLocalScore));
        }

        public void SetOpponentScore(int unitCount)
        {
            OpponentScore = unitCount;
            CallLog.Add(nameof(SetOpponentScore));
        }

        public void SetOpponentScoreVisible(bool isVisible)
        {
            IsOpponentScoreVisible = isVisible;
            CallLog.Add(nameof(SetOpponentScoreVisible));
        }

        public void SetEnergy(in EnergyGaugeState state)
        {
            EnergyState = state;
            SetEnergyCallCount++;
            CallLog.Add(nameof(SetEnergy));
        }

        public void SetCatchUp(bool isActive, int remainingSeconds)
        {
            IsCatchUpActive = isActive;
            CatchUpRemainingSeconds = remainingSeconds;
            SetCatchUpCallCount++;
            CallLog.Add($"{nameof(SetCatchUp)}:{isActive}");
        }

        public void SetHandSlot(int slotIndex, in HandSlotState state)
        {
            HandSlots[slotIndex] = state;
            CallLog.Add(nameof(SetHandSlot));
        }

        public void SetHandSlotAffordable(int slotIndex, bool isAffordable)
        {
            HandSlotAffordability[slotIndex] = isAffordable;
            CallLog.Add(nameof(SetHandSlotAffordable));
        }

        public void SetNextCard(in HandSlotState state)
        {
            NextCard = state;
            CallLog.Add(nameof(SetNextCard));
        }

        public void ClearHand()
        {
            CallLog.Add(nameof(ClearHand));
        }

        public void SetCountdownVisible(bool isVisible)
        {
            IsCountdownVisible = isVisible;
            CallLog.Add(nameof(SetCountdownVisible));
        }

        public void SetCountdownSeconds(int seconds)
        {
            CountdownSeconds = seconds;
            CallLog.Add(nameof(SetCountdownSeconds));
        }

        public void SetOvertimeBannerVisible(bool isVisible)
        {
            IsOvertimeBannerVisible = isVisible;
            CallLog.Add(nameof(SetOvertimeBannerVisible));
        }

        public void SetOutcome(string title, string reason)
        {
            OutcomeTitle = title;
            OutcomeReason = reason;
            IsOutcomeVisible = true;
            CallLog.Add(nameof(SetOutcome));
        }

        public void ClearOutcome()
        {
            IsOutcomeVisible = false;
            CallLog.Add(nameof(ClearOutcome));
        }
    }
}
