using System.Collections.Generic;
using UnityEngine;
namespace SpaceXonix.Pooling
{
    public sealed class PoolService : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly HashSet<GameObject> releasedInstances = new HashSet<GameObject>();
        public GameObject Acquire(GameObject prefab, Transform parent)
        {
            if (!pools.TryGetValue(prefab, out var pool)) pools[prefab] = pool = new Stack<GameObject>();
            var instance = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, parent);
            releasedInstances.Remove(instance);
            instance.transform.SetParent(parent, false); instance.SetActive(true); return instance;
        }
        public void Release(GameObject prefab, GameObject instance)
        {
            if (instance == null || !releasedInstances.Add(instance)) return;
            if (!pools.TryGetValue(prefab, out var pool)) pools[prefab] = pool = new Stack<GameObject>();
            instance.SetActive(false); pool.Push(instance);
        }
    }
}
