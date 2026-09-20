using SpaceXonix.Board;
using SpaceXonix.Campaign;
using SpaceXonix.Core;
using SpaceXonix.Enemies;
using SpaceXonix.Hazards;
using SpaceXonix.Input;
using SpaceXonix.Power;
using SpaceXonix.PowerUps;
using UnityEngine;

namespace SpaceXonix.Audio
{
    /// <summary>
    /// Turns gameplay events into sounds. All the wiring lives here so no gameplay system needs to
    /// know that audio exists, which also means a missing AudioManager costs nothing but silence.
    /// </summary>
    public sealed class GameplayAudioBinder : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private InputRouter inputRouter;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private PowerUpManager powerUpManager;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private LaserManager laserManager;
        [SerializeField] private CampaignManager campaignManager;
        [SerializeField] private SpaceXonix.Boss.BossController bossController;
        [Tooltip("Capture percentage at or above which the bigger capture sound is used instead.")]
        [SerializeField, Min(0f)] private float largeCapturePercentage = 15f;

        private void OnEnable()
        {
            if (boardManager != null) boardManager.CaptureCompleted += OnCaptureCompleted;
            if (inputRouter != null) inputRouter.DirectionChanged += OnDirectionChanged;
            if (powerMeter != null)
            {
                powerMeter.PowerFull += OnPowerFull;
                powerMeter.ShotFired += OnShotFired;
                powerMeter.EnemyDestroyedByShot += OnEnemyDestroyed;
            }
            if (powerUpManager != null)
            {
                powerUpManager.PickupSpawned += OnPickupSpawned;
                powerUpManager.StoredChanged += OnStoredChanged;
                powerUpManager.EffectStarted += OnEffectStarted;
                powerUpManager.EffectEnded += OnEffectEnded;
            }
            if (enemyManager != null) enemyManager.ExplosionOccurred += OnExplosion;
            if (gameManager != null) gameManager.PlayerFailed += OnPlayerFailed;
            if (bossController != null) bossController.Defeated += OnBossDefeated;
            if (campaignManager != null) campaignManager.StageLoaded += OnStageLoaded;
            SubscribeLasers(true);
        }

        private void OnDisable()
        {
            if (boardManager != null) boardManager.CaptureCompleted -= OnCaptureCompleted;
            if (inputRouter != null) inputRouter.DirectionChanged -= OnDirectionChanged;
            if (powerMeter != null)
            {
                powerMeter.PowerFull -= OnPowerFull;
                powerMeter.ShotFired -= OnShotFired;
                powerMeter.EnemyDestroyedByShot -= OnEnemyDestroyed;
            }
            if (powerUpManager != null)
            {
                powerUpManager.PickupSpawned -= OnPickupSpawned;
                powerUpManager.StoredChanged -= OnStoredChanged;
                powerUpManager.EffectStarted -= OnEffectStarted;
                powerUpManager.EffectEnded -= OnEffectEnded;
            }
            if (enemyManager != null) enemyManager.ExplosionOccurred -= OnExplosion;
            if (gameManager != null) gameManager.PlayerFailed -= OnPlayerFailed;
            if (bossController != null) bossController.Defeated -= OnBossDefeated;
            if (campaignManager != null) campaignManager.StageLoaded -= OnStageLoaded;
            SubscribeLasers(false);
        }

        /// <summary>Plays a sound through whichever manager is available, silently if none is.</summary>
        private void Play(GameSfx sfx)
        {
            var manager = audioManager != null ? audioManager : AudioManager.Instance;
            if (manager != null) manager.Play(sfx);
        }

        private void OnCaptureCompleted(BoardCaptureResult result) =>
            Play(result.PercentageGained >= largeCapturePercentage ? GameSfx.LargeCapture : GameSfx.CaptureCompleted);

        // Steering out of safe territory is what starts a trail, which is the moment worth hearing.
        private void OnDirectionChanged(SpaceXonix.Player.CardinalDirection direction) =>
            Play(boardManager != null && boardManager.IsPlayerExposed ? GameSfx.DirectionChanged : GameSfx.TrailStarted);

        private void OnPowerFull() => Play(GameSfx.PowerMeterFull);
        private void OnShotFired(PowerShotProjectile shot) => Play(GameSfx.PowerShot);
        private void OnEnemyDestroyed(EnemyController enemy) => Play(GameSfx.EnemyDestroyed);
        private void OnPickupSpawned(PowerUpPickup pickup) => Play(GameSfx.PickupSpawned);

        private void OnStoredChanged(PowerUpType? stored)
        {
            // Only a pickup being taken is worth a sound; clearing the slot is not.
            if (stored.HasValue) Play(GameSfx.PickupCollected);
        }

        private void OnEffectStarted(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield: Play(GameSfx.ShieldActivated); break;
                case PowerUpType.Freeze: Play(GameSfx.FreezeActivated); break;
                case PowerUpType.ArenaTilt: Play(GameSfx.ArenaTilt); break;
            }
        }

        private void OnEffectEnded(PowerUpType type)
        {
            if (type == PowerUpType.Freeze) Play(GameSfx.FreezeEnded);
        }

        private void OnExplosion(Vector3 position, int enemies, int territory) => Play(GameSfx.VolatileExplosion);
        private void OnPlayerFailed(PlayerFailureReason reason) => Play(GameSfx.PlayerHit);
        private void OnBossDefeated() => Play(GameSfx.BossDestroyed);

        /// <summary>The boss stage gets its own track, as the GDD asks.</summary>
        private void OnStageLoaded(StageDefinition stage)
        {
            var manager = audioManager != null ? audioManager : AudioManager.Instance;
            if (manager == null || stage == null) return;
            manager.PlayMusic(stage.IsBossStage ? MusicTrack.Boss : MusicTrack.Gameplay);
        }

        private void SubscribeLasers(bool subscribe)
        {
            if (laserManager == null || laserManager.Emitters == null) return;
            foreach (var emitter in laserManager.Emitters)
            {
                if (emitter == null) continue;
                if (subscribe)
                {
                    emitter.WarningStarted += OnLaserWarning;
                    emitter.FiringStarted += OnLaserFiring;
                }
                else
                {
                    emitter.WarningStarted -= OnLaserWarning;
                    emitter.FiringStarted -= OnLaserFiring;
                }
            }
        }

        private void OnLaserWarning() => Play(GameSfx.LaserWarning);
        private void OnLaserFiring() => Play(GameSfx.LaserFiring);
    }
}
