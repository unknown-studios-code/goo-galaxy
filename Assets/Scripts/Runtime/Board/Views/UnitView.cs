using System;
using System.Collections.Generic;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Board.Presenters;
using GooGalaxy.Runtime.Board.Utils;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;

namespace GooGalaxy.Runtime.Board.Views
{
    /// <summary>
    /// Renders the units standing on the board and the feedback a landing produces: it places and tints a
    /// pooled visual per live unit, and plays a pooled deploy, conversion, or armor-break effect on the hexes
    /// involved — one effect per outcome, because a flip and an absorbed hit must not read alike.
    /// Decides nothing — ownership, position, and conversion outcomes are read from the models that own them.
    /// </summary>
    /// <remarks>
    /// Visuals are keyed by unit id rather than by coordinate, so a Jump repositions the existing instance
    /// instead of releasing and re-acquiring one. Nothing here mutates a unit; a converted unit's new owner is
    /// read back from <c>GridUnit.PlayerId</c>, which the conversion attempt already set.
    /// </remarks>
    [DisallowMultipleComponent]
    public class UnitView : MonoBehaviour
    {
        private const int PlayerOneId = 1;
        private const int PlayerTwoId = 2;

        // The ceiling, not a typical count: a Clone permanently adds a unit, so anything smaller only defers the
        // Instantiate to the frame a move commits.
        private const int UnitPoolDefaultCapacity = BoardMetrics.DefaultBoardCellCount;
        private const int UnitPoolMaxSize = BoardMetrics.DefaultBoardCellCount;

        // One landing converts at most a full ring, and two landings routinely overlap inside one effect lifetime.
        private const int EffectPoolDefaultCapacity = BoardMetrics.NeighborsPerCell * 2;
        private const int EffectPoolMaxSize = 32;

        private static readonly ProfilerMarker _conversionFeedbackMarker = new("UnitView.ApplyConversionFeedback");

        [Header("Prefabs")]
        [SerializeField]
        private GameObject _unitPrefab;

        [SerializeField]
        private GameObject _deployEffectPrefab;

        [SerializeField]
        private GameObject _conversionEffectPrefab;

        [Tooltip(
            "Played when a landing breaks an armored unit's shell without flipping it. Per GDD 06, armor is a white shell, so this reads as the shell shattering — never as an ownership flip."
        )]
        [SerializeField]
        private GameObject _armorBreakEffectPrefab;

        [Header("Layout")]
        [Tooltip("Distance from a hex center to its corner vertex, in world units. Must match GridView's cell visual size or units drift off their cells.")]
        [SerializeField]
        private float _cellVisualSize = 1.0f;

        [Tooltip("Sorting order for unit sprites. Must be above the cell sprites or units render behind the board.")]
        [SerializeField]
        private int _unitSortingOrder = 10;

        [Tooltip("Sorting order for effect sprites. Must be above the units so feedback is never hidden by a specimen.")]
        [SerializeField]
        private int _effectSortingOrder = 20;

        [Header("Faction Colors")]
        [Tooltip("Player 1 specimen tint. Electric Cyan (#00F5FF) per the GDD art direction.")]
        [SerializeField]
        private Color _playerOneColor = new(0f, 0.961f, 1f, 1f);

        [Tooltip("Player 2 specimen tint. Hot Magenta (#FF2DAA) per the GDD art direction.")]
        [SerializeField]
        private Color _playerTwoColor = new(1f, 0.176f, 0.667f, 1f);

        [Tooltip("Fallback tint for a unit whose owner is neither player. Visible only when player ids are wired wrong.")]
        [SerializeField]
        private Color _unclaimedColor = new(0.6f, 0.6f, 0.6f, 1f);

        [Header("Card Colors")]
        [Tooltip(
            "Body tint per card id, so a troop's type is readable at a glance. Placeholder for the per-card silhouettes GDD 06 calls for; a card with no entry keeps the owner's colour."
        )]
        [SerializeField]
        private CardBodyColor[] _cardBodyColors = Array.Empty<CardBodyColor>();

