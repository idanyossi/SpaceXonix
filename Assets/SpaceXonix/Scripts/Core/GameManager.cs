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

        public static GameManager Instance { get; private set; }
        private LifeStateModel lifeState;
        private Coroutine respawnCoroutine;

        public GameplayState CurrentState => lifeState != null ? lifeState.State : GameplayState.Playing;
        public int Lives => lifeState != null ? lifeState.Lives : startingLives;
        public PlayerController PlayerController => playerController;
        public event Action<int> LivesChanged;
        public event Action<PlayerFailureReason> PlayerFailed;
        public event Action RespawnStarted;
        public event Action PlayerRespawned;
        public event Action GameOver;

        private void Awake()
        {
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
            boardManager.TrailStateChanged += OnTrailStateChanged;
        }

        private void OnDestroy()
        {
            if (boardManager != null) boardManager.TrailStateChanged -= OnTrailStateChanged;
            if (respawnCoroutine != null) StopCoroutine(respawnCoroutine);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            lifeState = new LifeStateModel(startingLives);
            LivesChanged?.Invoke(Lives);
            ApplyState(GameplayState.Playing);
        }

        public void SetState(GameplayState state)
        {
            lifeState?.SetState(state);
            ApplyState(state);
        }

        public bool ReportPlayerFailure(PlayerFailureReason reason)
        {
            if (lifeState == null || !lifeState.TryFail()) return false;

            boardManager.CancelActiveTrail();
            LivesChanged?.Invoke(Lives);
            PlayerFailed?.Invoke(reason);
            if (CurrentState == GameplayState.GameOver)
            {
                ApplyState(GameplayState.GameOver);
                GameOver?.Invoke();
                return true;
            }

            ApplyState(GameplayState.Respawning);
            RespawnStarted?.Invoke();
            respawnCoroutine = StartCoroutine(RespawnAfterDelay());
            return true;
        }

        private IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            CompleteRespawn();
        }

        private bool CompleteRespawn()
        {
            respawnCoroutine = null;
            if (lifeState == null || !lifeState.CompleteRespawn()) return false;
            boardManager.CancelActiveTrail();
            var respawnCell = boardManager.GetSafeRespawnCell();
            var respawnDirection = ResolveRespawnDirection(respawnCell, playerController.InitialDirection);
            playerController.RespawnAt(boardManager.GetWorldPosition(respawnCell), respawnDirection);
            inputRouter.ResetDirection(playerController.CurrentDirection);
            ApplyState(GameplayState.Playing);
            PlayerRespawned?.Invoke();
            return true;
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

        private void OnTrailStateChanged(BoardMoveResult result)
        {
            if (result == BoardMoveResult.TrailFailed) ReportPlayerFailure(PlayerFailureReason.TrailSelfIntersection);
        }

        private void ApplyState(GameplayState state)
        {
            var isPlaying = state == GameplayState.Playing;
            inputRouter.SetGameplayInputEnabled(isPlaying);
            playerController.SetMovementEnabled(isPlaying);
        }
    }
}
