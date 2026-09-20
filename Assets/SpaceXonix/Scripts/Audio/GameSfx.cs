namespace SpaceXonix.Audio
{
    /// <summary>
    /// Every sound the game asks for, taken from the GDD's required categories. Gameplay code
    /// requests one of these rather than a clip, so sounds can be swapped or left empty without
    /// touching a single system.
    /// </summary>
    public enum GameSfx
    {
        DirectionChanged,
        TrailStarted,
        CaptureCompleted,
        LargeCapture,
        PowerMeterFull,
        PowerShot,
        PickupSpawned,
        PickupCollected,
        ShieldActivated,
        FreezeActivated,
        FreezeEnded,
        ArenaTilt,
        EnemyDestroyed,
        VolatileExplosion,
        PlayerHit,
        LaserWarning,
        LaserFiring,
        BossProjectile,
        BossDestroyed,
        UiInteraction
    }

    /// <summary>The three music loops the GDD calls for.</summary>
    public enum MusicTrack
    {
        None,
        Menu,
        Gameplay,
        Boss
    }
}
