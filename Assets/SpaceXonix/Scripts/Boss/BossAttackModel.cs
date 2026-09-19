using System;

namespace SpaceXonix.Boss
{
    /// <summary>
    /// Pure attack-cycle timing for the Alien Core: a repeating fire interval that a Power Shot
    /// can interrupt. Holds no Unity state so it can be stepped deterministically in tests.
    /// </summary>
    public sealed class BossAttackModel
    {
        private readonly float fireInterval;

        public BossAttackModel(float fireInterval)
        {
            if (fireInterval <= 0f) throw new ArgumentOutOfRangeException(nameof(fireInterval), "The fire interval must be positive.");
            this.fireInterval = fireInterval;
            Reset();
        }

        public float TimeUntilNextVolley { get; private set; }
        public float InterruptTimeRemaining { get; private set; }
        public bool IsInterrupted => InterruptTimeRemaining > 0f;
        public event Action InterruptStarted;
        public event Action InterruptEnded;

        /// <summary>Advances the cycle. Returns true on each frame a volley should be fired.</summary>
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return false;
            if (InterruptTimeRemaining > 0f)
            {
                InterruptTimeRemaining -= deltaTime;
                if (InterruptTimeRemaining > 0f) return false;
                InterruptTimeRemaining = 0f;
                // The cycle restarts from a full interval so the core never fires the instant it recovers.
                TimeUntilNextVolley = fireInterval;
                InterruptEnded?.Invoke();
                return false;
            }
            TimeUntilNextVolley -= deltaTime;
            if (TimeUntilNextVolley > 0f) return false;
            TimeUntilNextVolley = fireInterval;
            return true;
        }

        /// <summary>A Power Shot hit stops the attack cycle for a while without damaging the core.</summary>
        public void Interrupt(float duration)
        {
            if (duration <= 0f) return;
            var wasInterrupted = IsInterrupted;
            InterruptTimeRemaining = Math.Max(InterruptTimeRemaining, duration);
            if (!wasInterrupted) InterruptStarted?.Invoke();
        }

        public void Reset()
        {
            TimeUntilNextVolley = fireInterval;
            InterruptTimeRemaining = 0f;
        }
    }
}
