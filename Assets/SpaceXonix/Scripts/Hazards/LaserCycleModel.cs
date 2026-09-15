using System;

namespace SpaceXonix.Hazards
{
    public sealed class LaserCycleModel
    {
        private readonly float warningDuration;
        private readonly float firingDuration;
        private readonly float cooldownDuration;

        public LaserCycleModel(float warningDuration, float firingDuration, float cooldownDuration)
        {
            if (warningDuration <= 0f || firingDuration <= 0f || cooldownDuration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(warningDuration), "Laser phase durations must be positive.");
            this.warningDuration = warningDuration;
            this.firingDuration = firingDuration;
            this.cooldownDuration = cooldownDuration;
            Reset();
        }

        public LaserState State { get; private set; }
        public float TimeRemaining { get; private set; }
        public event Action<LaserState> StateChanged;

        public void Reset()
        {
            State = LaserState.Cooldown;
            TimeRemaining = cooldownDuration;
        }

        public void Advance(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            TimeRemaining -= deltaTime;
            while (TimeRemaining <= 0f)
            {
                var overflow = -TimeRemaining;
                State = State == LaserState.Cooldown ? LaserState.Warning : State == LaserState.Warning ? LaserState.Firing : LaserState.Cooldown;
                TimeRemaining = DurationFor(State) - overflow;
                StateChanged?.Invoke(State);
            }
        }

        private float DurationFor(LaserState state) => state == LaserState.Warning ? warningDuration : state == LaserState.Firing ? firingDuration : cooldownDuration;
    }
}
