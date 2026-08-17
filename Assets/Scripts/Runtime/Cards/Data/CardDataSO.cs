using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Interfaces;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Types;
using UnityEngine;

namespace GooGalaxy.Runtime.Cards.Data
{
    [CreateAssetMenu(menuName = "Goo Galaxy/Cards/Card Data", fileName = "NewCardData")]
    public class CardDataSO : ScriptableObject, ICardData
    {
        private static readonly ImpactEffect[] _noLandingEffects = Array.Empty<ImpactEffect>();

        [Header("Identity")]
        [Tooltip("Unique, stable identifier used as the lookup key in CardPresenter. Must not be empty.")]
        [SerializeField]
        private string _cardId;

        [Tooltip("Player-facing card name shown in the HUD and card inspector tools.")]
        [SerializeField]
        private string _displayName;

        [Tooltip(
            "Player-facing text for the card face, in one or two short sentences. Plain text — markup renders literally, and there is no localization yet."
        )]
        [TextArea(2, 4)]
        [SerializeField]
        private string _description;

        [Tooltip("Whether this card deploys a troop unit or resolves a one-time spell effect.")]
        [SerializeField]
        private CardType _type;

        [Header("Energy")]
        [Tooltip("Energy cost required to play this card, in whole Energy units.")]
        [SerializeField]
        private int _energyCost = 1;

        [Header("Movement")]
        [Tooltip("Whether this card can Clone at all. How far it clones is the distance authored below.")]
        [SerializeField]
        private bool _canClone;

        [Tooltip("Whether this card can Jump at all. How far it jumps is the distance authored below.")]
        [SerializeField]
        private bool _canJump;

        [Tooltip("Exact hex distance a Clone covers. 1 is standard; a wider reach clones only at that distance, never closer.")]
        [Range(BoardMetrics.MinMoveDistance, BoardMetrics.MaxMoveDistance)]
        [SerializeField]
        private int _cloneDistance = BoardMetrics.DefaultCloneDistance;

        [Tooltip("Exact hex distance a Jump covers. 2 is standard; a wider reach jumps only at that distance, never closer.")]
        [Range(BoardMetrics.MinMoveDistance, BoardMetrics.MaxMoveDistance)]
        [SerializeField]
        private int _jumpDistance = BoardMetrics.DefaultJumpDistance;

        [Tooltip("Hover: whether this card may land on hazardous hexes. Leave off for everything but Plasmic Leaper, or acid puddles stop denying area.")]
        [SerializeField]
        private bool _canIgnoreHazards;

        [Header("Protection")]
        [Tooltip("Whether this card requires two conversion events to flip instead of one.")]
        [SerializeField]
        private bool _hasArmor;

        [Header("Conversion")]
        [Tooltip("Hex rings around the landing whose enemies are converted. 1 is standard; 2 is Volatile Mass and reaches up to 18 units per landing.")]
        [Range(BoardMetrics.DefaultConversionRadius, BoardMetrics.MaxConversionRadius)]
        [SerializeField]
        private int _conversionRadius = BoardMetrics.DefaultConversionRadius;

        [Header("Abilities")]
        [Tooltip("Impacts resolved on landing, in order, after standard conversion. Leave empty for a card whose only rules are its passives.")]
        [SerializeField]
        private ImpactEffectDefinition[] _landingEffects;

        private ImpactEffect[] _cachedLandingEffects;

        public CardId CardId => new(_cardId);

        public string DisplayName => _displayName;

        /// <inheritdoc />
        /// <remarks>
        /// Coalesced on read. Unity's serializer gives a string field an empty value, but
        /// <c>ScriptableObject.CreateInstance</c> does not deserialize at all, so an asset built in code would
        /// otherwise hand back null against a contract that promises empty.
        /// </remarks>
        public string Description => _description ?? string.Empty;

        public CardType Type => _type;

        public int EnergyCost => _energyCost;

        public bool CanClone => _canClone;

        public bool CanJump => _canJump;

