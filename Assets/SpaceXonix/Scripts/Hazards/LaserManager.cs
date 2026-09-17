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

        public LaserEmitter[] Emitters => emitters;
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
                emitter.SetDefinition(placement.definition);
                var cell = placement.definition.axis == LaserAxis.Horizontal
                    ? new GridCoordinate(0, placement.line)
                    : new GridCoordinate(placement.line, 0);
                var position = boardManager.GetWorldPosition(cell);
                position.z = emitter.transform.position.z;
                emitter.transform.position = position;
                emitter.Initialize(boardManager, gameManager, poolService, warningPrefab, beamPrefab);
            }
        }

        private void Start()
        {
            if (gameManager != null) gameManager.StageCompleted += ShutdownEmitters;
            if (emitters == null) return;
            for (var i = 0; i < emitters.Length; i++)
                if (emitters[i] != null) emitters[i].Initialize(boardManager, gameManager, poolService, warningPrefab, beamPrefab);
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
