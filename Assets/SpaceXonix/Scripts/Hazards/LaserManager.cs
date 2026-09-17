using SpaceXonix.Board;
using SpaceXonix.Core;
using SpaceXonix.Pooling;
using UnityEngine;

namespace SpaceXonix.Hazards
{
    public sealed class LaserManager : MonoBehaviour
    {
        [SerializeField] private BoardManager boardManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PoolService poolService;
        [SerializeField] private GameObject warningPrefab;
        [SerializeField] private GameObject beamPrefab;
        [SerializeField] private LaserEmitter[] emitters;

        public LaserEmitter[] Emitters => emitters;

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
            for (var i = 0; i < emitters.Length; i++) if (emitters[i] != null) emitters[i].Tick(deltaTime);
        }
    }
}
