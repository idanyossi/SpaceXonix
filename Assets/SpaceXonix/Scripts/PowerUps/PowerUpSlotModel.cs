namespace SpaceXonix.PowerUps
{
    public enum PickupCollectResult
    {
        Stored,
        DecisionRequired,
        Rejected
    }

    /// <summary>One stored ability. A pickup touched while occupied waits for an explicit Keep/Replace decision.</summary>
    public sealed class PowerUpSlotModel
    {
        public PowerUpType? Stored { get; private set; }
        public PowerUpType? PendingOffer { get; private set; }
        public bool HasStored => Stored.HasValue;
        public bool IsAwaitingDecision => PendingOffer.HasValue;

        public PickupCollectResult Collect(PowerUpType offered)
        {
            if (IsAwaitingDecision) return PickupCollectResult.Rejected;
            if (!HasStored)
            {
                Stored = offered;
                return PickupCollectResult.Stored;
            }
            PendingOffer = offered;
            return PickupCollectResult.DecisionRequired;
        }

        public bool ResolveDecision(bool replace)
        {
            if (!IsAwaitingDecision) return false;
            if (replace) Stored = PendingOffer;
            PendingOffer = null;
            return true;
        }

        public bool TryConsume(out PowerUpType type)
        {
            type = default;
            if (IsAwaitingDecision || !HasStored) return false;
            type = Stored.Value;
            Stored = null;
            return true;
        }

        public void Clear()
        {
            Stored = null;
            PendingOffer = null;
        }
    }
}
