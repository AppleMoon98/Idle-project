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
    /// 몬스터는 항상 플레이어 반대편(화면 밖)에서 등장하도록, 스폰마다 플레이어의 뷰포트 좌표가
    /// 중심(0.5, 0.5)에서 세로/가로 중 어느 축으로 더 벗어나 있는지 보고 그 축의 반대쪽
    /// (상/하/좌/우 4방향 중 하나) 스폰 지점을 고른다. 전술 웨이브(TacticSpawnEntry)를 먼저
    /// 처리하고 - 스테이지에 입장하자마자 대형부터 즉시 구성되도록 - 그게 모두 끝나면 이어서
    /// 일반 웨이브(MonsterSpawnEntry)를 처리한다. 전술 대형은 한 마리씩 시간차로 나오지 않고,
    /// 한 틱에 리더/추종자 전원이 한꺼번에 스폰된다("뭉텅이로 조금씩"이 아니라 "대형이 즉시
    /// 갖춰진다").
    /// </summary>
    public sealed class MonsterSpawner : ITickable, IDisposable
    {
        /// <summary>
        /// 화면 밖 스폰 지점의 네 방향. 전술 대형 배치(PrepareFormationLayout)에서 Top/Bottom은
        /// 리더 줄이 X축을 따라 늘어서고 Left/Right는 Y축을 따라 늘어선다.
        /// </summary>
        private enum SpawnSide
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private static readonly Dictionary<TacticType, ITacticSpawnStrategy> TacticStrategies = new()
        {
            { TacticType.ShieldWall, new ShieldWallTacticStrategy() }
        };

        private readonly MonsterSpawnEntry[] _entries;
        private readonly TacticSpawnEntry[] _tacticEntries;
        private readonly PoolManager _pool;
        private readonly Transform[] _topSpawnPoints;
        private readonly Transform[] _bottomSpawnPoints;
        private readonly Transform[] _leftSpawnPoints;
        private readonly Transform[] _rightSpawnPoints;
        private readonly Transform _playerTarget;
        private readonly StageProgressTracker _tracker;
        private readonly Camera _camera;
        private readonly float _statMultiplier;
        private readonly float _tacticUnitSpacing;
        private readonly float _tacticRowSpacing;
        private readonly List<GameObject> _pendingLeaders = new();
        private readonly List<GameObject> _pendingFollowers = new();
        private readonly List<ITacticFormationGroup> _formationGroups = new();

        private int _entryIndex;
        private int _spawnedInEntry;
        private float _elapsed;
        private int _tacticEntryIndex;
        private int _topCursor;
        private int _bottomCursor;
        private int _leftCursor;
        private int _rightCursor;
        private Vector3[] _formationLeaderPositions;
        private Vector3[] _formationFollowerPositions;

        /// <summary>
        /// 모든 웨이브(일반 + 전술)의 스폰이 끝났는지 여부. 전술 대형이 아직 전멸하지 않았다면
        /// (뒤따를 일반 웨이브가 아직 대기 중이므로) 끝난 것으로 보지 않는다.
        /// </summary>
        public bool IsFinished => _entryIndex >= _entries.Length && _tacticEntryIndex >= _tacticEntries.Length && AllFormationGroupsCleared();

        public MonsterSpawner(
            StageSO stage,
            PoolManager pool,
            Transform[] topSpawnPoints,
            Transform[] bottomSpawnPoints,
            Transform[] leftSpawnPoints,
            Transform[] rightSpawnPoints,
            Transform playerTarget,
            StageProgressTracker tracker,
            float statMultiplier,
            float tacticUnitSpacing,
            float tacticRowSpacing)
        {
            _entries = stage.SpawnEntries ?? Array.Empty<MonsterSpawnEntry>();
            _tacticEntries = stage.TacticEntries ?? Array.Empty<TacticSpawnEntry>();
            _pool = pool;
            _topSpawnPoints = topSpawnPoints;
            _bottomSpawnPoints = bottomSpawnPoints;
            _leftSpawnPoints = leftSpawnPoints;
            _rightSpawnPoints = rightSpawnPoints;
            _playerTarget = playerTarget;
            _tracker = tracker;
            _statMultiplier = statMultiplier;
            _tacticUnitSpacing = tacticUnitSpacing;
            _tacticRowSpacing = tacticRowSpacing;
            _camera = Camera.main;
        }

        public void Tick(float deltaTime)
        {
            if (_tacticEntryIndex < _tacticEntries.Length)
            {
                TickTactics();
                return;
            }

            // 전술 대형(예: 방패벽)이 아직 전멸하지 않았다면, 뒤따를 일반 웨이브(엘리트/보스 등)를
            // 보류한다 - 대형이 살아 움직이는 전장에 엘리트/보스가 끼어들어 대형 사이로
            // 뒤섞이는 것을 막기 위해서다.
            if (!AllFormationGroupsCleared())
            {
                return;
            }

            if (_entryIndex < _entries.Length)
            {
                TickEntries(deltaTime);
            }
        }

        private bool AllFormationGroupsCleared()
        {
            foreach (ITacticFormationGroup group in _formationGroups)
            {
                if (!group.IsCleared)
                {
                    return false;
                }
            }

            return true;
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

        /// <summary>
        /// 남은 전술 엔트리를 전부 즉시(한 틱에) 처리한다 - 각 엔트리는 시간차 없이 리더/추종자
        /// 전원이 한꺼번에 스폰된다.
        /// </summary>
        private void TickTactics()
        {
            while (_tacticEntryIndex < _tacticEntries.Length)
            {
                SpawnFormation(_tacticEntries[_tacticEntryIndex]);
                _tacticEntryIndex++;
            }
        }

        private void SpawnFormation(TacticSpawnEntry entry)
        {
            SpawnSide side = DetermineSpawnSide();
            int pairCount = PrepareFormationLayout(entry, side);

            for (int i = 0; i < pairCount; i++)
            {
                GameObject leader = SpawnInstance(entry.LeaderPrefab, null, _formationLeaderPositions[i]);

                GameObject followerPrefab = entry.FollowerPrefab;

                if (entry.AlternateFollowerPrefab != null && UnityEngine.Random.value < entry.AlternateFollowerChance)
                {
                    followerPrefab = entry.AlternateFollowerPrefab;
                }

                GameObject follower = SpawnInstance(followerPrefab, null, _formationFollowerPositions[i]);
                ConfigureAsFormationFollower(follower);

                _pendingLeaders.Add(leader);
                _pendingFollowers.Add(follower);
            }

            if (TacticStrategies.TryGetValue(entry.Type, out ITacticSpawnStrategy strategy))
            {
                _formationGroups.Add(strategy.CreateFormationGroup(_pendingLeaders, _pendingFollowers));
            }

            _pendingLeaders.Clear();
            _pendingFollowers.Clear();
        }

        /// <summary>
        /// 전술 대형 한 벌의 스폰 위치를 미리 계산한다 - 리더(1열)는 고정된 스폰 지점 한 줄을
        /// 따라 나란히, 추종자(2열)는 그보다 rowSpacing만큼 더 화면 밖(플레이어 반대쪽)으로
        /// 물러난 평행한 줄을 따라 나란히 선다. Top/Bottom은 화면 가로(X축)를 따라 늘어서고
        /// Left/Right는 화면 세로(Y축)를 따라 늘어선다 - 스폰 지점이 놓인 화면 가장자리와
        /// 평행한 방향이 "줄"이고, 그 가장자리에서 더 바깥으로 물러나는 방향이 "열 간격"이다.
        /// 기존 NextSpawnPoint()처럼 몇 개 안 되는 스폰 지점을 순환 재사용하면 다수의 쌍이 같은
        /// 좌표 근처에 뭉쳐 스폰되므로(대형처럼 안 보이고, 스플래시 등 광역 판정에도 의도치 않게
        /// 한꺼번에 걸림) 전술 스폰만은 매 쌍마다 서로 다른 고유 좌표를 미리 계산해 쓴다.
        /// entry.TotalUnitCount를 절반으로 나눈 쌍의 수를 반환한다.
        /// </summary>
        private int PrepareFormationLayout(TacticSpawnEntry entry, SpawnSide side)
        {
            Transform[] points = GetSpawnPoints(side);
            Vector3 anchor = points[0].position;
            bool alongX = side == SpawnSide.Top || side == SpawnSide.Bottom;
            float outwardSign = side == SpawnSide.Top || side == SpawnSide.Right ? 1f : -1f;

            int pairCount = Mathf.Max(entry.TotalUnitCount / 2, 0);
            _formationLeaderPositions = new Vector3[pairCount];
            _formationFollowerPositions = new Vector3[pairCount];

            float anchorAlong = alongX ? anchor.x : anchor.y;
            float start = anchorAlong - (pairCount - 1) * _tacticUnitSpacing * 0.5f;

            for (int i = 0; i < pairCount; i++)
            {
                float along = start + i * _tacticUnitSpacing;

                if (alongX)
                {
                    _formationLeaderPositions[i] = new Vector3(along, anchor.y, anchor.z);
                    _formationFollowerPositions[i] = new Vector3(along, anchor.y + outwardSign * _tacticRowSpacing, anchor.z);
                }
                else
                {
                    _formationLeaderPositions[i] = new Vector3(anchor.x, along, anchor.z);
                    _formationFollowerPositions[i] = new Vector3(anchor.x + outwardSign * _tacticRowSpacing, along, anchor.z);
                }
            }

            return pairCount;
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
            SpawnInstance(entry.MonsterPrefab, entry.VisualSet, null);
        }

        /// <summary>
        /// explicitPosition이 주어지면(전술 대형의 각 자리) 그 좌표에 그대로 스폰하고,
        /// 아니면(일반 웨이브) 기존처럼 NextSpawnPoint()로 화면 밖 스폰 지점을 순환한다.
        /// </summary>
        private GameObject SpawnInstance(GameObject prefab, MonsterVisualSetSO visualSet, Vector3? explicitPosition)
        {
            Vector3 position;
            Quaternion rotation;

            if (explicitPosition.HasValue)
            {
                position = explicitPosition.Value;
                rotation = Quaternion.identity;
            }
            else
            {
                Transform spawnPoint = NextSpawnPoint();
                position = spawnPoint.position;
                rotation = spawnPoint.rotation;
            }

            GameObject instance = _pool.Get(prefab, position, rotation);

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
        /// DetermineSpawnSide()가 고른 방향의 스폰 지점 배열을 순환하며 다음 지점을 반환한다.
        /// </summary>
        private Transform NextSpawnPoint()
        {
            SpawnSide side = DetermineSpawnSide();
            Transform[] points = GetSpawnPoints(side);

            switch (side)
            {
                case SpawnSide.Bottom:
                    return points[_bottomCursor++ % points.Length];
                case SpawnSide.Left:
                    return points[_leftCursor++ % points.Length];
                case SpawnSide.Right:
                    return points[_rightCursor++ % points.Length];
                default:
                    return points[_topCursor++ % points.Length];
            }
        }

        private Transform[] GetSpawnPoints(SpawnSide side)
        {
            return side switch
            {
                SpawnSide.Bottom => _bottomSpawnPoints,
                SpawnSide.Left => _leftSpawnPoints,
                SpawnSide.Right => _rightSpawnPoints,
                _ => _topSpawnPoints
            };
        }

        /// <summary>
        /// 플레이어의 뷰포트 좌표가 화면 중심(0.5, 0.5)에서 세로/가로 중 더 많이 벗어난 축을 골라
        /// 그 반대쪽 방향을 반환한다 - 항상 플레이어와 가장 먼 화면 가장자리에서 스폰된다.
        /// 카메라/플레이어를 아직 쓸 수 없으면 기존 기본 동작과 같은 Top으로 대체한다.
        /// </summary>
        private SpawnSide DetermineSpawnSide()
        {
            if (_camera == null || _playerTarget == null)
            {
                return SpawnSide.Top;
            }

            Vector3 viewportPoint = _camera.WorldToViewportPoint(_playerTarget.position);
            float verticalOffset = viewportPoint.y - 0.5f;
            float horizontalOffset = viewportPoint.x - 0.5f;

            if (Mathf.Abs(verticalOffset) >= Mathf.Abs(horizontalOffset))
            {
                return verticalOffset > 0f ? SpawnSide.Bottom : SpawnSide.Top;
            }

            return horizontalOffset > 0f ? SpawnSide.Left : SpawnSide.Right;
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
