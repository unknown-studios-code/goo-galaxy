using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;

namespace GooGalaxy.Runtime.Match.Services
{
    /// <remarks>
    /// The match score, and the whole of it: per the GDD a player's score <i>is</i> the number of units they
    /// hold on the board, so there is nothing to accumulate and nothing to keep in step — every count is
    /// derived from the registry at the moment it is asked for.
    /// <para>
    /// <b>Allocation-free on every path, by construction.</b> Both overloads run after every resolved
    /// deployment, which is a path budgeted at zero bytes, so each pass takes
    /// <c>UnitPresenter.ActiveUnitValues</c> and never <c>ActiveUnits.Values</c>: the interface-typed collection
    /// boxes its backing struct enumerator, one allocation per pass. Nothing else here allocates either — no
    /// buffer, no closure, no formatted string.
    /// </para>
    /// <para>
    /// The two-player overload exists because a match settles both counts together; the single-player one stays
    /// as the primitive a caller with one side to count — or a test — reaches for.
    /// </para>
    /// </remarks>
    internal static class MatchScoreCounter
    {
        /// <remarks>
        /// A unit that is registered but no longer alive is not counted. Removal is two steps in the board's
        /// controllers — the unit is marked dead, then unregistered — so a caller that reads the registry
        /// between them would otherwise credit a unit that is already gone. A missing or destroyed registry
        /// counts as zero rather than throwing.
        /// </remarks>
        internal static int CountLiveUnits(UnitPresenter unitPresenter, int playerId)
        {
            if (unitPresenter == null)
            {
                return 0;
            }

            int count = 0;

            foreach (GridUnit unit in unitPresenter.ActiveUnitValues)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (unit.PlayerId == playerId)
                {
                    count++;
                }
            }

            return count;
        }

        /// <remarks>
        /// Both counts off <b>one</b> walk of the registry, under the same live-unit rule and the same
        /// zero-allocation guarantee as the single-player overload. A match settles two counts at once — on
        /// every deployment, and again the instant the clock runs out — and taking them from one pass is what
        /// keeps them describing the same board rather than two consecutive reads of it.
        /// <para>
        /// The two ids are assumed distinct, as a match's two sides always are: a unit is counted once, into the
        /// first id it matches.
        /// </para>
        /// </remarks>
        internal static void CountLiveUnits(
            UnitPresenter unitPresenter,
            int firstPlayerId,
            int secondPlayerId,
            out int firstPlayerUnits,
            out int secondPlayerUnits
        )
        {
            firstPlayerUnits = 0;
            secondPlayerUnits = 0;

            if (unitPresenter == null)
            {
                return;
            }

            foreach (GridUnit unit in unitPresenter.ActiveUnitValues)
            {
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                if (unit.PlayerId == firstPlayerId)
                {
                    firstPlayerUnits++;
                    continue;
                }

                if (unit.PlayerId == secondPlayerId)
                {
                    secondPlayerUnits++;
                }
            }
        }
    }
}
