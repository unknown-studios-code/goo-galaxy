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
using VContainer;

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

        // Pre-warmed to the widest single landing — a radius-2 conversion reaches 18 units, so a full-ring
        // premise would Instantiate the difference on the frame a Volatile Mass commits. Max size doubles it
        // because two landings routinely overlap inside one effect lifetime, and a release past max size
        // destroys the instance instead of returning it.
        private const int EffectPoolDefaultCapacity = BoardMetrics.MaxConversionTargetsPerLanding;
        private const int EffectPoolMaxSize = BoardMetrics.MaxConversionTargetsPerLanding * 2;

        // A shield lives as long as the armor does, so shields accumulate across a match rather than coming and
        // going: a board of nothing but Bio-Phalanx would show 61 at once. Pre-warming to that would cost more
        // at load than it ever saves, so the warm-up covers the widest single landing — a radius-2 conversion,
        // which is the one event that can reveal or strip a whole ring of them at once.
        private const int ShieldOverlayPoolDefaultCapacity = BoardMetrics.MaxConversionTargetsPerLanding;

        // Frozen is bounded by what one Protocol picks — an authored cluster of at most four — and the marker
        // expires an action window later, so the live count never approaches the conversion ring the shields
        // are sized from. Pre-warming to a full ring would leave most of the instances idle for the match.
        private const int FrozenOverlayPoolDefaultCapacity = BoardMetrics.MaxSpellClusterSize;

        // A fuse only ever burns on a Volatile Mass, and at 4 Energy a player rarely has two ticking at once —
        // two covers one per player. This mirrors the fuse system's own armed-roster capacity; a third
        // simultaneous bomb Instantiates on the frame it arms and then stays pooled for the rest of the match.
        private const int FuseOverlayPoolDefaultCapacity = 2;

        // Both max sizes are the true ceiling — one overlay per unit — so a release always returns the instance
        // instead of destroying it. A pool that destroys on release only shrinks, and the next landing pays an
        // Instantiate for what it just handed back.
        private const int OverlayPoolMaxSize = BoardMetrics.DefaultBoardCellCount;

        // Every overlay renders above the unit's ring and body, and below the transient effects, so a conversion
        // pop still reads on top of a shielded, frozen or fused piece. Frozen sits above the shield: a frozen
        // armored unit is primarily frozen, because that is what stops it being converted at all. The fuse sits
        // above both, because it is the only one of the three the player has seconds rather than windows to
        // answer.
        private const int ShieldOverlaySortingOffset = 2;
        private const int FrozenOverlaySortingOffset = 3;
        private const int FuseOverlaySortingOffset = 4;

        private static readonly ProfilerMarker _conversionFeedbackMarker = new("UnitView.ApplyConversionFeedback");

        // The view's only whole-registry pass, and it runs from LateUpdate behind a boolean gate — without its
        // own marker a non-deep profile cannot tell the pass apart from the frames that only test the gate.
        private static readonly ProfilerMarker _refreshStatusOverlaysMarker = new("UnitView.RefreshStatusOverlays");

        [Header("Prefabs")]
        [SerializeField]
        private GameObject _unitPrefab;

        [SerializeField]
        private GameObject _deployEffectPrefab;

        [SerializeField]
        private GameObject _conversionEffectPrefab;

        [Tooltip(
            "Played when a landing breaks an armored unit's shell without flipping it. "
                + "Per GDD 06, armor is a white shell, so this reads as the shell shattering — never as an ownership flip."
        )]
        [SerializeField]
        private GameObject _armorBreakEffectPrefab;

        [Tooltip(
            "Persistent aura parented to an armored unit while its shell is intact. "
                + "Per GDD 03 this is the Armored Membrane's translucent shield; it is removed the moment the armor is spent, not replayed."
        )]
        [SerializeField]
        private GameObject _shieldOverlayPrefab;

        [Tooltip(
            "Persistent aura parented to a unit while it is under Cryo-Stasis. "
                + "This is the only readout of Frozen — nothing else on screen says a unit cannot move or be converted."
        )]
        [SerializeField]
        private GameObject _frozenOverlayPrefab;

        // TODO (GOOM-26): replace with the countdown VFX GDD 06 specifies; this placeholder only reads as "armed".
        [Tooltip(
            "Persistent aura parented to a unit while its fuse is running. It only has to read as 'about to go "
                + "off' — the player has the card's authored fuse duration to answer it."
        )]
        [SerializeField]
        private GameObject _fuseOverlayPrefab;

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
            "Body tint per card id, so a troop's type is readable at a glance. "
                + "Placeholder for the per-card silhouettes GDD 06 calls for; a card with no entry keeps the owner's colour."
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

        private readonly Dictionary<int, GameObject> _unitVisuals = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, SpriteRenderer> _unitRenderers = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, SpriteRenderer> _unitBodyRenderers = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, GameObject> _shieldOverlays = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, GameObject> _frozenOverlays = new(BoardMetrics.DefaultBoardCellCount);
        private readonly Dictionary<int, GameObject> _fuseOverlays = new(BoardMetrics.DefaultBoardCellCount);
        private readonly List<int> _staleUnitIds = new(BoardMetrics.DefaultBoardCellCount);
        private readonly List<SpriteRenderer> _rendererBuffer = new(2);

        private GridPresenter _gridPresenter;
        private UnitPresenter _unitPresenter;
        private ObjectPool<GameObject> _unitPool;
        private ObjectPool<GameObject> _deployEffectPool;
        private ObjectPool<GameObject> _conversionEffectPool;
        private ObjectPool<GameObject> _armorBreakEffectPool;
        private ObjectPool<GameObject> _shieldOverlayPool;
        private ObjectPool<GameObject> _frozenOverlayPool;
        private ObjectPool<GameObject> _fuseOverlayPool;
        private bool _areStatusOverlaysDirty;

        /// <remarks>Counts only units currently rendered — pooled-but-idle instances are excluded.</remarks>
        internal int RenderedUnitCount => _unitVisuals.Count;

        /// <remarks>
        /// A destroyed-externally overlay is not dropped from tracking until the next call that touches its
        /// unit's overlay state (<see cref="ReleaseUnitVisual"/> or a status refresh), so this can briefly
        /// disagree with a live scene scan.
        /// </remarks>
        internal int TrackedShieldOverlayCount => _shieldOverlays.Count;

        /// <remarks>Same lazy-cleanup caveat as <see cref="TrackedShieldOverlayCount"/>.</remarks>
        internal int TrackedFrozenOverlayCount => _frozenOverlays.Count;

        /// <remarks>
        /// Reads the pool's own inactive count; 0 both when the pool is empty and when no shield overlay prefab
        /// was assigned for <c>Awake</c> to build one from.
        /// </remarks>
        internal int ShieldOverlayPoolInactiveCount => _shieldOverlayPool?.CountInactive ?? 0;

        /// <remarks>
        /// Reads the pool's own inactive count; 0 both when the pool is empty and when no frozen overlay prefab
        /// was assigned for <c>Awake</c> to build one from.
        /// </remarks>
        internal int FrozenOverlayPoolInactiveCount => _frozenOverlayPool?.CountInactive ?? 0;

        /// <remarks>Same lazy-cleanup caveat as <see cref="TrackedShieldOverlayCount"/>.</remarks>
        internal int TrackedFuseOverlayCount => _fuseOverlays.Count;

        /// <remarks>
        /// Reads the pool's own inactive count; 0 both when the pool is empty and when no fuse overlay prefab
        /// was assigned for <c>Awake</c> to build one from.
        /// </remarks>
        internal int FuseOverlayPoolInactiveCount => _fuseOverlayPool?.CountInactive ?? 0;

        /// <summary>Supplies the board this view reads hex positions from, and the registry it mirrors.</summary>
        /// <remarks>
        /// Both are injected rather than wired in the Inspector because this view lives on a child of the board
        /// object, so a sibling lookup finds neither.
        /// </remarks>
        /// <param name="gridPresenter">The board whose cells units are placed on.</param>
        /// <param name="unitPresenter">The registry whose live units this view renders.</param>
        [Inject]
        public void Construct(GridPresenter gridPresenter, UnitPresenter unitPresenter)
        {
            Debug.Assert(gridPresenter != null, BoardLogMessages.GridPresenterMissing, this);
            Debug.Assert(unitPresenter != null, BoardLogMessages.UnitPresenterMissing, this);

            _gridPresenter = gridPresenter;
            _unitPresenter = unitPresenter;
        }

        protected void Awake()
        {
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

            if (_shieldOverlayPrefab != null)
            {
                _shieldOverlayPool = CreatePool(_shieldOverlayPrefab, ShieldOverlayPoolDefaultCapacity, OverlayPoolMaxSize);
            }

            if (_frozenOverlayPrefab != null)
            {
                _frozenOverlayPool = CreatePool(_frozenOverlayPrefab, FrozenOverlayPoolDefaultCapacity, OverlayPoolMaxSize);
            }

            if (_fuseOverlayPrefab != null)
            {
                _fuseOverlayPool = CreatePool(_fuseOverlayPrefab, FuseOverlayPoolDefaultCapacity, OverlayPoolMaxSize);
            }

            PrewarmPool(_unitPool, UnitPoolDefaultCapacity);
            PrewarmPool(_deployEffectPool, EffectPoolDefaultCapacity);
            PrewarmPool(_conversionEffectPool, EffectPoolDefaultCapacity);
            PrewarmPool(_armorBreakEffectPool, EffectPoolDefaultCapacity);
            PrewarmPool(_shieldOverlayPool, ShieldOverlayPoolDefaultCapacity);
            PrewarmPool(_frozenOverlayPool, FrozenOverlayPoolDefaultCapacity);
            PrewarmPool(_fuseOverlayPool, FuseOverlayPoolDefaultCapacity);
        }

        protected void OnEnable()
        {
            MatchEvents.MoveExecuted += HandleMoveExecuted;
            MatchEvents.ConversionResolved += HandleConversionResolved;
            MatchEvents.AbilityResolved += HandleAbilityResolved;

            // The two events that do not ride a deployment. Arming does happen inside one, but expiry is the
            // whole reason these exist: a fuse runs out on the frame clock, with nothing else on the board
            // moving, so no deployment event will ever come along to flag the overlays stale.
            MatchEvents.FuseArmed += HandleFuseArmed;
            MatchEvents.FuseExpired += HandleFuseExpired;

            // While disabled the view is unsubscribed from all three events, so every deployment in that window
            // is invisible to it: statuses are stale, and a unit destroyed meanwhile never reached
            // HandleAbilityResolved, so its pooled visual — and any overlay parented to that visual — stays
            // checked out for the rest of the match, permanently shrinking a pool whose max size is the whole
            // board. A full resync rather than the dirty flag alone, because only the registry pass in
            // SyncUnitVisuals can find a visual whose unit no longer exists.
            if (_unitPresenter == null || _unitPool == null)
            {
                _areStatusOverlaysDirty = true;
                return;
            }

            SyncUnitVisuals();
        }

        // The refresh is deferred to the end of the frame rather than run inside the handler that flagged it,
        // because a status can still change after the last event goes out: step 6 of the GDD chain expires
        // durations *after* AbilityResolved is published, and nothing is raised once it has. Waiting until the
        // whole synchronous deployment chain has unwound is what makes an expiry visible on the deployment that
        // caused it rather than one deployment later. The flag keeps this a per-deployment registry pass, not a
        // per-frame one — an idle frame costs a single boolean test.
        protected void LateUpdate()
        {
            if (!_areStatusOverlaysDirty)
            {
                return;
            }

            _areStatusOverlaysDirty = false;
            RefreshStatusOverlays();
        }

        protected void OnDisable()
        {
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
            MatchEvents.ConversionResolved -= HandleConversionResolved;
            MatchEvents.AbilityResolved -= HandleAbilityResolved;
            MatchEvents.FuseArmed -= HandleFuseArmed;
            MatchEvents.FuseExpired -= HandleFuseExpired;
        }

        protected void OnDestroy()
        {
            _unitVisuals.Clear();
            _unitRenderers.Clear();
            _unitBodyRenderers.Clear();
            _shieldOverlays.Clear();
            _frozenOverlays.Clear();
            _fuseOverlays.Clear();
            _staleUnitIds.Clear();

            _unitPool?.Dispose();
            _deployEffectPool?.Dispose();
            _conversionEffectPool?.Dispose();
            _armorBreakEffectPool?.Dispose();
            _shieldOverlayPool?.Dispose();
            _frozenOverlayPool?.Dispose();
            _fuseOverlayPool?.Dispose();

            _unitPool = null;
            _deployEffectPool = null;
            _conversionEffectPool = null;
            _armorBreakEffectPool = null;
            _shieldOverlayPool = null;
            _frozenOverlayPool = null;
            _fuseOverlayPool = null;
        }

        /// <summary>
        /// Rebuilds every unit visual from the registry: creates one for each live unit, refreshes its position
        /// and tint, and releases any visual whose unit is gone. Call it once the starting units are registered,
        /// and again after any bulk change the move and conversion events did not describe.
        /// </summary>
        /// <remarks>
        /// A whole-board pass. It reads the registry through <c>ActiveUnitValues</c> so the rebuild loop binds the
        /// struct enumerator directly and allocates nothing; the staleness sweep still probes the interface, but
        /// only with <c>ContainsKey</c>, which does not enumerate. Intended for bootstrap and recovery, never for a
        /// per-frame or per-move path.
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

            foreach (GridUnit unit in _unitPresenter.ActiveUnitValues)
            {
                if (unit != null)
                {
                    ShowUnit(unit.UnitId, unit.Position, unit.PlayerId, unit.CardId);
                }
            }

            // Run now rather than flagged: a bulk rebuild is already whole-board work, and a Reset must leave
            // the overlays correct without waiting for the next frame.
            RefreshStatusOverlays();
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

            // Before the unit visual goes back, because the overlays are parented to it — releasing the parent
            // first would send its children into the unit pool and hand them out again with the next unit.
            SetOverlayState(_shieldOverlays, _shieldOverlayPool, unitId, false, ShieldOverlaySortingOffset);
            SetOverlayState(_frozenOverlays, _frozenOverlayPool, unitId, false, FrozenOverlaySortingOffset);
            SetOverlayState(_fuseOverlays, _fuseOverlayPool, unitId, false, FuseOverlaySortingOffset);

            _unitVisuals.Remove(unitId);
            _unitRenderers.Remove(unitId);
            _unitBodyRenderers.Remove(unitId);

            if (instance != null && _unitPool != null)
            {
                _unitPool.Release(instance);
            }
        }

        /// <remarks>Looks up the live visual of a unit, so a test can assert where and how it was rendered.</remarks>
        internal bool TryGetUnitVisual(int unitId, out GameObject instance)
        {
            return _unitVisuals.TryGetValue(unitId, out instance) && instance != null;
        }

        /// <remarks>Reads back the tint currently applied to a unit's visual.</remarks>
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

        /// <remarks>
        /// Returns false both for a unit that never had a shield and for one whose tracked entry was already
        /// dropped after an external destroy — the two are indistinguishable here; use
        /// <see cref="TrackedShieldOverlayCount"/> when the distinction itself is what is under test.
        /// </remarks>
        internal bool TryGetShieldOverlay(int unitId, out GameObject instance)
        {
            return _shieldOverlays.TryGetValue(unitId, out instance) && instance != null;
        }

        /// <remarks>
        /// Same indistinguishable-false caveat as <see cref="TryGetShieldOverlay"/>; use
        /// <see cref="TrackedFrozenOverlayCount"/> for the tracked-entry distinction.
        /// </remarks>
        internal bool TryGetFrozenOverlay(int unitId, out GameObject instance)
        {
            return _frozenOverlays.TryGetValue(unitId, out instance) && instance != null;
        }

        /// <remarks>
        /// Same indistinguishable-false caveat as <see cref="TryGetShieldOverlay"/>; use
        /// <see cref="TrackedFuseOverlayCount"/> for the tracked-entry distinction.
        /// </remarks>
        internal bool TryGetFuseOverlay(int unitId, out GameObject instance)
        {
            return _fuseOverlays.TryGetValue(unitId, out instance) && instance != null;
        }

        /// <remarks>Assigns the pooled prefabs and board metrics, so it must run before <c>Awake</c> builds the pools.</remarks>
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

        /// <remarks>
        /// Same "call before the GameObject is activated" contract as <see cref="SetViewConfiguration"/> —
        /// <c>Awake</c> builds the overlay pools from these fields and never rebuilds them afterward.
        /// </remarks>
        internal void SetOverlayConfiguration(GameObject shieldOverlayPrefab, GameObject frozenOverlayPrefab, GameObject fuseOverlayPrefab = null)
        {
            _shieldOverlayPrefab = shieldOverlayPrefab;
            _frozenOverlayPrefab = frozenOverlayPrefab;
            _fuseOverlayPrefab = fuseOverlayPrefab;
        }

        /// <remarks>Assigns the per-player tints, so a test can assert against values it owns.</remarks>
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

            // Covers the deployment that carries no impact at all: a plain Clone still spawns a unit that may
            // need a shield, and still closes the action window that expires someone else's Frozen.
            _areStatusOverlaysDirty = true;
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

            // Belt and braces, and knowingly so: in production this event is only ever raised from inside a
            // MoveExecuted dispatch that already set the flag, and the LateUpdate deferral makes the order of
            // those two subscribers irrelevant. It stays because ConversionResolved is a public bus event that
            // carries the one fact an overlay depends on — armor spent — and nothing enforces that a future
            // publisher raises it nested inside a move. Setting a boolean twice costs nothing.
            _areStatusOverlaysDirty = true;
        }

        // Released from the id alone, deliberately without a registry lookup: AbilityController publishes this
        // event before its step 6 cleanup runs, so a self-destructed unit is still alive and still registered
        // right now. Gating on the registry would find it and skip nothing, then the cleanup would drop it and
        // strand its pooled visual for the rest of the match — and every stranded instance permanently shrinks
        // a pool whose max size is the whole board.
        private void HandleAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            // Flagged before the early return: an impact that applied a status changed no unit's existence, so
            // the destroyed list is empty and the overlays are the only thing that moved.
            _areStatusOverlaysDirty = true;

            IReadOnlyList<int> destroyedUnitIds = result.DestroyedUnitIds;

            if (destroyedUnitIds == null)
            {
                return;
            }

            // Indexed rather than foreach: the payload is handed over as an interface, which boxes its backing
            // enumerator once per landing per subscriber.
            for (int i = 0; i < destroyedUnitIds.Count; i++)
            {
                ReleaseUnitVisual(destroyedUnitIds[i]);
            }
        }

        // Arming always happens inside a deployment, which has already flagged the overlays — so this is not
        // strictly load-bearing today. It stays because the flag is what makes the fuse readable at all, and
        // nothing enforces that a future publisher arms one from inside a deployment. Setting a boolean twice
        // costs nothing.
        private void HandleFuseArmed(int unitId, int playerId, float remainingSeconds)
        {
            _areStatusOverlaysDirty = true;
        }

        // Released from the id alone and without a registry lookup, for the opposite reason to
        // HandleAbilityResolved: FuseController raises this *after* it has unregistered the unit, so a lookup
        // finds nothing and the registry pass in RefreshStatusOverlays can never reach it either. The id is the
        // only handle left, and a visual not released here stays checked out for the rest of the match —
        // permanently shrinking a pool whose max size is the whole board.
        private void HandleFuseExpired(int unitId, int playerId)
        {
            ReleaseUnitVisual(unitId);
            _areStatusOverlaysDirty = true;
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

        // Shield, Frozen and the fuse are persistent, not transient: they last as long as the state does, so none
        // can ride the fire-and-forget PlayEffect path. There is no expiry event for the first two — the status
        // system drops a marker silently — but they can only change at a deployment boundary, applied in step 4
        // and expired in step 6. Revalidating the whole registry once per deployment is therefore both sufficient
        // and bounded: at most 61 units, and only on a frame where something actually resolved.
        //
        // The fuse is the exception that the dirty flag absorbs rather than breaks. It changes on the frame
        // clock, so no deployment event would ever flag it — which is why FuseArmed and FuseExpired set the flag
        // themselves. The pass stays per-deployment-or-fuse-event, never per frame.
        private void RefreshStatusOverlays()
        {
            if (_unitPresenter == null)
            {
                return;
            }

            using (_refreshStatusOverlaysMarker.Auto())
            {
                // ActiveUnitValues, not ActiveUnits.Values: the interface-typed collection boxes its enumerator.
                foreach (GridUnit unit in _unitPresenter.ActiveUnitValues)
                {
                    if (unit == null)
                    {
                        continue;
                    }

                    bool isAlive = unit.IsAlive;

                    SetOverlayState(_shieldOverlays, _shieldOverlayPool, unit.UnitId, isAlive && unit.HasArmor, ShieldOverlaySortingOffset);
                    SetOverlayState(_frozenOverlays, _frozenOverlayPool, unit.UnitId, isAlive && unit.HasStatus(StatusType.Frozen), FrozenOverlaySortingOffset);
                    SetOverlayState(_fuseOverlays, _fuseOverlayPool, unit.UnitId, isAlive && unit.HasFuse, FuseOverlaySortingOffset);
                }
            }
        }

        private void SetOverlayState(Dictionary<int, GameObject> overlays, ObjectPool<GameObject> pool, int unitId, bool shouldShow, int sortingOffset)
        {
            bool isTracked = overlays.TryGetValue(unitId, out GameObject overlay);
            bool isShown = isTracked && overlay != null;

            // A tracked entry whose instance was destroyed from outside is not shown and never will be, and the
            // equality test below would early-return on it forever. Dropping it here is what keeps the entry
            // from outliving a unit that dies in that state — the next show would have healed it, but a unit
            // that never gets one leaves the key behind for the rest of the match.
            if (isTracked && !isShown)
            {
                overlays.Remove(unitId);
            }

            // Only what changed is touched, so a board sitting on the same statuses costs a dictionary probe
            // per unit and nothing else.
            if (shouldShow == isShown)
            {
                return;
            }

            if (!shouldShow)
            {
                overlays.Remove(unitId);

                if (overlay != null && pool != null)
                {
                    // Re-parented out of the unit visual first, or it would still be a child when that visual
                    // is released and would come back attached to whichever unit borrows it next.
                    overlay.transform.SetParent(transform, false);
                    pool.Release(overlay);
                }

                return;
            }

            if (pool == null || !_unitVisuals.TryGetValue(unitId, out GameObject unitVisual) || unitVisual == null)
            {
                return;
            }

            overlay = pool.Get();
            overlay.transform.SetParent(unitVisual.transform, false);
            overlay.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (overlay.TryGetComponent(out SpriteRenderer overlayRenderer))
            {
                overlayRenderer.sortingOrder = _unitSortingOrder + sortingOffset;
            }

            overlays[unitId] = overlay;
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

        // Placeholder body tint for one card, matched by id. Authored in the Inspector.
        [Serializable]
        private struct CardBodyColor
        {
            [SerializeField]
            private string _cardId;

            [SerializeField]
            private Color _color;

            public readonly string CardId => _cardId;

            public readonly Color Color => _color;
        }
    }
}
