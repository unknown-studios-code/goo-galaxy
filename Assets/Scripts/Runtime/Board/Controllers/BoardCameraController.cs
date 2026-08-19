using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using UnityEngine;

namespace GooGalaxy.Runtime.Board.Controllers
{
    /// <summary>
    /// Frames the whole board in an orthographic camera, on any screen the game runs on.
    /// Listens for the grid and re-fits whenever the aspect ratio changes, so a rotation or a Game view resize
    /// never leaves part of the board off-screen.
    /// </summary>
    /// <remarks>
    /// The extent is derived from the grid radius rather than measured from renderer bounds: it is exact, it
    /// costs nothing, and it works before a single cell has been drawn. A portrait phone is the binding case —
    /// at a 0.46 aspect the board is far wider relative to the viewport than it is tall, so the horizontal fit
    /// decides the size and the vertical axis ends up with slack.
    /// <para>
    /// <c>GridPresenter</c> builds the grid in <c>Awake</c> and deliberately announces nothing;
    /// <c>MatchInitializer</c> publishes <c>GridInitialized</c> from the <c>Start</c>-time setup sequence
    /// <c>MatchController</c> drives, after every <c>OnEnable</c> in the scene has run. So an ordinary
    /// <c>OnEnable</c> subscription is enough and this type needs no execution order of its own. A board built
    /// later still re-frames through <see cref="FitToBoard(int)" />.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class BoardCameraController : MonoBehaviour
    {
        private static readonly float _sqrt3 = Mathf.Sqrt(3f);

        [Tooltip(
            "Distance from a hex center to its corner vertex, in world units. "
                + "Must match GridView's cell visual size or the framing will not match the board that was drawn."
        )]
        [SerializeField]
        private float _cellVisualSize = 1f;

        [Tooltip("Empty border kept around the board, as a fraction of its half-extent. 0.06 leaves a 6% margin.")]
        [Range(0f, 0.5f)]
        [SerializeField]
        private float _marginFraction = 0.06f;

        private Camera _camera;
        private float _lastAspect;
        private int _gridRadius = -1;
        private bool _hasLoggedProjectionError;

        protected void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        protected void OnEnable()
        {
            MatchEvents.GridInitialized += HandleGridInitialized;
        }

        // Aspect changes on device rotation and on every Game view resize, and neither raises an event.
        protected void LateUpdate()
        {
            if (_gridRadius < 0 || Mathf.Approximately(_camera.aspect, _lastAspect))
            {
                return;
            }

            FitToBoard();
        }

        protected void OnDisable()
        {
            MatchEvents.GridInitialized -= HandleGridInitialized;
        }

        /// <summary>
        /// Re-frames the camera around a board of the given radius. Call it when the board is rebuilt at a size
        /// the <c>GridInitialized</c> event did not describe.
        /// </summary>
        /// <param name="gridRadius">Rings of cells around the center hex. Negative values are ignored.</param>
        public void FitToBoard(int gridRadius)
        {
            if (gridRadius < 0)
            {
                return;
            }

            _gridRadius = gridRadius;
            FitToBoard();
        }

        // Test seam: the framing is a pure function of these two authored values, and a test that cannot set
        // them can only assert against whatever the prefab happens to carry.
        internal void SetFitConfiguration(float cellVisualSize, float marginFraction)
        {
            _cellVisualSize = cellVisualSize;
            _marginFraction = marginFraction;
        }

        private void HandleGridInitialized(IHexGrid grid)
        {
            if (grid == null)
            {
                Debug.LogError(BoardLogMessages.CameraFitGridMissing, this);
                return;
            }

            FitToBoard(grid.GridRadius);
        }

        private void FitToBoard()
        {
            // PERF: written before the projection check, not after the fit. LateUpdate re-enters while this
            // differs from the live aspect, so an early return that skips it logs on every frame forever.
            _lastAspect = _camera.aspect;

            if (!_camera.orthographic)
            {
                if (!_hasLoggedProjectionError)
                {
                    _hasLoggedProjectionError = true;
                    Debug.LogError(BoardLogMessages.CameraFitRequiresOrthographic, this);
                }

                return;
            }

            _hasLoggedProjectionError = false;

            // Flat-top axial layout: columns step 1.5 apart, rows sqrt(3). The trailing term is the half-size of
            // the outermost cell itself, which would otherwise be clipped at the border.
            float halfWidth = ((1.5f * _gridRadius) + 1f) * _cellVisualSize;
            float halfHeight = ((_sqrt3 * _gridRadius) + (_sqrt3 * 0.5f)) * _cellVisualSize;

            float aspect = Mathf.Max(_lastAspect, Mathf.Epsilon);
            float sizeForHeight = halfHeight;
            float sizeForWidth = halfWidth / aspect;

            _camera.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth) * (1f + _marginFraction);
        }
    }
}
