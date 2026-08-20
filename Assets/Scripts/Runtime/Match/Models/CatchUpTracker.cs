namespace GooGalaxy.Runtime.Match.Models
{
    /// <remarks>
    /// Drives one <see cref="CatchUpWindow" /> per player against the live unit counts, which is the whole of
    /// the catch-up Energy bonus: a player at or below the authored threshold share of the board's total units
    /// opens their window, and <see cref="CatchUpWindow" /> owns how long it stays open and how long it locks
    /// out afterward.
    /// <para>
    /// <b>An empty board activates nobody.</b> Both counts are zero before the board is seeded, and a
    /// Sterilization Beam can drive them there mid-match. An empty board is not a deficit — the ratio it would
    /// imply is undefined — so <see cref="IsBelowThreshold" /> guards on the total before testing either count.
    /// </para>
    /// <para>
    /// <b>The test multiplies rather than divides</b> — <c>playerUnits &lt;= totalUnits * thresholdRatio</c> —
    /// which is a deliberate choice, not an incidental one. Float imprecision on the multiplication then errs
    /// toward activating rather than away from it (5 units at a 0.4 ratio computes to roughly 2.0000000298, so
    /// 2-of-5 still passes), and erring toward activation is the safe direction for a mechanic whose entire job
    /// is to open, not to withhold.
    /// </para>
    /// <para>
    /// <b>Two independent windows, even though at most one player can be below threshold at a time.</b> The two
    /// counts sum to the total, so a player at or below the threshold share forces the other above the
    /// complement — which is why <see cref="CatchUpConfig.MaxThresholdRatio" /> stops just under a half rather
    /// than at it, since an even split at exactly 0.5 would satisfy both sides. But the lead swings over a
    /// match: a player who was boosted and then pulled ahead has to keep serving their own cooldown when they
    /// fall behind again later, and a tracker with only one window could not tell the two apart.
    /// </para>
    /// <para>
    /// Engine-free, and allocation-free on every tick — it runs once per frame of play. The two windows are
    /// the only allocation, taken once when the tracker is constructed and never repeated, which is why
    /// <see cref="Reset" /> returns them to Idle rather than replacing them.
    /// </para>
    /// </remarks>
    internal sealed class CatchUpTracker
    {
        private readonly CatchUpWindow _playerOneWindow = new();
        private readonly CatchUpWindow _playerTwoWindow = new();

        internal static bool IsBelowThreshold(int playerUnits, int totalUnits, float thresholdRatio)
        {
            if (totalUnits == 0)
            {
                return false;
            }

            return playerUnits <= (totalUnits * thresholdRatio);
        }

        internal void Tick(
            int playerOneUnits,
            int playerTwoUnits,
            float deltaTime,
            in CatchUpConfig config,
            out bool isPlayerOneActive,
            out bool isPlayerTwoActive
        )
        {
            int totalUnits = playerOneUnits + playerTwoUnits;

            bool isPlayerOneBelowThreshold = IsBelowThreshold(playerOneUnits, totalUnits, config.ThresholdRatio);
            bool isPlayerTwoBelowThreshold = IsBelowThreshold(playerTwoUnits, totalUnits, config.ThresholdRatio);

            isPlayerOneActive = _playerOneWindow.Tick(isPlayerOneBelowThreshold, deltaTime, config);
            isPlayerTwoActive = _playerTwoWindow.Tick(isPlayerTwoBelowThreshold, deltaTime, config);
        }

        /// <remarks>
        /// Returns both windows to the state a fresh tracker holds, so the next match measures its first
        /// deficit from Idle rather than from whatever the previous match left behind.
        /// </remarks>
        internal void Reset()
        {
            _playerOneWindow.Reset();
            _playerTwoWindow.Reset();
        }
    }
}
