using Character;
using Core;
using Managers;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// StageSO에 정의된 스폰 웨이브를 순서대로 시간차 실행해 몬스터를 생성한다.
    /// </summary>
    public sealed class MonsterSpawner : ITickable
    {
        private readonly MonsterSpawnEntry[] _entries;
        private readonly PoolManager _pool;
        private readonly Transform[] _spawnPoints;
        private readonly Transform _playerTarget;
        private readonly StageProgressTracker _tracker;

        private int _entryIndex;
        private int _spawnedInEntry;
        private float _elapsed;
        private int _spawnPointCursor;

        /// <summary>
        /// 모든 웨이브의 스폰이 끝났는지 여부.
        /// </summary>
        public bool IsFinished => _entryIndex >= _entries.Length;

        public MonsterSpawner(
            StageSO stage,
            PoolManager pool,
            Transform[] spawnPoints,
            Transform playerTarget,
            StageProgressTracker tracker)
        {
            _entries = stage.SpawnEntries;
            _pool = pool;
            _spawnPoints = spawnPoints;
            _playerTarget = playerTarget;
            _tracker = tracker;
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

            if (instance.TryGetComponent(out CharacterMover mover))
            {
                mover.Target = _playerTarget;
            }

            _tracker.RegisterSpawned(instance);
        }

        private Transform NextSpawnPoint()
        {
            Transform point = _spawnPoints[_spawnPointCursor];
            _spawnPointCursor = (_spawnPointCursor + 1) % _spawnPoints.Length;
            return point;
        }
    }
}
