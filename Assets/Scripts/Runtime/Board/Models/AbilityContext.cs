using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Board.Models
{
    /// <summary>
    /// Everything a landing impact needs to know about the deployment that triggered it, independent of whether
    /// that deployment was a troop move or a Protocol.
    /// </summary>
    /// <remarks>
    /// This type exists so <c>AbilityResolver</c> stops reading a <c>MoveCommand</c>. A Protocol has no
    /// command, no acting unit, and no vacated hex, and every one of those absences used to be expressible only
    /// as a fake move. Each is now a first-class, checkable state: <see cref="HasActingUnit"/>,
    /// <see cref="HasVacatedHex"/>, and <see cref="HasExplicitTargets"/>.
    /// <para>
    /// The two deployment shapes differ in exactly one place — how an impact's area is found.
    /// A troop derives it: <see cref="TargetHexes"/> is null and the area is the spiral of the impact's own
    /// radius around <see cref="OriginHex"/>, so one card can carry impacts of different reaches. A Protocol is
    /// given it: <see cref="TargetHexes"/> holds the hexes the player picked and the impact's radius is a
    /// validation rule rather than an expansion. Everything downstream of the area — the target filter, the
    /// cluster cap, the status application — is identical.
    /// </para>
    /// <para>
    /// <b>Ownership:</b> <see cref="TargetHexes"/> and the lists inside <see cref="Conversions"/> are borrowed
    /// from whoever built the context and are only valid for the duration of the resolution. Nothing retains
    /// them. The type is internal for that reason as much as any other — a caller outside the Board assembly
    /// could otherwise build one, store it, and keep <c>ConversionController</c>'s reusable buffers alive past
    /// the dispatch that owns them.
    /// </para>
    /// </remarks>
    internal readonly struct AbilityContext
    {
        /// <summary>
        /// The value <see cref="ActingUnitId"/> carries when no unit is acting, as on every Protocol. Matches
        /// <see cref="HexCell.NoOccupant"/> so "no unit here" and "no unit acting" read as the same sentinel.
        /// </summary>
        public const int NoActingUnit = HexCell.NoOccupant;

        private AbilityContext(
            int actingPlayerId,
            int actingUnitId,
            HexCoordinates originHex,
            bool hasVacatedHex,
            HexCoordinates vacatedHex,
            bool hasExplicitTargets,
            IReadOnlyList<HexCoordinates> targetHexes,
            ConversionResult conversions
        )
        {
            ActingPlayerId = actingPlayerId;
            ActingUnitId = actingUnitId;
            OriginHex = originHex;
            HasVacatedHex = hasVacatedHex;
            VacatedHex = vacatedHex;
            HasExplicitTargets = hasExplicitTargets;
            TargetHexes = targetHexes;
            Conversions = conversions;
        }

        /// <summary>The player whose deployment triggered the impacts, and the reference point for every target filter.</summary>
        public int ActingPlayerId { get; }

        /// <summary>The unit that landed, or <see cref="NoActingUnit"/> on a Protocol.</summary>
        public int ActingUnitId { get; }

        /// <summary>
        /// The landing hex for a troop, or the cluster centre for a Protocol. A troop impact expands its own
        /// radius from here; a Protocol impact does not expand at all.
        /// </summary>
        public HexCoordinates OriginHex { get; }

        /// <summary>Whether the deployment left a hex empty. True only for a Jump.</summary>
        public bool HasVacatedHex { get; }

        /// <summary>The hex the deployment left empty. Only meaningful while <see cref="HasVacatedHex"/> is true.</summary>
        public HexCoordinates VacatedHex { get; }

        /// <summary>
        /// The hexes the player picked, or null when the impact area is to be derived from
        /// <see cref="OriginHex"/>. Borrowed; never retained.
        /// </summary>
        public IReadOnlyList<HexCoordinates> TargetHexes { get; }

        /// <summary>
        /// What standard conversion did immediately before the impacts, so a
        /// <see cref="TargetFilter.NewlyConverted"/> filter can name exactly those units. Empty on a Protocol
        /// and on a landing that converted nothing. Borrowed; never retained.
        /// </summary>
        public ConversionResult Conversions { get; }

        /// <summary>Whether a unit is acting at all. False on every Protocol.</summary>
        public bool HasActingUnit => ActingUnitId != NoActingUnit;

        /// <summary>
        /// Whether the impact area was handed over rather than derived. True for a Protocol, false for a troop
        /// landing, and the single branch that separates the two deployment shapes.
        /// </summary>
        /// <remarks>
        /// Stored by the factory that built the context, never inferred from whether
        /// <see cref="TargetHexes"/> has entries. Inferring it made a Protocol with no targets indistinguishable
        /// from a troop landing, and a troop landing expands a radius — so an empty spell silently became an
        /// area effect centred on <c>(0, 0)</c>. A spell with no targets must resolve nothing, not something
        /// somewhere else.
        /// </remarks>
        public bool HasExplicitTargets { get; }

        /// <summary>
        /// Builds a context for a troop landing, where every impact derives its own area by expanding its radius
        /// from the landing hex.
        /// </summary>
        /// <param name="actingPlayerId">The player whose deployment this is.</param>
        /// <param name="actingUnitId">
        /// The unit standing on the landing hex. This is not always the commanded unit: a Clone leaves the
        /// commanded unit on its source and puts a brand-new unit on the target.
        /// </param>
        /// <param name="landingHex">The hex the acting unit landed on, and the centre every impact expands from.</param>
        /// <param name="hasVacatedHex">Whether the move left a hex empty. Only a Jump does.</param>
        /// <param name="vacatedHex">The hex the move left empty. Only read when <paramref name="hasVacatedHex"/> is true.</param>
        /// <param name="conversions">What standard conversion did on this landing, borrowed for the resolution.</param>
        /// <returns>A context an impact resolver can run against.</returns>
        public static AbilityContext ForLanding(
            int actingPlayerId,
            int actingUnitId,
            HexCoordinates landingHex,
            bool hasVacatedHex,
            HexCoordinates vacatedHex,
            ConversionResult conversions
        )
        {
            return new AbilityContext(actingPlayerId, actingUnitId, landingHex, hasVacatedHex, vacatedHex, false, null, conversions);
        }

        /// <summary>
        /// Builds a context for a Protocol, where the player-picked cluster is the impact area and no impact
        /// expands beyond it.
        /// </summary>
        /// <remarks>
        /// A Protocol has no acting unit and vacates no hex, so a <see cref="ImpactEffectType.SelfDestruct"/>
        /// or <see cref="ImpactEffectType.SpawnHazard"/> impact authored on one resolves as a no-op and reports
        /// a diagnostic. Nothing was converted either, so <see cref="Conversions"/> is empty and a
        /// <see cref="TargetFilter.NewlyConverted"/> filter selects nobody.
        /// </remarks>
        /// <param name="actingPlayerId">The player deploying the Protocol.</param>
        /// <param name="targetHexes">
        /// The hexes the player picked, centre first and already validated. Borrowed for the resolution. A null
        /// or empty list yields a context that resolves nothing, never one that falls back to an area.
        /// </param>
        /// <returns>A context an impact resolver can run against.</returns>
        public static AbilityContext ForSpell(int actingPlayerId, IReadOnlyList<HexCoordinates> targetHexes)
        {
            HexCoordinates centre = targetHexes != null && targetHexes.Count > 0 ? targetHexes[0] : default;

            return new AbilityContext(actingPlayerId, NoActingUnit, centre, false, default, true, targetHexes, default);
        }
    }
}
