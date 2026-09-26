using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Board;
using System;
using System.Collections;
using UnityEngine;

namespace SpaceXonix.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private BoardManager boardManager;
        [SerializeField, Min(1)] private int startingLives = 3;
        [SerializeField, Min(0f)] private float respawnDelay = 1.25f;
        [SerializeField, Min(0f)] private float postRespawnInvulnerabilityDuration = 2f;
        [SerializeField, Range(1f, 100f)] private float captureTargetPercentage = 75f;

        public static GameManager Instance { get; private set; }
        private LifeStateModel lifeState;
        private Coroutine respawnCoroutine;
        private bool failureInProgress;
        private bool playerDamageable;
        private int failureGateReleaseFrame = -1;
        private int playerLifecycleGeneration;
        private float invulnerabilityRemaining;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PlayerLifecycleDiagnostics playerDiagnostics;
#endif

        public GameplayState CurrentState => lifeState != null ? lifeState.State : GameplayState.Playing;
        public int Lives => lifeState != null ? lifeState.Lives : startingLives;
        public PlayerController PlayerController => playerController;
        public bool IsFailureInProgress => failureInProgress;
        public bool IsPlayerDamageable => playerDamageable;
        public bool IsInvulnerable => CurrentState == GameplayState.Playing && invulnerabilityRemaining > 0f;
        public int PlayerLifecycleGeneration => playerLifecycleGeneration;
        public float CaptureTargetPercentage => captureTargetPercentage;
        /// <summary>Configured lives for a fresh run, before any difficulty bonus.</summary>
        public int StartingLives => startingLives;
        public bool IsPaused { get; private set; }
        public bool IsShieldActive { get; private set; }
        /// <summary>How far round the ship the shield bubble reaches over the trail, in world units.</summary>
        public float ShieldTrailRadius { get; private set; }
        internal float InvulnerabilityRemaining => invulnerabilityRemaining;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool HasActiveRespawnOperation => respawnCoroutine != null;
