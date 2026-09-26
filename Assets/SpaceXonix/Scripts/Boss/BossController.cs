using System;
using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Boss
{
    /// <summary>
    /// The stationary Alien Core. It cannot be shot down: it fires pooled projectiles on a cycle,
    /// takes visual damage as the arena is captured, and dies when the stage's capture target is met.
    /// A Power Shot only interrupts the cycle.
    /// </summary>
    public sealed class BossController : MonoBehaviour
    {
        [SerializeField] private BossDefinition definition;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Optional visual root scaled down as the core takes capture damage.")]
        [SerializeField] private Transform bodyVisual;

        private readonly List<BossProjectile> activeProjectiles = new List<BossProjectile>();
        private readonly List<GridCoordinate> traversedCells = new List<GridCoordinate>();
        [Tooltip("Seeds which shot of each volley is the charged one; 0 picks a new seed each run.")]
        [SerializeField] private int randomSeed;
        private System.Random random;
        private BossAttackModel attack;
        private float fireIntervalMultiplier = 1f;
        private float projectileSpeedMultiplier = 1f;
        private Vector3 bodyBaseScale = Vector3.one;
        private bool bodyBaseScaleCaptured;

        public BossDefinition Definition => definition;
        public bool IsActive { get; private set; }
        public int DamageStage { get; private set; }
        public int MaxDamageStages => definition != null ? definition.damageStages : 1;
        public bool IsInterrupted => attack != null && attack.IsInterrupted;
        public float TimeUntilNextVolley => attack != null ? attack.TimeUntilNextVolley : 0f;
        public IReadOnlyList<BossProjectile> ActiveProjectiles => activeProjectiles;
        public event Action<int> DamageStageChanged;
        public event Action VolleyFired;
        public event Action Interrupted;
        public event Action Defeated;
        /// <summary>A territory-breaking shot hit the player's territory: where, and how many cells it broke.</summary>
        public event Action<Vector3, int> TerritoryBroken;

        // The core lives in the scene for every stage, so it must start hidden and only show on a boss stage.
        private void Awake() => SetVisible(false);

        private void OnEnable()
        {
            if (boardManager != null) boardManager.CapturedPercentageChanged += OnCapturedPercentageChanged;
            if (gameManager != null) gameManager.StageCompleted += OnStageCompleted;
        }

        private void OnDisable()
        {
            if (boardManager != null) boardManager.CapturedPercentageChanged -= OnCapturedPercentageChanged;
            if (gameManager != null) gameManager.StageCompleted -= OnStageCompleted;
            Deactivate();
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Wakes the core for a boss stage. Passing null leaves it dormant for every normal stage.</summary>
        public void Activate(BossDefinition bossDefinition)
        {
            Deactivate();
            definition = bossDefinition;
            if (definition == null) return;
            attack = new BossAttackModel(definition.fireInterval * fireIntervalMultiplier);
            DamageStage = 0;
            IsActive = true;
            MoveToBoardAnchor();
            ApplyDamageVisual();
            SetVisible(true);
        }

        public void Deactivate()
        {
            IsActive = false;
            attack = null;
            DamageStage = 0;
            ReleaseAllProjectiles();
            SetVisible(false);
        }

        /// <summary>
        /// Shows or hides the core's body. Only the visual is toggled, never this GameObject, so the
        /// controller keeps running and can be woken again on the boss stage.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (bodyVisual != null) bodyVisual.gameObject.SetActive(visible);
        }

        /// <summary>Stage modifier hook: scales how often the core fires.</summary>
        public void SetFireIntervalMultiplier(float multiplier)
        {
            fireIntervalMultiplier = Mathf.Max(.05f, multiplier);
            if (IsActive && definition != null) attack = new BossAttackModel(definition.fireInterval * fireIntervalMultiplier);
        }

        /// <summary>Stage modifier hook: scales how fast fired projectiles travel.</summary>
        public void SetProjectileSpeedMultiplier(float multiplier) => projectileSpeedMultiplier = Mathf.Max(.05f, multiplier);

        public void Tick(float deltaTime)
        {
            if (!IsActive || definition == null || deltaTime <= 0f) return;
            if (gameManager != null && (gameManager.IsPaused || gameManager.CurrentState == GameplayState.Briefing)) return;
            // Projectiles already in flight keep travelling while the player respawns, but the cycle only runs in play.
            if (gameManager == null || gameManager.CurrentState == GameplayState.Playing)
            {
                if (attack.Tick(deltaTime)) FireVolley();
            }
            AdvanceProjectiles(deltaTime);
        }

        /// <summary>
        /// A Power Shot that reaches the core interrupts the attack cycle without reducing its health.
        /// Returns true when the shot was absorbed and should be released.
        /// </summary>
        public bool TryInterceptShot(Vector2 from, Vector2 to, float shotRadius)
        {
            if (!IsActive || definition == null) return false;
            if (BossProjectile.DistanceToSegment(transform.position, from, to) > definition.bodyRadius + Mathf.Max(0f, shotRadius)) return false;
            attack.Interrupt(definition.interruptDuration);
            Interrupted?.Invoke();
            return true;
        }

        private void FireVolley()
        {
            if (projectilePrefab == null || poolService == null) return;
            var player = gameManager != null ? gameManager.PlayerController : null;
            var aim = player != null
                ? ((Vector2)(player.transform.position - transform.position)).normalized
                : Vector2.down;
            if (aim.sqrMagnitude < .0001f) aim = Vector2.down;

            var count = Mathf.Max(1, definition.projectilesPerVolley);
            var step = count > 1 ? definition.volleySpreadDegrees / (count - 1) : 0f;
            var start = count > 1 ? -definition.volleySpreadDegrees * .5f : 0f;
            var speed = definition.projectileSpeed * projectileSpeedMultiplier;
            // One shot of every volley, chosen at random, is charged to break territory, so the player
            // cannot just step out of the middle lane.
            random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            var charged = random.Next(count);
            for (var i = 0; i < count; i++)
            {
                var direction = (Vector2)(Quaternion.Euler(0f, 0f, start + step * i) * aim);
                var instance = poolService.Acquire(projectilePrefab, transform);
                var projectile = instance.GetComponent<BossProjectile>();
                if (projectile == null)
                {
                    poolService.Release(projectilePrefab, instance);
                    Debug.LogError("Boss projectile prefab requires a BossProjectile.", this);
                    return;
                }
                var origin = transform.position + (Vector3)(direction * definition.bodyRadius);
                origin.z = transform.position.z - .01f;
                projectile.Launch(origin, direction * speed, definition.projectileRadius,
                    i == charged ? definition.middleShotTerritoryRadiusCells : 0f);
                activeProjectiles.Add(projectile);
            }
            VolleyFired?.Invoke();
        }

        private void AdvanceProjectiles(float deltaTime)
        {
            if (activeProjectiles.Count == 0) return;
            var bounds = GetBoardBounds();
            for (var i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                var projectile = activeProjectiles[i];
                if (projectile == null) { activeProjectiles.RemoveAt(i); continue; }
                projectile.Advance(deltaTime, out var from, out var to);
                if (ResolveContact(projectile, from, to) || !bounds.Contains(to)) ReleaseProjectileAt(i);
            }
        }

        /// <summary>
        /// Resolves one projectile's swept segment. A hit on the ship is a boss-projectile failure, which the
        /// Shield blocks; a hit on the unfinished trail is a trail failure, which it does not, matching every
        /// other hazard in the game.
        /// </summary>
        private bool ResolveContact(BossProjectile projectile, Vector2 from, Vector2 to)
        {
            if (gameManager == null || boardManager == null) return false;
            var lifecycleGeneration = gameManager.PlayerLifecycleGeneration;
            if (!gameManager.CanProcessPlayerContact(lifecycleGeneration)) return false;
            var player = gameManager.PlayerController;
            if (player != null &&
                BossProjectile.DistanceToSegment(player.transform.position, from, to) <= projectile.Radius + player.CollisionRadius)
            {
                // The projectile is spent either way: a shielded hit absorbs it instead of killing the player.
                gameManager.ReportPlayerFailure(PlayerFailureReason.BossProjectile);
                return true;
            }

            boardManager.GetTraversedCells(from, to, traversedCells);
            for (var i = 0; i < traversedCells.Count; i++)
            {
                var cell = traversedCells[i];
                var state = boardManager.Model.GetCell(cell);
                if (state == BoardCellState.Trail)
                {
                    gameManager.ReportPlayerFailure(PlayerFailureReason.TrailHit);
                    return true;
                }
                // The middle shot breaks the first territory the player built that it reaches. The
                // permanent border is not the player's, so it flies on past that and off the board.
                if (!projectile.BreaksTerritory || state != BoardCellState.Captured || boardManager.Model.IsStructural(cell)) continue;
                var broken = boardManager.RemoveCapturedWithinRadius(cell, projectile.TerritoryRadiusCells);
                var at = boardManager.GetWorldPosition(cell);
                at.z = projectile.transform.position.z;
                TerritoryBroken?.Invoke(at, broken);
                return true;
            }
            return false;
        }

        private void OnCapturedPercentageChanged(float percentage)
        {
            if (!IsActive || definition == null || gameManager == null) return;
            var target = Mathf.Max(.01f, gameManager.CaptureTargetPercentage);
            var stage = Mathf.Clamp(Mathf.FloorToInt(percentage / target * definition.damageStages), 0, definition.damageStages);
            if (stage == DamageStage) return;
            DamageStage = stage;
            ApplyDamageVisual();
            DamageStageChanged?.Invoke(DamageStage);
        }

        private void OnStageCompleted()
        {
            if (!IsActive) return;
            // The core stops fighting at once, but its body stays up: the destruction sequence plays
            // over it and hides it at the end with HideBody.
            IsActive = false;
            attack = null;
            ReleaseAllProjectiles();
            Defeated?.Invoke();
        }

        /// <summary>The core's body, for presentation such as the destruction sequence.</summary>
        public Transform Body => bodyVisual;

        /// <summary>Hides the defeated core once its destruction sequence has finished.</summary>
        public void HideBody() => SetVisible(false);

        /// <summary>The core shrinks as the arena is taken, so its health reads without a bar.</summary>
        private void ApplyDamageVisual()
        {
            if (bodyVisual == null || definition == null) return;
            // Scale relative to whatever size the body was authored at, never to an absolute value.
            if (!bodyBaseScaleCaptured)
            {
                bodyBaseScale = bodyVisual.localScale;
                bodyBaseScaleCaptured = true;
            }
            var remaining = 1f - (float)DamageStage / Mathf.Max(1, definition.damageStages);
            bodyVisual.localScale = bodyBaseScale * Mathf.Lerp(.35f, 1f, remaining);
        }

        private void MoveToBoardAnchor()
        {
            if (boardManager == null || definition == null) return;
            var column = Mathf.Clamp(Mathf.RoundToInt(definition.columnFraction * (boardManager.Columns - 1)), 0, boardManager.Columns - 1);
            var row = Mathf.Clamp(Mathf.RoundToInt(definition.rowFraction * (boardManager.Rows - 1)), 0, boardManager.Rows - 1);
            transform.position = boardManager.GetWorldPosition(new GridCoordinate(column, row));
        }

        private Rect GetBoardBounds()
        {
            if (boardManager == null) return new Rect(float.MinValue / 2f, float.MinValue / 2f, float.MaxValue, float.MaxValue);
            var min = boardManager.transform.TransformPoint(Vector3.zero);
            var max = boardManager.transform.TransformPoint(new Vector3(boardManager.Columns * boardManager.CellWorldSize,
                boardManager.Rows * boardManager.CellWorldSize, 0f));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private void ReleaseAllProjectiles()
        {
            for (var i = activeProjectiles.Count - 1; i >= 0; i--) ReleaseProjectileAt(i);
        }

        private void ReleaseProjectileAt(int index)
        {
            var projectile = activeProjectiles[index];
            activeProjectiles.RemoveAt(index);
            if (projectile == null) return;
            projectile.Stop();
            if (poolService != null && projectilePrefab != null) poolService.Release(projectilePrefab, projectile.gameObject);
        }
    }
}
