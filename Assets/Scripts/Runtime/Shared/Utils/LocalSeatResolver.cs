using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Shared.Utils
{
    /// <summary>
    /// Decides which of a match's two seats belongs to the person holding this device, and which is the
    /// opponent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every system that has to know "which side am I" — the HUD, the input layer, anything that filters the
    /// board by owner — asks this rather than answering it again, because a second copy of the rule is a second
    /// place for the two to disagree about whose units a tap may command.
    /// </para>
    /// <para>
    /// <b>The seat is read from the announced configuration, never assumed.</b> Both seats can be local (a
    /// hot-seat match, where the first seat is home) and neither can be (a machine-versus-machine debug match,
    /// which still yields a usable pair and reports the failure through the return value). Real player ids start
    /// at one, so <see cref="PlayerSlot.UnassignedId" /> is substituted rather than passed on — an id of zero
    /// addresses no player and would silently filter every unit out.
    /// </para>
    /// <para>
    /// Logs nothing. A caller that wants to report an unresolved seat owns that message, because only the caller
    /// has a <c>UnityEngine.Object</c> worth attaching it to. Allocation-free on every path.
    /// </para>
    /// </remarks>
    public static class LocalSeatResolver
    {
        /// <summary>The id the home side falls back to when the configuration names none.</summary>
        public const int FallbackHomePlayerId = 1;

        /// <summary>The id the opponent falls back to when the configuration names none.</summary>
        public const int FallbackAwayPlayerId = 2;

        /// <summary>Resolves which seat is the local player's and which is the opponent's.</summary>
        /// <remarks>
        /// <para>
        /// <see cref="PlayerSlot.Control" /> decides, checking <see cref="MatchConfiguration.PlayerOne" /> and
        /// then <see cref="MatchConfiguration.PlayerTwo" /> for <see cref="PlayerControl.LocalHuman" />. With
        /// neither driven locally, the first seat is treated as home and false is returned — <b>both outputs are
        /// still filled</b> on that path, so a caller may use them without branching on the result.
        /// </para>
        /// <para>
        /// <b>Whole seats rather than bare ids</b>, because the two facts a caller needs are decided by the same
        /// branch: which side is home, and what drives the other one. Handing back only ids sent the one caller
        /// that also needs <see cref="PlayerSlot.Control" /> back to re-deriving the seat order for itself, which
        /// is the duplication this type exists to remove. Take the id from <see cref="PlayerSlot.Id" />.
        /// </para>
        /// <para>
        /// The returned seats carry their authored <see cref="PlayerSlot.Control" /> unchanged, but a
        /// <see cref="PlayerSlot.UnassignedId" /> id is replaced by the matching fallback — so a seat may read as
        /// "plays as 1, driven by nothing", which is exactly what an unnamed seat means.
        /// </para>
        /// </remarks>
        /// <param name="config">The configuration the match was announced with.</param>
        /// <param name="home">The seat the local player holds. Its id is never <see cref="PlayerSlot.UnassignedId" />.</param>
        /// <param name="away">The opposing seat. Its id is never <see cref="PlayerSlot.UnassignedId" />.</param>
        /// <returns>True when a <see cref="PlayerControl.LocalHuman" /> seat was found; false when the pair is a fallback.</returns>
        public static bool TryResolve(in MatchConfiguration config, out PlayerSlot home, out PlayerSlot away)
        {
            PlayerSlot one = config.PlayerOne;
            PlayerSlot two = config.PlayerTwo;

            if (one.Control == PlayerControl.LocalHuman)
            {
                ApplySeats(one, two, out home, out away);

                return true;
            }

            if (two.Control == PlayerControl.LocalHuman)
            {
                ApplySeats(two, one, out home, out away);

                return true;
            }

            ApplySeats(one, two, out home, out away);

            return false;
        }

        private static void ApplySeats(PlayerSlot resolvedHome, PlayerSlot resolvedAway, out PlayerSlot home, out PlayerSlot away)
        {
            home = WithFallbackId(resolvedHome, FallbackHomePlayerId);
            away = WithFallbackId(resolvedAway, FallbackAwayPlayerId);
        }

        // Rebuilt rather than mutated: PlayerSlot is a readonly struct, and keeping the authored Control is what
        // lets a caller still tell a machine opponent from a remote one on the fallback path.
        private static PlayerSlot WithFallbackId(PlayerSlot slot, int fallbackId)
        {
            return slot.Id == PlayerSlot.UnassignedId ? new PlayerSlot(fallbackId, slot.Control) : slot;
        }
    }
}