        /// <inheritdoc />
        /// <remarks>
        /// An unauthored value resolves to <see cref="BoardMetrics.DefaultCloneDistance" /> rather than to the
        /// shared minimum. <c>[Range]</c> is a drawer and never runs on deserialization, so an asset saved
        /// before this field existed loads as zero; clamping that up would hand back the minimum, which is only
        /// coincidentally the right answer for a Clone and is the wrong one for a Jump.
        /// </remarks>
        public int CloneDistance => ResolveAuthoredDistance(_cloneDistance, BoardMetrics.DefaultCloneDistance);

        /// <inheritdoc />
        /// <remarks>
        /// An unauthored value resolves to <see cref="BoardMetrics.DefaultJumpDistance" />. Clamping it up to
        /// <see cref="BoardMetrics.MinMoveDistance" /> instead would silently turn every Jump on a pre-migration
        /// asset into a one-hex move — wrong, plausible, and invisible.
        /// </remarks>
        public int JumpDistance => ResolveAuthoredDistance(_jumpDistance, BoardMetrics.DefaultJumpDistance);

        public bool HasArmor => _hasArmor;

        public bool CanIgnoreHazards => _canIgnoreHazards;

        /// <inheritdoc />
        /// <remarks>
        /// Clamped on read, not just in the Inspector. <c>[Range]</c> is a drawer: it never runs on
        /// deserialization, so an asset authored before this field existed loads as 0 and would otherwise
        /// convert nothing, and it never runs through <see cref="SetAuthoredData" /> either. The upper bound
        /// matters just as much — a radius past <see cref="BoardMetrics.MaxConversionRadius" /> outgrows every
        /// buffer sized from <see cref="BoardMetrics.MaxImpactAreaCells" /> on a per-landing path.
        /// </remarks>
        public int ConversionRadius => Mathf.Clamp(_conversionRadius, BoardMetrics.DefaultConversionRadius, BoardMetrics.MaxConversionRadius);

        /// <inheritdoc />
        /// <remarks>
        /// Built once and memoized: the board reads this on every landing, and projecting the authoring array
        /// per access would allocate one array per move. The cache is a derived view of authored data, never
        /// runtime state, and <c>OnValidate</c> drops it so an Inspector edit is picked up immediately.
        /// </remarks>
        public IReadOnlyList<ImpactEffect> LandingEffects => _cachedLandingEffects ??= BuildLandingEffects();

#if UNITY_EDITOR
        protected void OnValidate()
        {
            _cachedLandingEffects = null;
            ValidateAuthoredData();
        }
#endif

        internal void SetAuthoredData(
            string cardId,
            string displayName,
            string description,
            CardType type,
            int energyCost,
            bool canClone,
            bool canJump,
            bool hasArmor,
            bool canIgnoreHazards,
            int conversionRadius,
            ImpactEffectDefinition[] landingEffects,
            int cloneDistance = BoardMetrics.DefaultCloneDistance,
            int jumpDistance = BoardMetrics.DefaultJumpDistance
        )
        {
            _cardId = cardId;
            _displayName = displayName;
            _description = description ?? string.Empty;
            _type = type;
            _energyCost = energyCost;
            _canClone = canClone;
            _canJump = canJump;
            _cloneDistance = cloneDistance;
            _jumpDistance = jumpDistance;
            _hasArmor = hasArmor;
            _canIgnoreHazards = canIgnoreHazards;
            _conversionRadius = conversionRadius;
            _landingEffects = landingEffects;
            _cachedLandingEffects = null;
        }

