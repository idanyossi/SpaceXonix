using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using Unity.Cinemachine;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Turns big gameplay moments into Cinemachine impulses. Ordinary captures never shake, matching
    /// AirXonix's fixed camera; only large captures, Volatile blasts, and deaths move the view.
    /// Presentation only: it never touches gameplay state and obeys the camera-shake setting.
    /// </summary>
    public sealed class ArenaShaker : MonoBehaviour
    {
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private GameManager gameManager;

        [Header("Large capture")]
        [Tooltip("Captures below this percentage do not shake at all.")]
        [SerializeField, Min(0f)] private float captureMinimumPercentage = 15f;
        [Tooltip("Capture percentage that produces the full large-capture shake.")]
        [SerializeField, Min(0f)] private float captureFullPercentage = 30f;
        [SerializeField, Min(0f)] private float largeCaptureForce = .25f;

        [Header("Explosion")]
        [SerializeField, Min(0f)] private float explosionForce = .3f;
        [SerializeField, Min(.01f)] private float explosionReferenceRadius = .75f;

        [Header("Death")]
        [SerializeField, Min(0f)] private float deathMinForce = .6f;
        [SerializeField, Min(0f)] private float deathMaxForce = 1f;
        [Tooltip("0 uses a time-based seed.")]
        [SerializeField] private int randomSeed;

        private System.Random random;
        private SpaceXonix.Settings.GameSettingsModel boundSettings;

        public bool ShakeEnabled { get; private set; } = true;
        public float LastForce { get; private set; }
        public Vector3 LastVelocity { get; private set; }

        public void SetShakeEnabled(bool enabled) => ShakeEnabled = enabled;

        public void SetRandom(System.Random source) => random = source ?? new System.Random();

        private void Awake() => random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();

        private void OnEnable()
        {
            random ??= randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            // The player's camera-shake preference wins whenever a settings service exists, and
            // keeps winning when they change it mid-run from the pause menu.
            boundSettings = SpaceXonix.Settings.GameSettings.Current;
            if (boundSettings != null)
            {
                ApplyShakeSetting();
                boundSettings.Changed += ApplyShakeSetting;
            }
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
            if (enemyManager != null) enemyManager.ExplosionOccurred += OnExplosion;
            if (gameManager != null) gameManager.PlayerFailed += OnPlayerFailed;
        }

        private void OnDisable()
        {
            if (boundSettings != null) boundSettings.Changed -= ApplyShakeSetting;
            boundSettings = null;
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
            if (enemyManager != null) enemyManager.ExplosionOccurred -= OnExplosion;
            if (gameManager != null) gameManager.PlayerFailed -= OnPlayerFailed;
        }

        private void ApplyShakeSetting()
        {
            if (boundSettings != null) ShakeEnabled = boundSettings.CameraShakeEnabled;
        }

        /// <summary>Uniform shake of the given force along the source's default direction.</summary>
        public void Shake(float force)
        {
            LastForce = 0f;
            LastVelocity = Vector3.zero;
            if (!ShakeEnabled || force <= 0f) return;
            LastForce = force;
            if (impulseSource != null) impulseSource.GenerateImpulseWithForce(force);
        }

        /// <summary>Directional shake, used for the varied death kick.</summary>
        public void ShakeWithVelocity(Vector3 velocity)
        {
            LastForce = 0f;
            LastVelocity = Vector3.zero;
            if (!ShakeEnabled || velocity.sqrMagnitude <= 0f) return;
            LastForce = velocity.magnitude;
            LastVelocity = velocity;
            if (impulseSource != null) impulseSource.GenerateImpulseWithVelocity(velocity);
        }

        private void OnCaptureCompleted(BoardCaptureResult result) =>
            Shake(ShakeStrength.ForCapture(result.PercentageGained, captureMinimumPercentage, captureFullPercentage, largeCaptureForce));

        private void OnExplosion(Vector3 position, int destroyedEnemies, int destroyedTerritory)
        {
            var definition = enemyManager != null ? enemyManager.LastExplosionDefinition : null;
            var radius = definition != null ? definition.volatileBlastRadius * enemyManager.LastExplosionScale : explosionReferenceRadius;
            Shake(ShakeStrength.ForExplosion(radius, explosionReferenceRadius, explosionForce));
        }

        private void OnPlayerFailed(PlayerFailureReason reason)
        {
            random ??= new System.Random();
            ShakeWithVelocity(ShakeStrength.ForDeath(deathMinForce, deathMaxForce, (float)random.NextDouble(), (float)random.NextDouble()));
        }
    }
}
