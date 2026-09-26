using System.Collections;
using SpaceXonix.Player;
using SpaceXonix.Power;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// Gives the Power Shot its weight. Firing throws a muzzle flash off the ship's nose and kicks the
    /// camera; a hit adds a shockwave across the floor, a heavier shake and a split-second hit-stop
    /// on top of the alien's own explosion. Presentation only.
    /// </summary>
    public sealed class PowerShotPresenter : MonoBehaviour
    {
        [SerializeField] private PowerMeter powerMeter;
        [Tooltip("Plays the muzzle flash. A SpriteBurstPresenter with no events wired, used only as a pooled player.")]
        [SerializeField] private SpriteBurstPresenter muzzleFlashes;
        [Tooltip("The arena's explosions, for the boss hit (an alien's explosion is already played on its death).")]
        [SerializeField] private SpriteBurstPresenter explosions;
        [Tooltip("Plays the floor shockwave. An ExplosionRingPresenter with no events wired.")]
        [SerializeField] private ExplosionRingPresenter shockwaves;
        [SerializeField] private ArenaShaker shaker;

        [Header("Fire")]
        [SerializeField, Min(.01f)] private float muzzleSize = 1.2f;
        [Tooltip("How far ahead of the ship's centre the flash appears, in world units.")]
        [SerializeField, Min(0f)] private float muzzleOffset = .35f;
        [SerializeField, Min(0f)] private float fireShake = .3f;

        [Header("Impact")]
        [SerializeField, Min(0f)] private float impactShake = .6f;
        [SerializeField, Min(.01f)] private float shockwaveRadius = 1.1f;
        [SerializeField, Min(.01f)] private float bossHitSize = 1.6f;
        [Tooltip("Real seconds the game nearly freezes on a hit. 0 turns the hit-stop off.")]
        [SerializeField, Min(0f)] private float hitStopSeconds = .07f;
        [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = .05f;

        private Coroutine hitStop;
        private bool struckEnemy;

        public int ShotsFired { get; private set; }
        public int Impacts { get; private set; }
        public bool IsHitStopping => hitStop != null;

        private void OnEnable()
        {
            if (powerMeter == null) return;
            powerMeter.ShotFired += OnShotFired;
            powerMeter.ShotImpact += OnShotImpact;
            powerMeter.EnemyDestroyedByShot += OnEnemyDestroyed;
        }

        private void OnDisable()
        {
            if (powerMeter != null)
            {
                powerMeter.ShotFired -= OnShotFired;
                powerMeter.ShotImpact -= OnShotImpact;
                powerMeter.EnemyDestroyedByShot -= OnEnemyDestroyed;
            }
            EndHitStop();
        }

        private void OnShotFired(PowerShotProjectile shot)
        {
            if (shot == null) return;
            ShotsFired++;
            var visual = shot.GetComponent<ActorVisual>();
            var hover = visual != null ? visual.HoverHeight : .55f;
            var ahead = (Vector3)shot.Direction.ToVector2() * muzzleOffset;
            // Nudged toward the camera so the flash draws over the ship.
            if (muzzleFlashes != null) muzzleFlashes.Play(shot.transform.position + ahead + Vector3.back * (hover + .1f), muzzleSize);
            if (shaker != null) shaker.Shake(fireShake);
        }

        private void OnShotImpact(Vector3 position)
        {
            Impacts++;
            // An alien's death is announced just before its impact and already has its explosion;
            // the boss only stuns, so it gets one here.
            if (!struckEnemy && explosions != null) explosions.Play(position + Vector3.back * .6f, bossHitSize);
            struckEnemy = false;
            if (shockwaves != null) shockwaves.Spawn(position, shockwaveRadius);
            if (shaker != null) shaker.Shake(impactShake);
            StartHitStop();
        }

        private void OnEnemyDestroyed(SpaceXonix.Enemies.EnemyController enemy) => struckEnemy = true;

        private void StartHitStop()
        {
            if (hitStopSeconds <= 0f || !isActiveAndEnabled || !Application.isPlaying) return;
            // Never fight a pause: only a game running at normal speed is held.
            if (hitStop == null && !Mathf.Approximately(Time.timeScale, 1f)) return;
            if (hitStop != null) StopCoroutine(hitStop);
            hitStop = StartCoroutine(HoldTime());
        }

        private IEnumerator HoldTime()
        {
            Time.timeScale = hitStopTimeScale;
            yield return new WaitForSecondsRealtime(hitStopSeconds);
            hitStop = null;
            // Something else (the pause menu) may have taken over time meanwhile; leave it alone then.
            if (Mathf.Approximately(Time.timeScale, hitStopTimeScale)) Time.timeScale = 1f;
        }

        private void EndHitStop()
        {
            if (hitStop == null) return;
            StopCoroutine(hitStop);
            hitStop = null;
            if (Mathf.Approximately(Time.timeScale, hitStopTimeScale)) Time.timeScale = 1f;
        }
    }
}
