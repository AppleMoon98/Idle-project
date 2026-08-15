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

            // worldPositionStays를 생략(기본값 true)하면 이전 부모와 _poolRoot의 월드 스케일이
            // 다를 때마다(예: Canvas 하위 UI, 월드 스케일 ≈1.06) 로컬 스케일이 매번 재계산돼
            // 반납할 때마다 커진다 - 다음 Get()이 SetParent(newParent, false)로 그 값을 그대로
            // 물려받으므로 반납↔재사용이 반복될수록 계속 곱연산으로 누적된다. 비활성화된 상태라
            // 월드 좌표를 보존할 이유가 없으므로 항상 false로 고정한다.
            instance.transform.SetParent(_poolRoot, worldPositionStays: false);
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
