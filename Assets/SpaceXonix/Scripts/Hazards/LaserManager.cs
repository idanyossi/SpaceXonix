using System;
using System.Collections.Generic;
using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Hazards
{
    [Serializable]
    public sealed class LaserPlacement
    {
        public LaserDefinition definition;
        [Tooltip("Row for horizontal lasers, column for vertical lasers.")]
        [Min(0)] public int line;
    }

    public sealed class LaserManager : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject warningPrefab;
        [SerializeField] private GameObject beamPrefab;
        [SerializeField] private LaserEmitter[] emitters;
        [Header("Random lines")]
        [Tooltip("Move each laser to a new row or column at every warning, instead of firing along its placed line all stage.")]
        [SerializeField] private bool randomizeLines;
        [Tooltip("Rows or columns kept clear at each edge. The border is always captured, so a beam there threatens nothing.")]
        [SerializeField, Min(0)] private int edgeMargin = 4;
        [Tooltip("Preferred gap, in cells, from other lasers on the same axis.")]
        [SerializeField, Min(0)] private int separation = 8;
        [Tooltip("Lines are picked within this many rows or columns of the ship. 0 lets them fire anywhere.")]
        [SerializeField, Min(0)] private int playerVicinity = 12;
        [SerializeField] private int randomSeed;

        private readonly List<int> occupiedLines = new List<int>();
        private LaserLinePicker linePicker;

        public LaserEmitter[] Emitters => emitters;
        public float CooldownMultiplier { get; private set; } = 1f;

        /// <summary>Stage modifier (Laser Storm): scales every emitter's cooldown.</summary>
        public void SetCooldownMultiplier(float multiplier)
        {
            CooldownMultiplier = Mathf.Max(.05f, multiplier);
            if (emitters == null) return;
            for (var i = 0; i < emitters.Length; i++)
                if (emitters[i] != null) emitters[i].SetCooldownMultiplier(CooldownMultiplier);
        }
        public int ActiveEmitterCount
        {
            get
            {
                var count = 0;
                if (emitters != null) foreach (var emitter in emitters) if (emitter != null && emitter.enabled && emitter.gameObject.activeInHierarchy) count++;
                return count;
            }
        }

        /// <summary>Reuses or creates emitters for a stage's laser layout; unused emitters are shut down and hidden.</summary>
        public void ConfigureStage(IReadOnlyList<LaserPlacement> placements)
        {
            ShutdownEmitters();
            var required = placements?.Count ?? 0;
            var existing = emitters?.Length ?? 0;
            if (emitters == null || existing < required)
            {
                var grown = new LaserEmitter[required];
                for (var i = 0; i < existing; i++) grown[i] = emitters[i];
                emitters = grown;
            }
            for (var i = 0; i < emitters.Length; i++)
            {
                if (i >= required)
                {
                    if (emitters[i] != null) emitters[i].gameObject.SetActive(false);
                    continue;
                }
                var placement = placements[i];
                if (placement?.definition == null)
                {
                    if (emitters[i] != null) emitters[i].gameObject.SetActive(false);
                    continue;
                }
                if (emitters[i] == null)
                {
                    var host = new GameObject("LaserEmitter");
                    host.transform.SetParent(transform, false);
                    emitters[i] = host.AddComponent<LaserEmitter>();
                }
                var emitter = emitters[i];
                emitter.gameObject.SetActive(true);
                emitter.SetCooldownMultiplier(CooldownMultiplier);
                emitter.SetDefinition(placement.definition);
                var cell = placement.definition.axis == LaserAxis.Horizontal
                    ? new GridCoordinate(0, placement.line)
                    : new GridCoordinate(placement.line, 0);
                var position = boardManager.GetWorldPosition(cell);
                position.z = emitter.transform.position.z;
                emitter.transform.position = position;
                emitter.Initialize(boardManager, gameManager, poolService, warningPrefab, beamPrefab);
                emitter.SetLinePicker(randomizeLines ? PickLine : (Func<LaserEmitter, int>)null);
            }
        }

        /// <summary>Turns random lines on or off. Public for tests and tuning.</summary>
        public void SetRandomizeLines(bool value, int seed = 0)
        {
            randomizeLines = value;
            randomSeed = seed;
            linePicker = null;
            if (emitters == null) return;
            foreach (var emitter in emitters)
                if (emitter != null) emitter.SetLinePicker(value ? PickLine : (Func<LaserEmitter, int>)null);
        }

        private int PickLine(LaserEmitter emitter)
        {
            linePicker ??= new LaserLinePicker(randomSeed);
            occupiedLines.Clear();
            // The laser's own line counts too, so consecutive shots never repeat the same lane.
            foreach (var other in emitters)
                if (other != null && other.gameObject.activeInHierarchy && other.Definition != null &&
                    other.Definition.axis == emitter.Definition.axis)
                    occupiedLines.Add(other.Line);
            var count = emitter.Definition.axis == LaserAxis.Horizontal ? boardManager.Rows : boardManager.Columns;
            var focus = -1;
            var player = gameManager != null ? gameManager.PlayerController : null;
            if (player != null)
            {
                var cell = boardManager.WorldToGrid(player.transform.position);
                focus = emitter.Definition.axis == LaserAxis.Horizontal ? cell.Y : cell.X;
            }
            return linePicker.Pick(count, edgeMargin, separation, occupiedLines, focus, playerVicinity);
        }

        private void Start()
        {
            if (gameManager != null) gameManager.StageCompleted += ShutdownEmitters;
            if (emitters == null) return;
            for (var i = 0; i < emitters.Length; i++)
            {
                if (emitters[i] == null) continue;
                emitters[i].Initialize(boardManager, gameManager, poolService, warningPrefab, beamPrefab);
                emitters[i].SetLinePicker(randomizeLines ? PickLine : (Func<LaserEmitter, int>)null);
            }
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.StageCompleted -= ShutdownEmitters;
        }

        private void Update() => Tick(Time.deltaTime);

        public void ShutdownEmitters()
        {
            if (emitters == null) return;
            for (var i = 0; i < emitters.Length; i++) if (emitters[i] != null) emitters[i].Shutdown();
        }

        public void Tick(float deltaTime)
        {
            if (emitters == null) return;
            if (gameManager != null && gameManager.CurrentState == GameplayState.Briefing) return;
            for (var i = 0; i < emitters.Length; i++)
                if (emitters[i] != null && emitters[i].gameObject.activeInHierarchy) emitters[i].Tick(deltaTime);
        }
    }
}
