using SpaceXonix.Core;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Makes the respawn pause read as a death instead of a freeze: the ship bursts and disappears
    /// on the hit and returns when the respawn completes. Presentation only.
    /// </summary>
    public sealed class PlayerDeathPresenter : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ActorVisual playerVisual;
        [SerializeField] private ExplosionRingPresenter ringPresenter;
        [SerializeField, Min(.01f)] private float burstRadius = .55f;

        public bool ShipHidden { get; private set; }

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.PlayerFailed += OnPlayerFailed;
            gameManager.PlayerRespawned += ShowShip;
            gameManager.StageBriefingStarted += ShowShip;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.PlayerFailed -= OnPlayerFailed;
            gameManager.PlayerRespawned -= ShowShip;
            gameManager.StageBriefingStarted -= ShowShip;
            ShowShip();
        }

        public void HideShip()
        {
            ShipHidden = true;
            SetShipVisible(false);
        }

        public void ShowShip()
        {
            ShipHidden = false;
            SetShipVisible(true);
        }

        private void OnPlayerFailed(PlayerFailureReason reason)
        {
            if (playerVisual != null && ringPresenter != null) ringPresenter.Spawn(playerVisual.transform.position, burstRadius);
            HideShip();
        }

        private void SetShipVisible(bool visible)
        {
            if (playerVisual == null) return;
            if (playerVisual.Visual != null) playerVisual.Visual.gameObject.SetActive(visible);
            if (playerVisual.Shadow != null) playerVisual.Shadow.gameObject.SetActive(visible);
        }
    }
}