        [Header("Effects")]
        [Tooltip(
            "Seconds an effect instance stays active before returning to its pool. Must outlast the particle system's own duration or the effect is cut short."
        )]
        [Min(0f)]
        [SerializeField]
        private float _effectLifetimeInSeconds = 1f;

        [Header("References")]
        [SerializeField]
        private GridPresenter _gridPresenter;

        [SerializeField]
        private UnitPresenter _unitPresenter;

        private readonly Dictionary<int, GameObject> _unitVisuals = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, SpriteRenderer> _unitRenderers = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, SpriteRenderer> _unitBodyRenderers = new(BoardMetrics.DefaultBoardCellCount);
        private readonly List<int> _staleUnitIds = new(BoardMetrics.DefaultBoardCellCount);
        private readonly List<SpriteRenderer> _rendererBuffer = new(2);

        private ObjectPool<GameObject> _unitPool;
        private ObjectPool<GameObject> _deployEffectPool;
        private ObjectPool<GameObject> _conversionEffectPool;
        private ObjectPool<GameObject> _armorBreakEffectPool;

        /// <summary>The number of units currently rendered. Pooled-but-idle instances are not counted.</summary>
        internal int RenderedUnitCount => _unitVisuals.Count;

        private void Awake()
        {
            if (_gridPresenter == null)
            {
                TryGetComponent(out _gridPresenter);
            }

            if (_unitPresenter == null)
            {
                TryGetComponent(out _unitPresenter);
            }

            Debug.Assert(_unitPrefab != null, BoardLogMessages.UnitViewPrefabNotAssigned, this);

            if (_unitPrefab != null)
            {
                _unitPool = CreatePool(_unitPrefab, UnitPoolDefaultCapacity, UnitPoolMaxSize);
            }

            if (_deployEffectPrefab != null)
            {
                _deployEffectPool = CreatePool(_deployEffectPrefab, EffectPoolDefaultCapacity, EffectPoolMaxSize);
            }

            if (_conversionEffectPrefab != null)
            {
                _conversionEffectPool = CreatePool(_conversionEffectPrefab, EffectPoolDefaultCapacity, EffectPoolMaxSize);
            }

            if (_armorBreakEffectPrefab != null)
            {
                _armorBreakEffectPool = CreatePool(_armorBreakEffectPrefab, EffectPoolDefaultCapacity, EffectPoolMaxSize);
            }

            PrewarmPool(_unitPool, UnitPoolDefaultCapacity);
            PrewarmPool(_deployEffectPool, EffectPoolDefaultCapacity);
            PrewarmPool(_conversionEffectPool, EffectPoolDefaultCapacity);
            PrewarmPool(_armorBreakEffectPool, EffectPoolDefaultCapacity);
        }

        private void OnEnable()
        {
            MatchEvents.MoveExecuted += HandleMoveExecuted;
            MatchEvents.ConversionResolved += HandleConversionResolved;
        }

        private void OnDisable()
        {
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
            MatchEvents.ConversionResolved -= HandleConversionResolved;
        }

        private void OnDestroy()
        {
            _unitVisuals.Clear();
            _unitRenderers.Clear();
            _unitBodyRenderers.Clear();
            _staleUnitIds.Clear();

            _unitPool?.Dispose();
            _deployEffectPool?.Dispose();
            _conversionEffectPool?.Dispose();
            _armorBreakEffectPool?.Dispose();

            _unitPool = null;
            _deployEffectPool = null;
            _conversionEffectPool = null;
            _armorBreakEffectPool = null;
        }

