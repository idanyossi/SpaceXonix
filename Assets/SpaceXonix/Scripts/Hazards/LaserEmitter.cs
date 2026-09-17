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
        private BoardManager board;
        private GameManager game;
        private PoolService pool;
        private GameObject warningPrefab;
        private GameObject beamPrefab;
        private GameObject activeWarning;
        private GameObject activeBeam;
        private LaserCycleModel cycle;
        private bool playerHitThisFiringPhase;

        public LaserState State => cycle != null ? cycle.State : LaserState.Cooldown;
        public LaserAxis Axis => definition.axis;
        public float TimeRemaining => cycle != null ? cycle.TimeRemaining : 0f;
        public event Action WarningStarted;
        public event Action FiringStarted;
        public event Action CooldownStarted;

        public LaserDefinition Definition => definition;

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
            cycle = new LaserCycleModel(definition.warningDuration, definition.firingDuration, definition.cooldownDuration);
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
                ReleaseBeam(); activeWarning = Acquire(warningPrefab); Configure(activeWarning); WarningStarted?.Invoke();
            }
            else if (state == LaserState.Firing)
            {
                playerHitThisFiringPhase = false;
                ReleaseWarning(); activeBeam = Acquire(beamPrefab); Configure(activeBeam); CheckPlayerHit(); FiringStarted?.Invoke();
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

        private void Configure(GameObject instance)
        {
            if (instance == null) return;
            var first = board.GetWorldPosition(new GridCoordinate(0, 0));
            var last = board.GetWorldPosition(new GridCoordinate(board.Columns - 1, board.Rows - 1));
            var emitterCell = board.WorldToGrid(transform.position);
            var center = board.GetWorldPosition(emitterCell);
            var length = definition.axis == LaserAxis.Horizontal ? Mathf.Abs(last.x - first.x) + board.CellWorldSize : Mathf.Abs(last.y - first.y) + board.CellWorldSize;
            if (definition.axis == LaserAxis.Horizontal) center.x = (first.x + last.x) * .5f; else center.y = (first.y + last.y) * .5f;
            instance.GetComponent<LaserPresentation>().Configure(center, definition.axis, length, definition.beamWidth);
        }

        private GameObject Acquire(GameObject prefab) => prefab != null && pool != null ? pool.Acquire(prefab, transform) : null;
        private void ReleaseWarning() { if (activeWarning == null) return; pool.Release(warningPrefab, activeWarning); activeWarning = null; }
        private void ReleaseBeam() { if (activeBeam == null) return; pool.Release(beamPrefab, activeBeam); activeBeam = null; }
        private void ReleasePresentations() { ReleaseWarning(); ReleaseBeam(); }
    }
}
