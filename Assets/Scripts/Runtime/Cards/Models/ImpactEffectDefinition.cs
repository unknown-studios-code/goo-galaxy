using System;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Models
{
    /// <summary>
    /// Inspector-facing authoring form of a single landing impact. Converted to the immutable runtime
    /// <see cref="ImpactEffect"/> once, when the owning card asset first publishes its impacts.
    /// </summary>
    /// <remarks>
    /// A mutable <c>struct</c> rather than the project's usual <c>readonly struct</c> because Unity's serializer
    /// cannot write to init-only fields or auto-properties: an authoring type has to be writable to appear in
    /// the Inspector at all. It exists only so the runtime type does not have to compromise — nothing outside
    /// authoring and <see cref="ToImpactEffect"/> should hold one.
    /// </remarks>
    [Serializable]
    public struct ImpactEffectDefinition
    {
        [Tooltip("What this impact does. None resolves as a no-op; leaving it at None is how a card ships an unfinished effect by accident.")]
        [SerializeField]
        private ImpactEffectType _type;

        [Tooltip("Condition applied to every selected unit. Only read by Apply Status; the other impact types ignore it.")]
        [SerializeField]
        private StatusType _status;

        [Tooltip("Hex rings around the landing hex this impact reaches. 0 covers the landing hex alone, 1 adds its 6 neighbours, 2 adds another 12.")]
        [Range(0, BoardMetrics.MaxConversionRadius)]
        [SerializeField]
        private int _radius;

        [Tooltip("How long the result lasts, in action windows: defender windows for a status, owner windows for a hazard. 0 makes the impact a no-op.")]
        [Min(0)]
        [SerializeField]
        private int _duration;

        [Tooltip("Which units inside the radius are affected. Self is the unit that just landed; All hits friendly units too, as Cryo-Stasis does.")]
        [SerializeField]
        private TargetFilter _target;

        [Tooltip(
            "Ceiling on affected units, or 0 for no ceiling. Cryo-Stasis uses 3 so its authored cluster cannot widen when the radius finds more occupied hexes."
        )]
        [Min(0)]
        [SerializeField]
        private int _clusterSize;

        /// <summary>Builds one authored impact from explicit values, for tests and editor tooling.</summary>
        /// <param name="type">What the impact does.</param>
        /// <param name="status">The condition to apply, for an Apply Status impact.</param>
        /// <param name="radius">Hex rings around the landing hex the impact reaches.</param>
        /// <param name="duration">How long the result lasts, in action windows.</param>
        /// <param name="target">Which units inside the radius are affected.</param>
        /// <param name="clusterSize">Ceiling on affected units, or zero for no ceiling.</param>
        public ImpactEffectDefinition(ImpactEffectType type, StatusType status, int radius, int duration, TargetFilter target, int clusterSize)
        {
            _type = type;
            _status = status;
            _radius = radius;
            _duration = duration;
            _target = target;
            _clusterSize = clusterSize;
        }

        /// <summary>Produces the immutable runtime impact this authored value describes.</summary>
        /// <returns>The runtime impact, carrying exactly the authored values.</returns>
        public ImpactEffect ToImpactEffect()
        {
            return new ImpactEffect(_type, _status, _radius, _duration, _target, _clusterSize);
        }
    }
}