        /// <summary>
        /// Rebuilds every unit visual from the registry: creates one for each live unit, refreshes its position
        /// and tint, and releases any visual whose unit is gone. Call it once the starting units are registered,
        /// and again after any bulk change the move and conversion events did not describe.
        /// </summary>
        /// <remarks>
        /// A whole-board pass that walks the registry through its interface, so it boxes one enumerator per
        /// call. Intended for bootstrap and recovery, never for a per-frame or per-move path.
        /// </remarks>
        public void SyncUnitVisuals()
        {
            if (_unitPresenter == null || _unitPool == null)
            {
                Debug.LogError(BoardLogMessages.UnitViewBoardUnavailable, this);
                return;
            }

            IReadOnlyDictionary<int, GridUnit> activeUnits = _unitPresenter.ActiveUnits;

            _staleUnitIds.Clear();

            foreach (KeyValuePair<int, GameObject> visual in _unitVisuals)
            {
                if (!activeUnits.ContainsKey(visual.Key))
                {
                    _staleUnitIds.Add(visual.Key);
                }
            }

            for (int i = 0; i < _staleUnitIds.Count; i++)
            {
                ReleaseUnitVisual(_staleUnitIds[i]);
            }

            foreach (KeyValuePair<int, GridUnit> entry in activeUnits)
            {
                if (entry.Value != null)
                {
                    ShowUnit(entry.Key, entry.Value.Position, entry.Value.PlayerId, entry.Value.CardId);
                }
            }
        }

        /// <summary>
        /// Returns the visual of a unit that has left the board to its pool. Does nothing when the unit has no
        /// visual, so it is safe to call for a unit that was never rendered.
        /// </summary>
        /// <param name="unitId">The identifier of the unit whose visual should be released.</param>
        public void ReleaseUnitVisual(int unitId)
        {
            if (!_unitVisuals.TryGetValue(unitId, out GameObject instance))
            {
                return;
            }

            _unitVisuals.Remove(unitId);
            _unitRenderers.Remove(unitId);
            _unitBodyRenderers.Remove(unitId);

            if (instance != null && _unitPool != null)
            {
                _unitPool.Release(instance);
            }
        }

        /// <summary>Looks up the live visual of a unit, so a test can assert where and how it was rendered.</summary>
        internal bool TryGetUnitVisual(int unitId, out GameObject instance)
        {
            return _unitVisuals.TryGetValue(unitId, out instance) && instance != null;
        }

        /// <summary>Reads back the tint currently applied to a unit's visual.</summary>
        internal bool TryGetUnitColor(int unitId, out Color color)
        {
            color = default;

            if (!_unitRenderers.TryGetValue(unitId, out SpriteRenderer unitRenderer) || unitRenderer == null)
            {
                return false;
            }

            color = unitRenderer.color;

            return true;
        }

        /// <summary>Assigns the pooled prefabs and board metrics before <c>Awake</c> builds the pools from them.</summary>
        internal void SetViewConfiguration(
            GameObject unitPrefab,
            GameObject deployEffectPrefab,
            GameObject conversionEffectPrefab,
            GameObject armorBreakEffectPrefab,
            float cellVisualSize
        )
        {
            _unitPrefab = unitPrefab;
            _deployEffectPrefab = deployEffectPrefab;
            _conversionEffectPrefab = conversionEffectPrefab;
            _armorBreakEffectPrefab = armorBreakEffectPrefab;
            _cellVisualSize = cellVisualSize;
        }

        /// <summary>Assigns the per-player tints, so a test can assert against values it owns.</summary>
        internal void SetFactionColors(Color playerOneColor, Color playerTwoColor)
        {
            _playerOneColor = playerOneColor;
            _playerTwoColor = playerTwoColor;
        }

        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            if (_unitPresenter == null || _unitPool == null || !TryGetHexGrid(out HexGrid grid))
            {
                Debug.LogError(BoardLogMessages.UnitViewBoardUnavailable, this);
                return;
            }

            IReadOnlyDictionary<int, GridUnit> activeUnits = _unitPresenter.ActiveUnits;

            for (int i = 0; i < affectedCoordinates.Count; i++)
            {
                HexCoordinates coordinates = affectedCoordinates[i];

                // A Jump's source is empty by now and needs no release: the unit that left it keeps the same id,
                // so the target below repositions that very instance.
                if (!grid.TryGetCell(coordinates, out HexCell cell) || !cell.IsOccupied)
                {
                    continue;
                }

                if (activeUnits.TryGetValue(cell.OccupantUnitId, out GridUnit unit) && unit != null)
                {
                    ShowUnit(unit.UnitId, coordinates, unit.PlayerId, unit.CardId);
                }
            }

