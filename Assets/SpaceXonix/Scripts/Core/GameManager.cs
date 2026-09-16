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

        public static GameManager Instance { get; private set; }
        private LifeStateModel lifeState;
        private Coroutine respawnCoroutine;
        private bool failureInProgress;
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
        public bool IsInvulnerable => CurrentState == GameplayState.Playing && invulnerabilityRemaining > 0f;
        public int PlayerLifecycleGeneration => playerLifecycleGeneration;
        internal float InvulnerabilityRemaining => invulnerabilityRemaining;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool HasActiveRespawnOperation => respawnCoroutine != null;
#endif
        public event Action<int> LivesChanged;
        public event Action<PlayerFailureReason> PlayerFailed;
        public event Action RespawnStarted;
        public event Action PlayerRespawned;
        public event Action GameOver;

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
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            lifeState = new LifeStateModel(startingLives);
            failureInProgress = false;
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
            if (!failureInProgress || failureGateReleaseFrame < 0 || Time.frameCount <= failureGateReleaseFrame) return;
            failureInProgress = false;
            failureGateReleaseFrame = -1;
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
        }

        public void SetState(GameplayState state)
        {
            invulnerabilityRemaining = 0f;
            lifeState?.SetState(state);
            ApplyState(state);
        }

        public bool CanProcessPlayerContact(int lifecycleGeneration)
        {
            return lifecycleGeneration == playerLifecycleGeneration && !failureInProgress && !IsInvulnerable &&
                lifeState != null && lifeState.State == GameplayState.Playing;
        }

        public bool ReportPlayerFailure(PlayerFailureReason reason)
        {
            if (failureInProgress || IsInvulnerable || lifeState == null || lifeState.State != GameplayState.Playing)
            {
                TracePlayerLifecycle("FailureRejected", reason);
                return false;
            }
            failureInProgress = true;
            playerLifecycleGeneration++;
            if (!lifeState.TryFail())
            {
                playerLifecycleGeneration--;
                failureInProgress = false;
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
            playerLifecycleGeneration++;
            invulnerabilityRemaining = 0f;
            lifeState.SetState(GameplayState.Respawning);
            ApplyState(GameplayState.Respawning);
            boardManager.CancelActiveTrail();
            playerController.PrepareForRespawn();
            if (!RestorePlayerToCurrentSafeState() || !lifeState.CompleteRespawn()) return;

            invulnerabilityRemaining = postRespawnInvulnerabilityDuration;
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
        public void TracePlayerLifecycle(string reason, PlayerFailureReason? failureReason = null, int operationGeneration = -1)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            playerDiagnostics?.Record(reason, failureReason, operationGeneration);
#endif
        }
    }
}