        /// <remarks>
        /// Runs on every Inspector edit, which is what migrates an asset authored before those fields existed from
        /// its deserialized 0.
        /// <para>
        /// The write-backs are an editor self-heal and nothing else: <see cref="ConversionRadius" />,
        /// <see cref="CloneDistance" /> and <see cref="JumpDistance" /> all resolve on read, so gameplay never
        /// depends on the stored values being in range, and their only caller —
        /// <c>OnValidate</c> — is compiled out of a player build. Writing an authored asset at runtime would
        /// persist in the Editor and silently reset in a build, so it must stay behind that guard.
        /// </para>
        /// </remarks>
        internal void ValidateAuthoredData()
        {
            _conversionRadius = Mathf.Clamp(_conversionRadius, BoardMetrics.DefaultConversionRadius, BoardMetrics.MaxConversionRadius);
            _cloneDistance = ResolveAuthoredDistance(_cloneDistance, BoardMetrics.DefaultCloneDistance);
            _jumpDistance = ResolveAuthoredDistance(_jumpDistance, BoardMetrics.DefaultJumpDistance);

            if (string.IsNullOrWhiteSpace(_cardId))
            {
                Debug.LogWarning(string.Format(CardLogMessages.CardIdEmptyFormat, name), this);
            }

            // A warning rather than a repair, exactly as the empty id above: an unauthored description is a
            // legal state for the type — CardDefinition copies whatever it is given — but never one a shipped
            // card should be in, and the Inspector is the only place a designer sees it in time.
            if (string.IsNullOrWhiteSpace(_description))
            {
                Debug.LogWarning(string.Format(CardLogMessages.DescriptionEmptyFormat, name), this);
            }

            WarnOnUnplayableSpellClusters();
            WarnOnDurationUnitMismatch();
        }

        // A spell's Cluster Size doubles as the number of hexes the player must pick, so zero is not "no
        // ceiling" there — it is a count no selection can ever match, and the deployment fails validation at
        // runtime with nothing in the console. The field's own tooltip cannot say this, because the meaning
        // depends on the card type; catching it in the Inspector is the only place a designer sees it in time.
        private void WarnOnUnplayableSpellClusters()
        {
            if (_type != CardType.Spell || _landingEffects == null)
            {
                return;
            }

            for (int i = 0; i < _landingEffects.Length; i++)
            {
                if (_landingEffects[i].ToImpactEffect().ClusterSize > 0)
                {
                    continue;
                }

                Debug.LogWarning(string.Format(CardLogMessages.SpellClusterSizeMissingFormat, name, i), this);
            }
        }

        // Duration is one number that two incompatible clocks read: action windows only advance when somebody
        // deploys, seconds advance on their own. An impact authored with the wrong unit is skipped outright at
        // runtime rather than reinterpreted, so without this the card simply does nothing and the only clue is a
        // console line during a match. The field's own tooltip cannot say which unit is right, because that
        // depends on the impact type sitting three fields above it.
        private void WarnOnDurationUnitMismatch()
        {
            if (_landingEffects == null)
            {
                return;
            }

            for (int i = 0; i < _landingEffects.Length; i++)
            {
                if (HasConsistentDurationUnit(_landingEffects[i].ToImpactEffect()))
                {
                    continue;
                }

                Debug.LogWarning(string.Format(CardLogMessages.DurationUnitMismatchFormat, name, i), this);
            }
        }

        // None carries no duration at all, and an impact type this build does not know about is already reported
        // as UnknownEffectType by the resolver — neither is worth a second warning here.
        private static bool HasConsistentDurationUnit(in ImpactEffect effect)
        {
            return effect.Type switch
            {
                ImpactEffectType.ArmFuse => effect.DurationUnit == ImpactDurationUnit.Seconds,
                ImpactEffectType.ApplyStatus or ImpactEffectType.SpawnHazard => effect.DurationUnit == ImpactDurationUnit.ActionWindows,
                _ => true,
            };
        }

        private static int ResolveAuthoredDistance(int authored, int fallback)
        {
            return authored <= 0 ? fallback : Mathf.Clamp(authored, BoardMetrics.MinMoveDistance, BoardMetrics.MaxMoveDistance);
        }

        private ImpactEffect[] BuildLandingEffects()
        {
            if (_landingEffects == null || _landingEffects.Length == 0)
            {
                return _noLandingEffects;
            }

            var effects = new ImpactEffect[_landingEffects.Length];

            for (int i = 0; i < _landingEffects.Length; i++)
            {
                effects[i] = _landingEffects[i].ToImpactEffect();
            }

            return effects;
        }
    }
}
