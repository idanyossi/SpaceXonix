using SpaceXonix.Core;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Makes the respawn pause read as a death instead of a freeze: the ship blows up in a pixel-art
    /// fireball at its hover height and returns when the respawn completes. Presentation only.
    /// The fireball replaced a ring laid flat on the board, which read as the ship being squashed.
    /// </summary>
    public sealed class PlayerDeathPresenter : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private ActorVisual playerVisual;
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Width of the fireball in world units; the ship is roughly a third of this.")]
        [SerializeField, Min(.01f)] private float explosionSize = 1.1f;

        private SpriteBurst activeExplosion;

        public bool ShipHidden { get; private set; }
        public SpriteBurst ActiveExplosion => activeExplosion;

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
            ReleaseExplosion();
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Advances the fireball. Public so tests can drive it.</summary>
        public void Tick(float deltaTime)
        {
            if (activeExplosion != null && !activeExplosion.Tick(deltaTime)) ReleaseExplosion();
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
            // From the ship's visual, which hovers above the board, so the fireball is where the ship was seen.
            var origin = playerVisual != null && playerVisual.Visual != null ? playerVisual.Visual.position : transform.position;
            PlayExplosion(origin);
            HideShip();
        }

        private void PlayExplosion(Vector3 position)
        {
            ReleaseExplosion();
            if (explosionPrefab == null || poolService == null) return;
            var instance = poolService.Acquire(explosionPrefab, transform);
            activeExplosion = instance.GetComponent<SpriteBurst>();
            if (activeExplosion == null)
            {
                poolService.Release(explosionPrefab, instance);
                return;
            }
            activeExplosion.Play(position, explosionSize);
        }

        private void ReleaseExplosion()
        {
            if (activeExplosion == null) return;
            if (poolService != null && explosionPrefab != null) poolService.Release(explosionPrefab, activeExplosion.gameObject);
            activeExplosion = null;
        }

        private void SetShipVisible(bool visible)
        {
            if (playerVisual == null) return;
            if (playerVisual.Visual != null) playerVisual.Visual.gameObject.SetActive(visible);
            if (playerVisual.Shadow != null) playerVisual.Shadow.gameObject.SetActive(visible);
        }
    }
}
