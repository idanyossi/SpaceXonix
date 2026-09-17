namespace SpaceXonix.PowerUps
{
    public enum PickupCollectResult
    {
        Stored,
        Replaced
    }

    /// <summary>One stored ability. Collecting a pickup always stores it, replacing any held ability immediately.</summary>
    public sealed class PowerUpSlotModel
    {
        public PowerUpType? Stored { get; private set; }
        public bool HasStored => Stored.HasValue;

        public PickupCollectResult Collect(PowerUpType offered)
        {
            var result = HasStored ? PickupCollectResult.Replaced : PickupCollectResult.Stored;
            Stored = offered;
            return result;
        }

        public bool TryConsume(out PowerUpType type)
        {
            type = default;
            if (!HasStored) return false;
            type = Stored.Value;
            Stored = null;
            return true;
        }

        public void Clear() => Stored = null;
    }
}
