using System;

namespace SpaceXonix.Power
{
    /// <summary>Capture-charged meter: gain = powerPerPercent × captured percent × gain multiplier, capped at max.</summary>
    public sealed class PowerMeterModel
    {
        public PowerMeterModel(float maxPower, float powerPerPercent)
        {
            if (maxPower <= 0f) throw new ArgumentOutOfRangeException(nameof(maxPower));
            if (powerPerPercent < 0f) throw new ArgumentOutOfRangeException(nameof(powerPerPercent));
            MaxPower = maxPower;
            PowerPerPercent = powerPerPercent;
        }

        public float MaxPower { get; }
        public float PowerPerPercent { get; }
        public float GainMultiplier { get; private set; } = 1f;
        public float Power { get; private set; }
        public bool IsFull => Power >= MaxPower;
        public float Normalized => Power / MaxPower;

        public void SetGainMultiplier(float multiplier)
        {
            if (multiplier < 0f) throw new ArgumentOutOfRangeException(nameof(multiplier));
            GainMultiplier = multiplier;
        }

        /// <summary>Returns the power actually added after capping.</summary>
        public float AddCapture(float percentageGained)
        {
            if (percentageGained <= 0f || IsFull) return 0f;
            var before = Power;
            Power = Math.Min(MaxPower, Power + PowerPerPercent * percentageGained * GainMultiplier);
            return Power - before;
        }

        public bool TryConsumeFull()
        {
            if (!IsFull) return false;
            Power = 0f;
            return true;
        }

        public void Reset()
        {
            Power = 0f;
            GainMultiplier = 1f;
        }
    }
}
