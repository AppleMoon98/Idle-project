using System;
using System.Collections.Generic;
using Character;
using Combat;
using Core;
using Managers;
using Stage.Tactics;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// StageSO에 정의된 스폰 웨이브를 순서대로 시간차 실행해 몬스터를 생성한다.
    /// 몬스터는 항상 플레이어 반대편(화면 밖)에서 등장하도록, 스폰마다 플레이어가
    /// 화면 상단에 있는지 확인해 상단/하단 스폰 지점 중 하나를 고른다. 전술 웨이브
    /// (TacticSpawnEntry)를 먼저 처리하고 - 스테이지에 입장하자마자 대형부터 즉시 구성되도록 -
    /// 그게 모두 끝나면 이어서 일반 웨이브(MonsterSpawnEntry)를 처리한다. 전술 대형 하나를
    /// 스폰하는 동안은 상/하단 판정을 매번 다시 하지 않고 그 대형을 시작할 때 한 번만 정해서
    /// 고정한다 - 대형이 스폰되는 도중 플레이어가 화면 중앙을 넘나들어도 같은 쪽에서 계속
    /// 나와야 "대형"으로 보이기 때문이다.
    /// </summary>
    public sealed class MonsterSpawner : ITickable, IDisposable
    {
        private static readonly Dictionary<TacticType, ITacticSpawnStrategy> TacticStrategies = new()
        {
            { TacticType.ShieldWall, new ShieldWallTacticStrategy() }
        };

        private readonly MonsterSpawnEntry[] _entries;
        private readonly TacticSpawnEntry[] _tacticEntries;
        private readonly PoolManager _pool;
        private readonly Transform[] _topSpawnPoints;
        private readonly Transform[] _bottomSpawnPoints;
        private readonly Transform _playerTarget;
        private readonly StageProgressTracker _tracker;
        private readonly Camera _camera;
        private readonly float _playerNearTopViewportThreshold;
        private readonly float _statMultiplier;
        private readonly List<GameObject> _pendingLeaders = new();
        private readonly List<GameObject> _pendingFollowers = new();
        private readonly List<ITacticFormationGroup> _formationGroups = new();

        private int _entryIndex;
        private int _spawnedInEntry;
        private float _elapsed;
        private int _tacticEntryIndex;
        private int _pairsSpawnedInEntry;
        private float _tacticElapsed;
        private int _topCursor;
        private int _bottomCursor;
        private bool? _tacticFormationSideIsBottom;

        /// <summary>
        /// 모든 웨이브(일반 + 전술)의 스폰이 끝났는지 여부.
        /// </summary>
        public bool IsFinished => _entryIndex >= _entries.Length && _tacticEntryIndex >= _tacticEntries.Length;

        public MonsterSpawner(
            StageSO stage,
            PoolManager pool,
            Transform[] topSpawnPoints,
            Transform[] bottomSpawnPoints,
            Transform playerTarget,
            StageProgressTracker tracker,
            float playerNearTopViewportThreshold,
            float statMultiplier)
        {
            _entries = stage.SpawnEntries ?? Array.Empty<MonsterSpawnEntry>();
            _tacticEntries = stage.TacticEntries ?? Array.Empty<TacticSpawnEntry>();
            _pool = pool;
            _topSpawnPoints = topSpawnPoints;
            _bottomSpawnPoints = bottomSpawnPoints;
            _playerTarget = playerTarget;
            _tracker = tracker;
            _playerNearTopViewportThreshold = playerNearTopViewportThreshold;
            _statMultiplier = statMultiplier;
            _camera = Camera.main;
        }

        public void Tick(float deltaTime)
        {
            if (_tacticEntryIndex < _tacticEntries.Length)
            {
                TickTactics(deltaTime);
                return;
            }

            if (_entryIndex < _entries.Length)
            {
                TickEntries(deltaTime);
            }
        }

        private void TickEntries(float deltaTime)
        {
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

        private void TickTactics(float deltaTime)
        {
            if (_pairsSpawnedInEntry == 0 && _tacticFormationSideIsBottom == null)
            {
                _tacticFormationSideIsBottom = IsPlayerNearTop();
            }

            _tacticElapsed += deltaTime;

            TacticSpawnEntry entry = _tacticEntries[_tacticEntryIndex];

            if (_tacticElapsed < entry.SpawnInterval)
            {
                return;
            }

            _tacticElapsed = 0f;
            SpawnPair(entry);

            _pairsSpawnedInEntry++;

            if (_pairsSpawnedInEntry >= entry.PairCount)
            {
                if (TacticStrategies.TryGetValue(entry.Type, out ITacticSpawnStrategy strategy))
                {
                    _formationGroups.Add(strategy.CreateFormationGroup(_pendingLeaders, _pendingFollowers));
                }

                _pendingLeaders.Clear();
                _pendingFollowers.Clear();
                _pairsSpawnedInEntry = 0;
                _tacticFormationSideIsBottom = null;
                _tacticEntryIndex++;
            }
        }

        private void SpawnPair(TacticSpawnEntry entry)
        {
            GameObject leader = SpawnInstance(entry.LeaderPrefab, null);

            GameObject followerPrefab = entry.FollowerPrefab;

            if (entry.AlternateFollowerPrefab != null && UnityEngine.Random.value < entry.AlternateFollowerChance)
            {
                followerPrefab = entry.AlternateFollowerPrefab;
            }

            GameObject follower = SpawnInstance(followerPrefab, null);
            ConfigureAsFormationFollower(follower);

            _pendingLeaders.Add(leader);
            _pendingFollowers.Add(follower);
        }

        /// <summary>
        /// 대형의 2열로 스폰된 인스턴스를 FormationFollower 모드로 전환한다 - 창병은 원래
        /// FormationFollower만 갖고 있어 사실상 no-op이지만, 궁병처럼 평소엔 RangedKiter로
        /// 독립적으로 움직이던 유닛이 대형에 편입될 때는 그쪽을 끄고 FormationFollower를 켠다.
        /// FormationFollower는 IMonsterMovementInitializer를 구현하지 않으므로(위 클래스 doc 참고)
        /// SpawnInstance의 범용 초기화로는 호출되지 않아 여기서 명시적으로 Initialize한다.
        /// </summary>
        private void ConfigureAsFormationFollower(GameObject instance)
        {
            if (instance.TryGetComponent(out RangedKiter kiter))
            {
                kiter.enabled = false;
            }

            if (instance.TryGetComponent(out FormationFollower follower))
            {
                follower.enabled = true;
                follower.Initialize(_playerTarget);
            }
        }

        private void SpawnOne(MonsterSpawnEntry entry)
        {
            SpawnInstance(entry.MonsterPrefab, entry.VisualSet);
        }

        private GameObject SpawnInstance(GameObject prefab, MonsterVisualSetSO visualSet)
        {
            Transform spawnPoint = NextSpawnPoint();
            GameObject instance = _pool.Get(prefab, spawnPoint.position, spawnPoint.rotation);

            if (instance.TryGetComponent(out StageMonsterScaler scaler))
            {
                scaler.ApplyScale(_statMultiplier);
            }

            if (instance.TryGetComponent(out IMonsterMovementInitializer movementInitializer))
            {
                movementInitializer.Initialize(_playerTarget);
            }

            // 세트가 없어도 무조건 호출한다 - StageMonsterScaler.ApplyScale과 같은 이유로,
            // 풀에서 재사용된 인스턴스가 이전 스폰의 스킨을 그대로 들고 있으면 안 되기 때문이다.
            if (instance.TryGetComponent(out MonsterVisualRandomizer visual))
            {
                visual.ApplyVisualSet(visualSet);
            }

            _tracker.RegisterSpawned(instance);
            return instance;
        }

        /// <summary>
        /// 플레이어가 화면 상단에 있으면 하단 스폰 지점을, 아니면 상단 스폰 지점을 사용한다.
        /// 전술 대형을 스폰하는 도중이면(_tacticFormationSideIsBottom 고정됨) 매번 다시 판정하지
        /// 않고 그 대형이 시작할 때 정한 쪽을 그대로 쓴다.
        /// </summary>
        private Transform NextSpawnPoint()
        {
            bool useBottom = _tacticFormationSideIsBottom ?? IsPlayerNearTop();

            if (useBottom)
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

        /// <summary>
        /// 이 스포너가 만든 전술 대형(ITacticFormationGroup)들의 EventBus 구독을 모두 해제한다.
        /// 스테이지가 도중에 끝나면(플레이어 사망/전환) 남은 몬스터는 죽지 않고 풀로 강제
        /// 반환되므로(StageProgressTracker.ReleaseRemaining), 대형의 CharacterDiedEvent 구독이
        /// 자연스럽게 끝나지 않는다 - StageController.EndCurrentStage가 이 메서드를 명시적으로 호출한다.
        /// </summary>
        public void Dispose()
        {
            foreach (ITacticFormationGroup group in _formationGroups)
            {
                group.Dispose();
            }

            _formationGroups.Clear();
        }
    }
}