#endif
        public event Action<int> LivesChanged;
        public event Action<PlayerFailureReason> PlayerFailed;
        public event Action RespawnStarted;
        public event Action PlayerRespawned;
        public event Action GameOver;
        public event Action StageCompleted;
        public event Action<bool> PausedChanged;
        public event Action StageBriefingStarted;
        public event Action StagePlayStarted;
        /// <summary>Raised when the shield absorbs something that would have cost a life.</summary>
        public event Action<PlayerFailureReason> FailureShielded;

        private void Awake()
        {
            Application.runInBackground = true;
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one GameManager may exist in a scene.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (inputRouter == null || playerController == null || boardManager == null)
            {
                Debug.LogError("GameManager requires InputRouter, PlayerController, and BoardManager references.", this);
                enabled = false;
                return;
            }

            playerController.ConnectInput(inputRouter);
            playerController.ConnectBoard(boardManager);
            playerController.ConnectLifecycle(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerDiagnostics = new PlayerLifecycleDiagnostics(this, playerController, boardManager, inputRouter);
#endif
            inputRouter.SetGameplayInputEnabled(false);
            playerController.SetGameplayState(GameplayState.Respawning);
            boardManager.TrailFailureRequested += OnTrailFailureRequested;
        }

        private void OnDestroy()
        {
            if (boardManager != null) boardManager.TrailFailureRequested -= OnTrailFailureRequested;
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            if (IsPaused) Time.timeScale = 1f;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            lifeState = new LifeStateModel(startingLives);
            failureInProgress = false;
            playerDamageable = true;
            failureGateReleaseFrame = -1;
            playerLifecycleGeneration = 0;
            invulnerabilityRemaining = 0f;
            inputRouter.ResetDirection(playerController.InitialDirection);
            LivesChanged?.Invoke(Lives);
            ApplyState(GameplayState.Playing);
            TracePlayerLifecycle("GameStarted");
        }

        private void LateUpdate()
        {
            EnsureValidPlayingPlayerState();
            TryCompleteStage();
            if (!failureInProgress || failureGateReleaseFrame < 0 || Time.frameCount <= failureGateReleaseFrame) return;
            failureInProgress = false;
            failureGateReleaseFrame = -1;
            if (CurrentState == GameplayState.Playing && invulnerabilityRemaining <= 0f)
                playerDamageable = true;
            TracePlayerLifecycle("FailureGateReleased");
        }

        private void Update()
        {
            AdvanceLifecycle(Time.deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnityEngine.InputSystem.Keyboard.current?.f8Key.wasPressedThisFrame == true)
                playerDiagnostics?.DumpManual();
#endif
        }

        public void AdvanceLifecycle(float deltaTime)
        {
            if (invulnerabilityRemaining <= 0f) return;
            invulnerabilityRemaining = Mathf.Max(0f, invulnerabilityRemaining - Mathf.Max(0f, deltaTime));
            if (invulnerabilityRemaining <= 0f && !failureInProgress && CurrentState == GameplayState.Playing)
                playerDamageable = true;
        }

        public void SetState(GameplayState state)
        {
            invulnerabilityRemaining = 0f;
            playerDamageable = state == GameplayState.Playing;
            lifeState?.SetState(state);
            ApplyState(state);
        }

        /// <summary>Runs after the frame's movement so a completing capture has fully settled the player on safe terrain.</summary>
        public bool TryCompleteStage()
        {
            if (lifeState == null || lifeState.State != GameplayState.Playing || failureInProgress ||
                boardManager.IsPlayerExposed || boardManager.CapturedPercentage < captureTargetPercentage) return false;
            if (!lifeState.TryCompleteStage()) return false;
            invulnerabilityRemaining = 0f;
            playerDamageable = false;
            playerLifecycleGeneration++;
            ApplyState(GameplayState.StageComplete);
            TracePlayerLifecycle("StageCompleted", force: true);
            StageCompleted?.Invoke();
            return true;
        }

        /// <summary>
        /// Resets lifecycle, board, and player for a new stage and holds gameplay in Briefing.
        /// Lives carry across stages: pass the run's current lives, or a value below 1 to start from the configured total.
        /// </summary>
        public void BeginStage(int lives = 0)
        {
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }
            SetPaused(false);
            SetShieldActive(false);
            lifeState = new LifeStateModel(lives > 0 ? lives : startingLives);
            lifeState.SetState(GameplayState.Briefing);
            failureInProgress = false;
            playerDamageable = false;
            failureGateReleaseFrame = -1;
            playerLifecycleGeneration++;
            invulnerabilityRemaining = 0f;
            boardManager.ResetBoard();
            playerController.ConnectBoard(boardManager);
            var spawnCell = boardManager.WorldToGrid(boardManager.GetDefaultSpawnPosition());
            playerController.RestoreSafeManualState(spawnCell, ResolveRespawnDirection(spawnCell, playerController.InitialDirection));
            ApplyState(GameplayState.Briefing);
            LivesChanged?.Invoke(Lives);
            TracePlayerLifecycle("StageBriefingStarted", force: true);
            StageBriefingStarted?.Invoke();
        }

        /// <summary>Adds lives to the run immediately (Reinforced Hull).</summary>
        public bool AddLives(int amount)
        {
            if (lifeState == null || !lifeState.AddLives(amount)) return false;
            LivesChanged?.Invoke(Lives);
            TracePlayerLifecycle($"LivesGranted:{amount}", force: true);
            return true;
        }

        public bool StartStagePlay()
        {
            if (lifeState == null || lifeState.State != GameplayState.Briefing) return false;
            lifeState.SetState(GameplayState.Playing);
            playerDamageable = true;
            ApplyState(GameplayState.Playing);
            TracePlayerLifecycle("StagePlayStarted", force: true);
            StagePlayStarted?.Invoke();
            return true;
        }

        /// <summary>Freezes simulation time without leaving the current life state, so exposed movement resumes intact.</summary>
        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            inputRouter.SetGameplayInputEnabled(!paused && CurrentState == GameplayState.Playing);
            TracePlayerLifecycle(paused ? "GamePaused" : "GameResumed", force: true);
            PausedChanged?.Invoke(paused);
        }

        /// <param name="trailRadius">How far round the ship the bubble also covers the trail. Below the ship's own radius it only covers the ship.</param>
        public void SetShieldActive(bool active, float trailRadius = 0f)
        {
            ShieldTrailRadius = active ? Mathf.Max(0f, trailRadius) : 0f;
            if (IsShieldActive == active) return;
            IsShieldActive = active;
            TracePlayerLifecycle(active ? "ShieldActivated" : "ShieldExpired", force: true);
        }

        /// <summary>
        /// Trail under the shield bubble is covered by it: the cell the ship is on and every cell within
        /// the bubble's reach. The rest of the trail stays vulnerable while shielded.
        /// </summary>
        public bool IsTrailCellShielded(GridCoordinate cell)
        {
            var player = PlayerController;
            if (!IsShieldActive || boardManager == null || player == null) return false;
            if (cell == boardManager.PlayerCell) return true;
            var radius = Mathf.Max(player.CollisionRadius, ShieldTrailRadius);
            return boardManager.CellOverlapsCircle(cell, player.transform.position, radius);
        }

        public static bool IsShieldableFailure(PlayerFailureReason reason) =>
            reason == PlayerFailureReason.EnemyContact || reason == PlayerFailureReason.Laser ||
            reason == PlayerFailureReason.BossProjectile;

        public bool CanProcessPlayerContact(int lifecycleGeneration)
        {
            return !IsPaused && lifecycleGeneration == playerLifecycleGeneration && playerDamageable && !failureInProgress && !IsInvulnerable &&
                lifeState != null && lifeState.State == GameplayState.Playing;
        }

        public bool ReportPlayerFailure(PlayerFailureReason reason)
        {
            if (IsShieldActive && IsShieldableFailure(reason))
            {
                TracePlayerLifecycle("FailureShielded", reason);
                FailureShielded?.Invoke(reason);
                return false;
            }
            if (IsPaused || !playerDamageable || failureInProgress || IsInvulnerable || lifeState == null ||
                lifeState.State != GameplayState.Playing)
            {
                TracePlayerLifecycle("FailureRejected", reason);
                return false;
            }
            playerDamageable = false;
            failureInProgress = true;
            playerLifecycleGeneration++;
            if (!lifeState.TryFail())
            {
                playerLifecycleGeneration--;
                return false;
            }

            invulnerabilityRemaining = 0f;
            ApplyState(CurrentState);
            boardManager.CancelActiveTrail();
            playerController.PrepareForRespawn();
            TracePlayerLifecycle("FailureAccepted", reason);
            LivesChanged?.Invoke(Lives);
            PlayerFailed?.Invoke(reason);
            if (CurrentState == GameplayState.GameOver)
            {
                invulnerabilityRemaining = 0f;
                GameOver?.Invoke();
                return true;
            }

            if (respawnCoroutine == null) respawnCoroutine = StartCoroutine(RespawnAfterDelay());
            RespawnStarted?.Invoke();
            TracePlayerLifecycle("RespawnStarted", reason);
            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            TracePlayerLifecycle("RespawnDelayElapsed");
            respawnCoroutine = null;
            CompleteRespawn();
        }

        private bool CompleteRespawn()
        {
            if (!failureInProgress || lifeState == null || lifeState.State != GameplayState.Respawning) return false;
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }
            boardManager.CancelActiveTrail();
            TracePlayerLifecycle("RespawnCompletionStarted");
            if (!RestorePlayerToCurrentSafeState()) return false;
            if (!lifeState.CompleteRespawn())
            {
                ApplyState(GameplayState.Respawning);
                return false;
            }
            invulnerabilityRemaining = postRespawnInvulnerabilityDuration;
            playerDamageable = false;
            ApplyState(GameplayState.Playing);
            failureGateReleaseFrame = Time.frameCount;
            PlayerRespawned?.Invoke();
            TracePlayerLifecycle("RespawnCompleted");
            return true;
        }

        private bool RestorePlayerToCurrentSafeState()
        {
            boardManager.CancelActiveTrail();
            var respawnCell = boardManager.GetSafeRespawnCell();
            TracePlayerLifecycle($"RespawnCellSelected:{respawnCell}", force: true);
            if (!boardManager.IsValidRespawnCell(respawnCell)) return false;
            var respawnDirection = ResolveRespawnDirection(respawnCell, playerController.InitialDirection);
            if (!playerController.RestoreSafeManualState(respawnCell, respawnDirection)) return false;
            return boardManager.Model.ActiveTrail.Count == 0 && !boardManager.IsPlayerExposed &&
                boardManager.Model.GetCell(boardManager.PlayerCell) == BoardCellState.Captured &&
                playerController.ControlState == PlayerControlState.SafeIdle && playerController.HasValidPlayingState();
        }

        private void EnsureValidPlayingPlayerState()
        {
            if (lifeState == null || lifeState.State != GameplayState.Playing || playerController.HasValidPlayingState()) return;

            TracePlayerLifecycle("RuntimeInvariantRepairStarted");

            failureInProgress = true;
            playerDamageable = false;
            playerLifecycleGeneration++;
            invulnerabilityRemaining = 0f;
            lifeState.SetState(GameplayState.Respawning);
            ApplyState(GameplayState.Respawning);
            boardManager.CancelActiveTrail();
            playerController.PrepareForRespawn();
            if (!RestorePlayerToCurrentSafeState() || !lifeState.CompleteRespawn()) return;

            invulnerabilityRemaining = postRespawnInvulnerabilityDuration;
            playerDamageable = false;
            ApplyState(GameplayState.Playing);
            failureGateReleaseFrame = Time.frameCount;
            PlayerRespawned?.Invoke();
            TracePlayerLifecycle("RuntimeInvariantRepairCompleted");
        }

        private CardinalDirection ResolveRespawnDirection(GridCoordinate cell, CardinalDirection preferred)
        {
            if (CanMoveFrom(cell, preferred)) return preferred;

            var directions = new[]
            {
                CardinalDirection.Up,
                CardinalDirection.Down,
                CardinalDirection.Left,
                CardinalDirection.Right
            };
            foreach (var direction in directions)
            {
                if (CanMoveFrom(cell, direction)) return direction;
            }

            return preferred;
        }

        private bool CanMoveFrom(GridCoordinate cell, CardinalDirection direction)
        {
            var offset = direction.ToVector2();
            return boardManager.IsLegalPlayerStep(new GridCoordinate(cell.X + (int)offset.x, cell.Y + (int)offset.y));
        }

        private bool OnTrailFailureRequested() => ReportPlayerFailure(PlayerFailureReason.TrailSelfIntersection);

        private void ApplyState(GameplayState state)
        {
            var isPlaying = state == GameplayState.Playing;
            inputRouter.SetGameplayInputEnabled(isPlaying);
            playerController.SetGameplayState(state);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public void TracePlayerLifecycle(string reason, PlayerFailureReason? failureReason = null,
            int operationGeneration = -1, bool force = false)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerDiagnostics?.Record(reason, failureReason, operationGeneration, force);
#endif
        }
    }
}
