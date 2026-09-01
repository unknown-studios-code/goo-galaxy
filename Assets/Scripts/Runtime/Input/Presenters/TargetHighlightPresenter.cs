using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Views;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Input.Presenters
{
    /// <summary>
    /// Owns which hexes are currently shown as legal targets, and writes only the change onto the board's cells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Only the difference is applied.</b> A pointer dragged across the board asks for a new target set many
    /// times a second, and clearing all 61 cells and re-setting the handful that are legal would write a colour
    /// into every <c>SpriteRenderer</c> on the board on every one of those passes. Diffing against the previous
    /// set turns that into the two or three writes that actually changed.
    /// </para>
    /// <para>
    /// <b>Two sets, swapped rather than rebuilt.</b> The pass fills the spare set, diffs it against the live
    /// one, and then swaps the references — so a whole match of selections allocates the two sets once, at
    /// construction, instead of one per tap.
    /// </para>
    /// <para>
    /// <b>It decides nothing about legality.</b> The targets arrive already enumerated and already filtered; a
    /// hex reaching this component is one a validator has accepted. Nothing is highlighted that the board would
    /// then refuse — and if it were, the commit would still be refused by the board rather than by this.
    /// </para>
    /// <para>
    /// <b>It subscribes to no phase or match-end event, and that is deliberate rather than an oversight.</b>
    /// Every cancellation in a live match routes through <c>MatchInputController.CancelSelection</c>, which
    /// clears the interaction state and calls <see cref="ClearTargets" /> as one step. A second subscription
    /// here — to a phase change or a match end — would be a second cancel path racing the first, and could clear
    /// the highlight while <c>MatchInputController</c> still believed a selection was live, which reads on
    /// screen as a selected unit with no targets. <see cref="HandleMatchStarted" /> is the one exception, because
    /// <c>GridView</c> resets every cell's highlight state under this component when it rebuilds the board for a
    /// new match, without telling this component it did.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class TargetHighlightPresenter : MonoBehaviour
    {
        // A Clone or a Jump reaches at most one ring, and a Deploy footprint at most the empty half of the
        // board. Sized so neither ever grows the set.
        private const int TargetCapacity = BoardMetrics.DefaultBoardCellCount;

        private GridView _gridView;
        private HashSet<HexCoordinates> _highlighted = new(TargetCapacity);
        private HashSet<HexCoordinates> _pending = new(TargetCapacity);

        /// <summary>How many hexes are currently shown as legal targets.</summary>
        public int HighlightedCount => _highlighted.Count;

        [Inject]
        public void Construct(GridView gridView)
        {
            Debug.Assert(gridView != null, InputLogMessages.HighlightGridViewMissing, this);

            _gridView = gridView;
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;

            ClearTargets();
        }

        /// <summary>Reports whether a hex is currently shown as a legal target.</summary>
        /// <param name="coordinates">The hex to test.</param>
        /// <returns>True while that hex is highlighted.</returns>
        public bool IsHighlighted(HexCoordinates coordinates)
        {
            return _highlighted.Contains(coordinates);
        }

        /// <summary>Shows exactly the given hexes as legal targets, and stops showing every other.</summary>
        /// <remarks>
        /// The list is read and never retained, so the caller may reuse the buffer immediately. A duplicate
        /// entry is harmless; a hex the board does not draw a cell for is skipped rather than reported.
        /// </remarks>
        /// <param name="targets">The hexes to highlight. A null or empty list clears the highlight.</param>
        public void SetTargets(IReadOnlyList<HexCoordinates> targets)
        {
            _pending.Clear();

            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    _pending.Add(targets[i]);
                }
            }

            ApplyPendingPass();
        }

        /// <summary>Stops showing every highlighted hex. Safe to call when none is shown.</summary>
        public void ClearTargets()
        {
            _pending.Clear();
            ApplyPendingPass();
        }

        private void ApplyPendingPass()
        {
            ApplyHighlightDifference();

            (_highlighted, _pending) = (_pending, _highlighted);
        }

        private void ApplyHighlightDifference()
        {
            // Both loops bind the concrete HashSet's struct enumerator, so neither boxes one per pass.
            foreach (HexCoordinates coordinates in _pending)
            {
                if (!_highlighted.Contains(coordinates))
                {
                    SetCellHighlight(coordinates, true);
                }
            }

            foreach (HexCoordinates coordinates in _highlighted)
            {
                if (!_pending.Contains(coordinates))
                {
                    SetCellHighlight(coordinates, false);
                }
            }
        }

        private void SetCellHighlight(HexCoordinates coordinates, bool isHighlighted)
        {
            if (_gridView == null || !_gridView.CellViews.TryGetValue(coordinates, out CellView cellView) || cellView == null)
            {
                return;
            }

            cellView.SetHighlightState(isHighlighted);
        }

        // GridView resets every cell's highlight when it rebuilds or re-uses the board for a new match, so the
        // set held here would otherwise describe tints that are already gone and suppress the writes that would
        // restore them.
        private void HandleMatchStarted(MatchConfiguration config)
        {
            ClearTargets();
        }
    }
}
