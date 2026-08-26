using System;
using GooGalaxy.Runtime.UI.Models;

namespace GooGalaxy.Runtime.UI.Views
{
    /// <summary>
    /// The match HUD's View contract: a sink of state that has already been resolved, formatted and decided
    /// elsewhere.
    /// </summary>
    /// <remarks>
    /// <b>It decides nothing.</b> Every member states what the screen now shows, never what it should work out
    /// for itself — which phase hides the panel, which seat is the home side, whether a slot is affordable and
    /// what a countdown means are all settled by <c>MatchHudPresenter</c> before a call arrives here. No
    /// gameplay type beyond <c>Shared.Types</c> appears in these signatures, which is what keeps the Cards,
    /// Deck, Energy and Match assemblies out of the rendering layer entirely.
    /// <para>
    /// <b>An implementation may legitimately drop every call.</b> A UI Toolkit panel does not exist until its
    /// <c>UIDocument</c> has built one, and the presenter is deliberately ordered ahead of the view, so the
    /// opening snapshot is normally written into a screen that has nothing to write it into yet. Dropping those
    /// calls is correct behaviour, not a failure: <see cref="IsPanelReady" /> reports the state and
    /// <see cref="PanelInitialized" /> is the signal to push the snapshot again. A caller must therefore never
    /// treat a setter as having taken effect, and must keep the state it pushed.
    /// </para>
    /// <para>
    /// <b>Every setter sits on a repeating path.</b> Energy publishes on <c>EnergyPresenter</c>'s regeneration
    /// quantum and the clock once a second, so an implementation allocates nothing per call — the <c>in</c>
    /// parameters exist so a state struct crosses the seam without a copy, and the two text setters take strings
    /// the caller has already cached.
    /// </para>
    /// </remarks>
    public interface IMatchHudView
    {
        /// <summary>Raised once the implementation can render, and again after every teardown and rebuild.</summary>
        /// <remarks>
        /// A subscriber that also tests <see cref="IsPanelReady" /> when it subscribes cannot miss the first
        /// one, whichever order the two components were enabled in.
        /// </remarks>
        public event Action PanelInitialized;

        /// <summary>Whether the implementation is able to render — for UI Toolkit, whether its panel exists.</summary>
        public bool IsPanelReady { get; }

        /// <summary>Shows or hides the whole HUD, taking it out of layout entirely while hidden.</summary>
        /// <param name="isVisible">Whether the HUD draws at all. False is what the loading phase shows.</param>
        public void SetHudVisible(bool isVisible);

        /// <summary>Names the two seats, so each score can be drawn in the faction colour of its player.</summary>
        /// <param name="localPlayerId">The seat drawn on the home side.</param>
        /// <param name="opponentPlayerId">The seat drawn as the opponent.</param>
        public void SetSeats(int localPlayerId, int opponentPlayerId);

        /// <summary>Draws the seconds left in the running phase.</summary>
        /// <param name="remainingSeconds">Whole seconds left, exactly as the match clock published them.</param>
        public void SetTimerSeconds(int remainingSeconds);

        /// <summary>Blanks the timer, for a match that has not started or was abandoned before it did.</summary>
        /// <remarks>Must not render a zero: a match that never ran has not run out of time.</remarks>
        public void ClearTimer();

        /// <summary>Switches the timer between its ordinary and its running-out treatment.</summary>
        /// <param name="isUrgent">Whether the clock is inside the last stretch of the phase.</param>
        public void SetTimerUrgent(bool isUrgent);

        /// <summary>Names the opponent.</summary>
        /// <param name="label">The text to draw, already resolved from what drives that seat.</param>
        public void SetOpponentLabel(string label);

        /// <summary>Draws the live unit count of the local player.</summary>
        /// <param name="unitCount">The count to draw.</param>
        public void SetLocalScore(int unitCount);

        /// <summary>Draws the live unit count of the opponent.</summary>
        /// <param name="unitCount">The count to draw.</param>
        public void SetOpponentScore(int unitCount);

        /// <summary>Shows or hides the score of the opponent, leaving everything around it where it was.</summary>
        /// <param name="isVisible">Whether the score draws.</param>
        /// <remarks>
        /// The space it occupied is preserved, so turning it off for a capture cannot shift the timer beside it.
        /// </remarks>
        public void SetOpponentScoreVisible(bool isVisible);

        /// <summary>Draws one frame of Energy for the local player.</summary>
        /// <param name="state">Fill, readout and border state, already resolved by the caller.</param>
        public void SetEnergy(in EnergyGaugeState state);

        /// <summary>Shows or hides the catch-up line, and states the window it reports.</summary>
        /// <param name="isActive">Whether a catch-up window is open for the local player.</param>
        /// <param name="remainingSeconds">Seconds the window was opened with. Ignored while inactive.</param>
        /// <remarks>
        /// The value is not counted down: the bus publishes a catch-up window only when it opens or closes, so a
        /// ticking number here would be invented rather than reported.
        /// </remarks>
        public void SetCatchUp(bool isActive, int remainingSeconds);

        /// <summary>Draws a card into one hand slot, or empties it.</summary>
        /// <param name="slotIndex">Zero-based slot. An index the screen does not author is dropped.</param>
        /// <param name="state">The card to draw. <see cref="HandSlotState.Empty" /> empties the slot.</param>
        public void SetHandSlot(int slotIndex, in HandSlotState state);

        /// <summary>Reports whether one hand slot can be paid for right now.</summary>
        /// <param name="slotIndex">Zero-based slot. An index the screen does not author is dropped.</param>
        /// <param name="isAffordable">Whether a Deploy priced at that card cost is within the balance.</param>
        /// <remarks>
        /// Separate from <see cref="SetHandSlot" /> because it flips as Energy regenerates, several times a
        /// second, while the card in the slot only changes when the hand rotates.
        /// </remarks>
        public void SetHandSlotAffordable(int slotIndex, bool isAffordable);

        /// <summary>Draws the card queued for the next freed slot.</summary>
        /// <param name="state">The card to draw. <see cref="HandSlotState.Empty" /> empties the slot.</param>
        public void SetNextCard(in HandSlotState state);

        /// <summary>Empties every hand slot and the queued next card, and undims them all.</summary>
        public void ClearHand();

        /// <summary>Shows or hides the pre-match countdown, scrim and numeral together.</summary>
        /// <param name="isVisible">Whether the countdown is being shown.</param>
        /// <remarks>
        /// Whatever the implementation dims, it must not block input: the domain already refuses every play
        /// outside the two played phases, and a second place for that rule to live is a second place for it to
        /// be wrong.
        /// </remarks>
        public void SetCountdownVisible(bool isVisible);

        /// <summary>Draws the countdown value.</summary>
        /// <param name="seconds">Seconds left before play opens.</param>
        public void SetCountdownSeconds(int seconds);

        /// <summary>Shows or hides the transient banner that announces overtime.</summary>
        /// <param name="isVisible">Whether the banner draws.</param>
        public void SetOvertimeBannerVisible(bool isVisible);

        /// <summary>Shows the end-of-match result and the reason for it.</summary>
        /// <param name="title">The headline — won, lost, or drawn — already resolved for the local seat.</param>
        /// <param name="reason">One line saying what ended the match. Empty draws no reason line.</param>
        public void SetOutcome(string title, string reason);

        /// <summary>Hides the end-of-match result, for the results screen taking over.</summary>
        public void ClearOutcome();
    }
}
