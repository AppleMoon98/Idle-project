using Combat;
using Core;
using Managers;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// StageSO에 정의된 스폰 웨이브를 순서대로 시간차 실행해 몬스터를 생성한다.
    /// 몬스터는 항상 플레이어 반대편(화면 밖)에서 등장하도록, 스폰마다 플레이어가
    /// 화면 상단에 있는지 확인해 상단/하단 스폰 지점 중 하나를 고른다.
    /// </summary>
    public sealed class MonsterSpawner : ITickable
    {
        private readonly MonsterSpawnEntry[] _entries;
        private readonly PoolManager _pool;
        private readonly Transform[] _topSpawnPoints;
        private readonly Transform[] _bottomSpawnPoints;
        private readonly Transform _playerTarget;
        private readonly StageProgressTracker _tracker;
        private readonly Camera _camera;
        private readonly float _playerNearTopViewportThreshold;

        private int _entryIndex;
        private int _spawnedInEntry;
        private float _elapsed;
        private int _topCursor;
        private int _bottomCursor;

        /// <summary>
        /// 모든 웨이브의 스폰이 끝났는지 여부.
        /// </summary>
        public bool IsFinished => _entryIndex >= _entries.Length;

        public MonsterSpawner(
            StageSO stage,
            PoolManager pool,
            Transform[] topSpawnPoints,
            Transform[] bottomSpawnPoints,
            Transform playerTarget,
            StageProgressTracker tracker,
            float playerNearTopViewportThreshold)
        {
            _entries = stage.SpawnEntries;
            _pool = pool;
            _topSpawnPoints = topSpawnPoints;
            _bottomSpawnPoints = bottomSpawnPoints;
            _playerTarget = playerTarget;
            _tracker = tracker;
            _playerNearTopViewportThreshold = playerNearTopViewportThreshold;
            _camera = Camera.main;
        }

        public void Tick(float deltaTime)
        {
            if (IsFinished)
            {
                return;
            }

            _elapsed += deltaTime;

            MonsterSpawnEntry entry = _entries[_entryIndex];

            if (_elapsed < entry.SpawnInterval)
            {
                return;
            }

            _elapsed = 0f;
            SpawnOne(entry);

            _spawnedInEntry++;

            if (_spawnedInEntry >= entry.Count)
            {
                _spawnedInEntry = 0;
                _entryIndex++;
            }
        }

        private void SpawnOne(MonsterSpawnEntry entry)
        {
            Transform spawnPoint = NextSpawnPoint();
            GameObject instance = _pool.Get(entry.MonsterPrefab, spawnPoint.position, spawnPoint.rotation);

            if (instance.TryGetComponent(out MonsterTargetSelector targetSelector))
            {
                targetSelector.Initialize(_playerTarget);
            }

            _tracker.RegisterSpawned(instance);
        }

        /// <summary>
        /// 플레이어가 화면 상단에 있으면 하단 스폰 지점을, 아니면 상단 스폰 지점을 사용한다.
        /// </summary>
        private Transform NextSpawnPoint()
        {
            if (IsPlayerNearTop())
            {
                Transform point = _bottomSpawnPoints[_bottomCursor % _bottomSpawnPoints.Length];
                _bottomCursor++;
                return point;
            }

            Transform topPoint = _topSpawnPoints[_topCursor % _topSpawnPoints.Length];
            _topCursor++;
            return topPoint;
        }

        private bool IsPlayerNearTop()
        {
            if (_camera == null || _playerTarget == null)
            {
                return false;
            }

            Vector3 viewportPoint = _camera.WorldToViewportPoint(_playerTarget.position);
            return viewportPoint.y > _playerNearTopViewportThreshold;
        }
    }
}
