using SpaceXonix.Input;
using SpaceXonix.Player;
using SpaceXonix.Board;
using UnityEngine;

namespace SpaceXonix.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private BoardManager boardManager;

        public static GameManager Instance { get; private set; }
        public GameplayState CurrentState { get; private set; } = GameplayState.Playing;

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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            SetState(GameplayState.Playing);
        }

        public void SetState(GameplayState state)
        {
            CurrentState = state;
            var isPlaying = state == GameplayState.Playing;
            inputRouter.SetGameplayInputEnabled(isPlaying);
            playerController.SetMovementEnabled(isPlaying);
        }
    }
}
