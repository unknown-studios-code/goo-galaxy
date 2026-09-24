using System.Collections.Generic;
using GooGalaxy.Runtime.Input.Models;
using GooGalaxy.Runtime.Match.Models;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Input.Services
{
    /// <summary>
    /// Filters a resolved option set down to what one live selection can act on, and looks a released target back
    /// up in that same set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The Protocol filter and the per-path filter live in one place</b>, so the set a selection highlights and
    /// the set a commit is looked up in cannot drift apart. Every <see cref="MoveOptionKind.Protocol" /> option is
    /// dropped before anything reads one, because cluster targeting is out of scope for the MVP; a board move is
    /// then kept only when it belongs to the selection's own unit (<see cref="InteractionSourceKind.BoardUnit" />)
    /// or its own hand slot (<see cref="InteractionSourceKind.HandSlot" />).
    /// </para>
    /// <para>
    /// Stateless and allocation-free: every method scans its input with an indexed <c>for</c> loop and writes only
    /// into a buffer the caller already owns.
    /// </para>
    /// </remarks>
    public static class SelectionTargetResolver
    {
        /// <summary>Whether <paramref name="option" /> is one a selection started from <paramref name="source" /> can highlight or commit.</summary>
        /// <param name="option">The option under test.</param>
        /// <param name="source">The live selection to test it against.</param>
        /// <returns>
        /// <see langword="false" /> for every <see cref="MoveOptionKind.Protocol" /> option; otherwise whether the
        /// option's unit or hand slot matches the one <paramref name="source" /> was started from.
        /// </returns>
        public static bool IsOptionForSource(in MoveOption option, in InteractionSource source)
        {
            if (option.Kind != MoveOptionKind.BoardMove)
            {
                return false;
            }

            if (source.Kind == InteractionSourceKind.BoardUnit)
            {
                return option.UnitId == source.UnitId;
            }

            if (source.Kind == InteractionSourceKind.HandSlot)
            {
                return option.MoveType == MoveType.Deploy && option.SlotIndex == source.SlotIndex;
            }

            return false;
        }

        /// <summary>Fills <paramref name="targets" /> with the target hex of every option in <paramref name="options" /> that matches <paramref name="source" />.</summary>
        /// <remarks>Clears <paramref name="targets" /> first, so it always ends up holding exactly this call's result.</remarks>
        /// <param name="options">The resolved option set to filter.</param>
        /// <param name="source">The live selection to filter for.</param>
        /// <param name="targets">The buffer to clear and fill. Owned by the caller.</param>
        public static void CollectTargets(IReadOnlyList<MoveOption> options, in InteractionSource source, List<HexCoordinates> targets)
        {
            targets.Clear();

            for (int i = 0; i < options.Count; i++)
            {
                MoveOption option = options[i];

                if (IsOptionForSource(in option, in source))
                {
                    targets.Add(option.Target);
                }
            }
        }

        /// <summary>Finds the option a release onto <paramref name="target" /> should commit, for the live selection <paramref name="source" />.</summary>
        /// <remarks>
        /// Scanned rather than keyed by target, so no dictionary is rebuilt per enumeration. The first match
        /// wins, and the enumerator adds a unit's Clone options ahead of its Jump options. The two can only
        /// share a target when a capability authors equal Clone and Jump distances — never at the defaults of 1
        /// and 2 — and then the Clone commits. That tie-break is this layer's choice (a Clone nets +1 unit
        /// against a Jump's +0), not one the GDD states.
        /// </remarks>
        /// <param name="options">The resolved option set to search.</param>
        /// <param name="source">The live selection the found option must match.</param>
        /// <param name="target">The hex the release landed on.</param>
        /// <param name="option">The matching option, or <see langword="default" /> when none was found.</param>
        /// <returns><see langword="true" /> when a matching option was found.</returns>
        public static bool TryFindOptionForTarget(IReadOnlyList<MoveOption> options, in InteractionSource source, HexCoordinates target, out MoveOption option)
        {
            option = default;

            for (int i = 0; i < options.Count; i++)
            {
                MoveOption candidate = options[i];

                if (candidate.Target != target || !IsOptionForSource(in candidate, in source))
                {
                    continue;
                }

                option = candidate;

                return true;
            }

            return false;
        }
    }
}