            PlayEffect(_deployEffectPool, command.Target);
        }

        private void HandleConversionResolved(int actingPlayerId, ConversionResult result)
        {
            using (_conversionFeedbackMarker.Auto())
            {
                if (_unitPresenter == null || _unitPool == null)
                {
                    Debug.LogError(BoardLogMessages.UnitViewBoardUnavailable, this);
                    return;
                }

                // Per GDD 06 the two outcomes must never look alike: a flip is the "melt and reform" into the new
                // owner's color, while a broken shell is only the Armored white-shell overlay coming off.
                ApplyConversionFeedback(result.ConvertedUnitIds, _conversionEffectPool, shouldRefreshOwnerTint: true);
                ApplyConversionFeedback(result.ArmorStrippedUnitIds, _armorBreakEffectPool, shouldRefreshOwnerTint: false);
            }
        }

        // Ownership is read back from the unit rather than from the acting player id, so this is correct whether
        // it runs before or after HandleMoveExecuted — MoveExecuted subscriber order is registration order.
        private void ApplyConversionFeedback(IReadOnlyList<int> unitIds, ObjectPool<GameObject> effectPool, bool shouldRefreshOwnerTint)
        {
            if (unitIds == null)
            {
                return;
            }

            IReadOnlyDictionary<int, GridUnit> activeUnits = _unitPresenter.ActiveUnits;

            for (int i = 0; i < unitIds.Count; i++)
            {
                int unitId = unitIds[i];

                if (!activeUnits.TryGetValue(unitId, out GridUnit unit) || unit == null)
                {
                    continue;
                }

                if (shouldRefreshOwnerTint)
                {
                    // ShowUnit rather than TintUnit: a unit converted before anything rendered it has no visual
                    // yet, and TintUnit would silently no-op and leave it invisible for the rest of the match.
                    ShowUnit(unitId, unit.Position, unit.PlayerId, unit.CardId);
                }

                PlayEffect(effectPool, unit.Position);
            }
        }

        // The visual is two sprites: the root ring carries the owner's colour and the child body carries the
        // card's, so a glance answers both "whose is it" and "what is it". GDD 06 gives each card its own
        // silhouette for the second question; until those exist, colour stands in.
        private void ShowUnit(int unitId, HexCoordinates coordinates, int playerId, CardId cardId)
        {
            if (!_unitVisuals.TryGetValue(unitId, out GameObject instance) || instance == null)
            {
                instance = _unitPool.Get();
                _unitVisuals[unitId] = instance;

                SpriteRenderer ringRenderer = instance.TryGetComponent(out SpriteRenderer renderer) ? renderer : null;

                if (ringRenderer != null)
                {
                    ringRenderer.sortingOrder = _unitSortingOrder;
                }

                _unitRenderers[unitId] = ringRenderer;
                _unitBodyRenderers[unitId] = FindBodyRenderer(instance, ringRenderer);
            }

            instance.transform.SetPositionAndRotation(ProjectToBoard(coordinates), Quaternion.identity);
            TintUnit(unitId, playerId, cardId);
        }

        // The body is any sprite below the root. A prefab without one still renders, just without a card tint.
        private SpriteRenderer FindBodyRenderer(GameObject instance, SpriteRenderer ringRenderer)
        {
            // PERF: the List overload fills a reused buffer; the array overload allocates one per pooled acquire.
            instance.GetComponentsInChildren(true, _rendererBuffer);

            for (int i = 0; i < _rendererBuffer.Count; i++)
            {
                if (_rendererBuffer[i] != ringRenderer)
                {
                    _rendererBuffer[i].sortingOrder = _unitSortingOrder + 1;

                    return _rendererBuffer[i];
                }
            }

            return null;
        }

