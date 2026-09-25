using System;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Hazards
{
    public sealed class LaserEmitter : MonoBehaviour
    {
        [SerializeField] private LaserDefinition definition;
        [Tooltip("Height of the firing beam above the board floor. The warning previews the beam, so it uses the same height.")]
        [SerializeField, Min(0f)] private float beamHeight = .55f;
        [SerializeField, Min(0f)] private float warningHeight = .55f;
        private BoardManager board;
        private GameManager game;
        private PoolService pool;
        private GameObject warningPrefab;
        private GameObject beamPrefab;
        private GameObject activeWarning;
        private GameObject activeBeam;
        private LaserCycleModel cycle;
        private bool playerHitThisFiringPhase;
        private Func<LaserEmitter, int> linePicker;

        public LaserState State => cycle != null ? cycle.State : LaserState.Cooldown;
        public LaserAxis Axis => definition.axis;
        public float TimeRemaining => cycle != null ? cycle.TimeRemaining : 0f;
        public event Action WarningStarted;
        public event Action FiringStarted;
        public event Action CooldownStarted;

        public LaserDefinition Definition => definition;
        public float CooldownMultiplier { get; private set; } = 1f;

        /// <summary>Stage modifier (Laser Storm): scales the cooldown of the next built cycle.</summary>
        public void SetCooldownMultiplier(float multiplier) => CooldownMultiplier = Mathf.Max(.05f, multiplier);

        /// <summary>Row for a horizontal laser, column for a vertical one.</summary>
        public int Line
        {
            get
            {
                if (board == null) return 0;
                var cell = board.WorldToGrid(transform.position);
                return definition.axis == LaserAxis.Horizontal ? cell.Y : cell.X;
            }
        }

        /// <summary>
        /// When set, the laser moves to the line this returns at the start of every warning, so each
        /// shot comes from somewhere new. Null keeps it on its placed line.
        /// </summary>
        public void SetLinePicker(Func<LaserEmitter, int> picker) => linePicker = picker;

        /// <summary>Places the laser on a row (horizontal) or column (vertical).</summary>
        public void MoveToLine(int line)
        {
            if (board == null) return;
            var cell = definition.axis == LaserAxis.Horizontal ? new GridCoordinate(0, line) : new GridCoordinate(line, 0);
            var position = board.GetWorldPosition(cell);
            position.z = transform.position.z;
            transform.position = position;
        }

        public void SetDefinition(LaserDefinition laserDefinition)
        {
            Shutdown();
            definition = laserDefinition;
        }

        public void Initialize(BoardManager boardManager, GameManager gameManager, PoolService poolService,
            GameObject warningPresentationPrefab, GameObject beamPresentationPrefab)
        {
            if (cycle != null) cycle.StateChanged -= OnStateChanged;
            ReleasePresentations();
            board = boardManager; game = gameManager; pool = poolService;
            warningPrefab = warningPresentationPrefab; beamPrefab = beamPresentationPrefab;
            cycle = new LaserCycleModel(definition.warningDuration, definition.firingDuration, definition.cooldownDuration * CooldownMultiplier);
            playerHitThisFiringPhase = false;
            cycle.StateChanged += OnStateChanged;
        }

        public void Tick(float deltaTime)
        {
            if (cycle == null) return;
            if (cycle.State == LaserState.Firing) CheckPlayerHit();
            cycle.Advance(deltaTime);
        }

        public bool ContainsPoint(Vector3 worldPoint) => ContainsCircle(worldPoint, 0f);

        public bool ContainsCircle(Vector3 worldPoint, float radius)
        {
            var emitterCell = board.WorldToGrid(transform.position);
            var pointCell = board.WorldToGrid(worldPoint);
            if (!board.Model.IsInBounds(pointCell)) return false;
            var emitterCenter = board.GetWorldPosition(emitterCell);
            var perpendicularDistance = definition.axis == LaserAxis.Horizontal ? Mathf.Abs(worldPoint.y - emitterCenter.y) : Mathf.Abs(worldPoint.x - emitterCenter.x);
            return perpendicularDistance <= definition.beamWidth * .5f + Mathf.Max(0f, radius);
        }

        public void Shutdown()
        {
            if (cycle != null) cycle.StateChanged -= OnStateChanged;
            cycle = null;
            ReleasePresentations();
        }

        private void OnDisable() => Shutdown();

        private void OnStateChanged(LaserState state)
        {
            if (state == LaserState.Warning)
            {
                // Moving only here, before the warning shows, keeps the warning, the beam and the hit
                // test on one line for the whole shot.
                if (linePicker != null) MoveToLine(linePicker(this));
                ReleaseBeam(); activeWarning = Acquire(warningPrefab); Configure(activeWarning, warningHeight); WarningStarted?.Invoke();
            }
            else if (state == LaserState.Firing)
            {
                playerHitThisFiringPhase = false;
                ReleaseWarning(); activeBeam = Acquire(beamPrefab); Configure(activeBeam, beamHeight); CheckPlayerHit(); FiringStarted?.Invoke();
            }
            else
            {
                playerHitThisFiringPhase = false;
                ReleasePresentations(); CooldownStarted?.Invoke();
            }
        }

        private void CheckPlayerHit()
        {
            if (playerHitThisFiringPhase || game == null || game.PlayerController == null) return;
            var lifecycleGeneration = game.PlayerLifecycleGeneration;
            if (!game.CanProcessPlayerContact(lifecycleGeneration)) return;
            var player = game.PlayerController;
            if (ContainsCircle(player.transform.position, player.CollisionRadius) && game.ReportPlayerFailure(PlayerFailureReason.Laser))
                playerHitThisFiringPhase = true;
        }

        private void Configure(GameObject instance, float height)
        {
            if (instance == null) return;
            var first = board.GetWorldPosition(new GridCoordinate(0, 0));
            var last = board.GetWorldPosition(new GridCoordinate(board.Columns - 1, board.Rows - 1));
            var emitterCell = board.WorldToGrid(transform.position);
            var center = board.GetWorldPosition(emitterCell);
            var length = definition.axis == LaserAxis.Horizontal ? Mathf.Abs(last.x - first.x) + board.CellWorldSize : Mathf.Abs(last.y - first.y) + board.CellWorldSize;
            if (definition.axis == LaserAxis.Horizontal) center.x = (first.x + last.x) * .5f; else center.y = (first.y + last.y) * .5f;
            instance.GetComponent<LaserPresentation>().Configure(center, definition.axis, length, definition.beamWidth, height);
        }

        private GameObject Acquire(GameObject prefab) => prefab != null && pool != null ? pool.Acquire(prefab, transform) : null;
        private void ReleaseWarning() { if (activeWarning == null) return; pool.Release(warningPrefab, activeWarning); activeWarning = null; }
        private void ReleaseBeam() { if (activeBeam == null) return; pool.Release(beamPrefab, activeBeam); activeBeam = null; }
        private void ReleasePresentations() { ReleaseWarning(); ReleaseBeam(); }
    }
}
