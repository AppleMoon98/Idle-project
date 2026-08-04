using System;
using System.Collections.Generic;
using Core;
using Core.Pooling;
using UnityEngine;

namespace Managers
{
    /// <summary>
    /// 프리팹별 GameObject 풀을 관리하는 오케스트레이터.
    /// RegisterPool로 등록한 프리팹에 한해 Get/Release만으로 Instantiate/Destroy를 대체한다.
    /// </summary>
    public sealed class PoolManager : IManager, IService
    {
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();
        private Transform _poolRoot;

        public void Initialize()
        {
            _poolRoot = new GameObject("[PoolManager]").transform;
        }

        public void Shutdown()
        {
            foreach (ObjectPool<GameObject> pool in _pools.Values)
            {
                pool.Clear();
            }

            _pools.Clear();

            if (_poolRoot != null)
            {
                UnityEngine.Object.Destroy(_poolRoot.gameObject);
                _poolRoot = null;
            }
        }

        /// <summary>
        /// 프리팹에 대한 풀을 등록하고 defaultCapacity만큼 미리 생성한다.
        /// </summary>
        public void RegisterPool(GameObject prefab, int defaultCapacity, int maxSize)
        {
            if (_pools.ContainsKey(prefab))
            {
                throw new InvalidOperationException($"Pool for prefab '{prefab.name}' is already registered.");
            }

            var pool = new ObjectPool<GameObject>(
                createFunc: () => CreateInstance(prefab),
                onGet: instance => instance.SetActive(true),
                onRelease: ReturnToRoot,
                onDestroy: instance => UnityEngine.Object.Destroy(instance),
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);

            _pools.Add(prefab, pool);
        }

        /// <summary>
        /// 프리팹에 대한 풀이 아직 등록되어 있지 않은 경우에만 등록한다.
        /// 여러 스테이지에서 같은 몬스터 프리팹을 재사용할 때 중복 등록 예외를 피하기 위함이다.
        /// </summary>
        public void EnsurePool(GameObject prefab, int defaultCapacity, int maxSize)
        {
            if (_pools.ContainsKey(prefab))
            {
                return;
            }

            RegisterPool(prefab, defaultCapacity, maxSize);
        }

        /// <summary>
        /// 등록된 풀에서 인스턴스를 꺼내 지정한 위치/회전으로 배치한다.
        /// </summary>
        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!_pools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
            {
                throw new InvalidOperationException($"Pool for prefab '{prefab.name}' is not registered. Call RegisterPool first.");
            }

            GameObject instance = pool.Get();
            instance.transform.SetPositionAndRotation(position, rotation);
            NotifySpawned(instance);
            return instance;
        }

        /// <summary>
        /// 인스턴스를 원본 풀로 반납한다. PooledInstance 태그로 출처 풀을 자동 판별한다.
        /// </summary>
        public void Release(GameObject instance)
        {
            if (!instance.TryGetComponent(out PooledInstance tag) ||
                !_pools.TryGetValue(tag.SourcePrefab, out ObjectPool<GameObject> pool))
            {
                throw new InvalidOperationException($"'{instance.name}' was not spawned by this PoolManager.");
            }

            NotifyDespawned(instance);
            pool.Release(instance);
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, _poolRoot);
            instance.SetActive(false);

            PooledInstance tag = instance.AddComponent<PooledInstance>();
            tag.SourcePrefab = prefab;

            return instance;
        }

        private void ReturnToRoot(GameObject instance)
        {
            instance.SetActive(false);
            instance.transform.SetParent(_poolRoot);
        }

        private static void NotifySpawned(GameObject instance)
        {
            foreach (IPoolable poolable in instance.GetComponents<IPoolable>())
            {
                poolable.OnSpawned();
            }
        }

        private static void NotifyDespawned(GameObject instance)
        {
            foreach (IPoolable poolable in instance.GetComponents<IPoolable>())
            {
                poolable.OnDespawned();
            }
        }
    }
}