        private void TintUnit(int unitId, int playerId, CardId cardId)
        {
            if (_unitRenderers.TryGetValue(unitId, out SpriteRenderer ringRenderer) && ringRenderer != null)
            {
                ringRenderer.color = GetFactionColor(playerId);
            }

            if (_unitBodyRenderers.TryGetValue(unitId, out SpriteRenderer bodyRenderer) && bodyRenderer != null)
            {
                bodyRenderer.color = GetCardColor(cardId, playerId);
            }
        }

        private Color GetCardColor(CardId cardId, int playerId)
        {
            for (int i = 0; i < _cardBodyColors.Length; i++)
            {
                if (_cardBodyColors[i].CardId == cardId.Value)
                {
                    return _cardBodyColors[i].Color;
                }
            }

            // An unmapped card keeps the owner's colour, so a missing entry reads as "plain unit", not as a bug.
            return GetFactionColor(playerId);
        }

        private void PlayEffect(ObjectPool<GameObject> pool, HexCoordinates coordinates)
        {
            if (pool == null)
            {
                return;
            }

            GameObject instance = pool.Get();
            instance.transform.SetPositionAndRotation(ProjectToBoard(coordinates), Quaternion.identity);

            if (instance.TryGetComponent(out SpriteRenderer effectRenderer))
            {
                effectRenderer.sortingOrder = _effectSortingOrder;
            }

            _ = ReleaseEffectAfterLifetimeAsync(pool, instance);
        }

        private async Awaitable ReleaseEffectAfterLifetimeAsync(ObjectPool<GameObject> pool, GameObject instance)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(_effectLifetimeInSeconds, destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Narrower than the usual isActiveAndEnabled guard on purpose: a view disabled mid-effect must still
            // return the instance, or the pool hands out a fresh one and leaks the active object until teardown.
            if (this == null || instance == null || pool == null)
            {
                return;
            }

            try
            {
                pool.Release(instance);
            }
            catch (InvalidOperationException exception)
            {
                // The pools run with collectionCheck on, so a double release throws. The caller discards this
                // Awaitable, so an unobserved throw here would silently strand the instance outside the pool.
                Debug.LogException(exception, this);
            }
        }

        private Color GetFactionColor(int playerId)
        {
            return playerId switch
            {
                PlayerOneId => _playerOneColor,
                PlayerTwoId => _playerTwoColor,
                _ => _unclaimedColor,
            };
        }

        private Vector3 ProjectToBoard(HexCoordinates coordinates)
        {
            return HexMathUtils.ProjectToWorldSpace(coordinates, _cellVisualSize);
        }

        private ObjectPool<GameObject> CreatePool(GameObject prefab, int defaultCapacity, int maxSize)
        {
            return new ObjectPool<GameObject>(
                () => Instantiate(prefab, transform),
                ActivateInstance,
                DeactivateInstance,
                DestroyInstance,
                collectionCheck: true,
                defaultCapacity,
                maxSize
            );
        }

        // PERF: defaultCapacity only sizes the pool's free list, so without this the first Get of every instance
        // Instantiates on the frame a player commits a move — up to seven at once on the first landing.
        private void PrewarmPool(ObjectPool<GameObject> pool, int count)
        {
            if (pool == null)
            {
                return;
            }

            var warmed = new GameObject[count];

            for (int i = 0; i < count; i++)
            {
                warmed[i] = pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                pool.Release(warmed[i]);
            }
        }

        private void ActivateInstance(GameObject instance)
        {
            instance.SetActive(true);
        }

        private void DeactivateInstance(GameObject instance)
        {
            // A particle effect released while it still has live particles would pop them in at the next hex the
            // instance is used on, one frame after it is re-activated.
            if (instance.TryGetComponent(out ParticleSystem effect))
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            instance.SetActive(false);
        }

        private void DestroyInstance(GameObject instance)
        {
            Destroy(instance);
        }

        private bool TryGetHexGrid(out HexGrid grid)
        {
            grid = _gridPresenter != null ? _gridPresenter.HexGrid : null;

            return grid != null;
        }

        /// <summary>Placeholder body tint for one card, matched by id. Authored in the Inspector.</summary>
        [Serializable]
        private struct CardBodyColor
        {
            public string CardId;

            public Color Color;
        }
    }
}
