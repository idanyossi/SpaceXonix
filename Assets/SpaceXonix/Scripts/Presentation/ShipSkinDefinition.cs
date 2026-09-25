using System;
using UnityEngine;

namespace SpaceXonix.Presentation
{
    /// <summary>
    /// How a ship flies. Every ship but the default trades a strength for a weakness. All values
    /// are multipliers on top of the run's upgrades and the stage's modifier; 1 changes nothing.
    /// </summary>
    [Serializable]
    public sealed class ShipStats
    {
        [Tooltip("Shown on the hangar tile, e.g. \"+25% speed off your territory\".")]
        public string perk = "Balanced all-rounder";
        public string drawback = "";
        [Min(.1f)] public float speed = 1f;
        [Tooltip("Extra speed on the player's own territory.")]
        [Min(.1f)] public float safeSpeed = 1f;
        [Tooltip("Extra speed out in the open, drawing a trail.")]
        [Min(.1f)] public float exposedSpeed = 1f;
        [Min(.1f)] public float powerCharge = 1f;
        [Min(.1f)] public float shotSpeed = 1f;
        [Min(0f)] public float pickupChance = 1f;
        [Min(.1f)] public float abilityDuration = 1f;
        [Min(0)] public int extraLives;

        public static readonly ShipStats Neutral = new ShipStats();
    }

    /// <summary>
    /// One ship the player can fly: its animation frames, drawn nose-up, its name and its stats.
    /// The ship's size and hitbox never change between ships; only how it flies does.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceXonix/Ship Skin")]
    public sealed class ShipSkinDefinition : ScriptableObject
    {
        [Tooltip("Saved in the settings, so it must never change once a skin has shipped.")]
        public string id = "skin";
        public string displayName = "Ship";
        [Tooltip("Nose-up frames, looped. The first one doubles as the preview and the life icon.")]
        public Sprite[] frames = new Sprite[0];
        [Min(.1f)] public float framesPerSecond = 12f;
        public ShipStats stats = new ShipStats();

        public Sprite Preview => frames != null && frames.Length > 0 ? frames[0] : null;
    }
}
