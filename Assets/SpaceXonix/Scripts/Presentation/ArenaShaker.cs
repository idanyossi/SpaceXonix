using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using Unity.Cinemachine;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Turns capture, explosion, and death events into Cinemachine impulses.
    /// Presentation only: it never touches gameplay state and can be switched off by the camera-shake setting.
    /// </summary>
    public sealed class ArenaShaker : MonoBehaviour
    {
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField, Min(0f)] private float captureForce = .12f;
        [SerializeField, Min(0f)] private float largeCaptureForce = .5f;
        [Tooltip("Capture percentage that produces the full large-capture shake.")]
        [SerializeField, Min(1f)] private float largeCapturePercentage = 15f;
        [SerializeField, Min(0f)] private float explosionForce = .6f;
        [SerializeField, Min(.01f)] private float explosionReferenceRadius = .75f;
        [SerializeField, Min(0f)] private float playerDeathForce = .45f;

        public bool ShakeEnabled { get; private set; } = true;
        public float LastForce { get; private set; }

        public void SetShakeEnabled(bool enabled) => ShakeEnabled = enabled;

        private void OnEnable()
        {
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
            if (enemyManager != null) enemyManager.ExplosionOccurred += OnExplosion;
            if (gameManager != null) gameManager.PlayerFailed += OnPlayerFailed;
        }

        private void OnDisable()
        {
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
            if (enemyManager != null) enemyManager.ExplosionOccurred -= OnExplosion;
            if (gameManager != null) gameManager.PlayerFailed -= OnPlayerFailed;
        }

        public void Shake(float force)
        {
            LastForce = 0f;
            if (!ShakeEnabled || force <= 0f) return;
            LastForce = force;
            if (impulseSource != null) impulseSource.GenerateImpulseWithForce(force);
        }

        private void OnCaptureCompleted(BoardCaptureResult result) =>
            Shake(ShakeStrength.ForCapture(result.PercentageGained, captureForce, largeCaptureForce, largeCapturePercentage));

        private void OnExplosion(Vector3 position, int destroyedEnemies, int destroyedTerritory)
        {
            var definition = enemyManager != null ? enemyManager.LastExplosionDefinition : null;
            var radius = definition != null ? definition.volatileBlastRadius : explosionReferenceRadius;
            Shake(ShakeStrength.ForExplosion(radius, explosionReferenceRadius, explosionForce));
        }

        private void OnPlayerFailed(PlayerFailureReason reason) => Shake(playerDeathForce);
    }
}
