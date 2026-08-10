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
    /// 처리하고 - 스테이지에 입장하자마자 대형부터 구성되도록 - 그게 모두 끝나면 이어서
    /// 일반 웨이브(MonsterSpawnEntry)를 처리한다. 전술 대형은 쌍(리더+추종자) 단위로
    /// <see cref="TacticSpawnEntry.PairSpawnInterval"/>만큼 시간차를 두고 스폰된다 - 이
    /// 값이 0(기본값)이면 한 틱에 전원이 한꺼번에 스폰되어(대형이 즉시 갖춰짐), 0보다 크면
    /// 쌍마다 그 간격만큼 늦춰 스폰돼 대형 전체가 한꺼번에 교전 가능해지는 상황을 완화한다.
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

        /// <summary>
        /// BehindTacticFormation 배치가 대형 후방으로 물러나는 거리 = tacticRowSpacing × 이 배수.
        /// 추종자(2열)가 이미 리더보다 rowSpacing만큼 물러나 있으므로, 그보다 한 겹 더 뒤에 서게 된다.
        /// </summary>
        private const float BehindFormationDistanceMultiplier = 2f;

        private int _entryIndex;
        private int _spawnedInEntry;
        private float _elapsed;
        private int _tacticEntryIndex;
        private bool _tacticEntryStarted;
        private int _tacticPairIndex;
        private float _tacticPairElapsed;
        private int _topCursor;
        private int _bottomCursor;
        private int _leftCursor;
        private int _rightCursor;
        private Vector3[] _formationLeaderPositions;
        private Vector3[] _formationFollowerPositions;
        private Vector3 _lastFormationAnchor;
        private bool _lastFormationAlongX;
        private float _lastFormationOutwardSign;
        private bool _hasFormationPlacement;

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
                TickTactics(deltaTime);
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
        /// 현재 전술 엔트리를 쌍(리더+추종자) 단위로 시간차를 두고 스폰한다.
        /// <see cref="TacticSpawnEntry.PairSpawnInterval"/>이 0(기본값)이면 첫 틱에 모든 쌍이
        /// 즉시 스폰되어 기존 동작과 동일하다 - 0보다 크면 그 간격만큼 쌍마다 시간차를 둬서,
        /// 대형 전체가 한꺼번에 교전 가능해지는 상황(예: N-40 방패벽의 41마리 동시 피격)을
        /// 완화한다. 마지막 전술 엔트리의 마지막 쌍까지 다 스폰되면 이어서
        /// spawnWithTactics 일반 웨이브(기마병/기사 등)를 처리한다.
        /// </summary>
        private void TickTactics(float deltaTime)
        {
            TacticSpawnEntry entry = _tacticEntries[_tacticEntryIndex];

            if (!_tacticEntryStarted)
            {
                SpawnSide side = DetermineSpawnSide();
                PrepareFormationLayout(entry, side);
                _tacticPairIndex = 0;
                _tacticPairElapsed = entry.PairSpawnInterval;
                _tacticEntryStarted = true;

                if (_formationLeaderPositions.Length == 0)
                {
                    FinishTacticEntry(entry);
                    return;
                }
            }

            int totalPairs = _formationLeaderPositions.Length;
            _tacticPairElapsed += deltaTime;

            while (_tacticPairIndex < totalPairs && _tacticPairElapsed >= entry.PairSpawnInterval)
            {
                _tacticPairElapsed -= entry.PairSpawnInterval;
                SpawnFormationPair(entry, _tacticPairIndex);
                _tacticPairIndex++;
            }

            if (_tacticPairIndex >= totalPairs)
            {
                FinishTacticEntry(entry);
            }
        }

        /// <summary>
        /// 대형의 쌍(리더+추종자) 하나를 index 자리에 스폰한다.
        /// </summary>
        private void SpawnFormationPair(TacticSpawnEntry entry, int index)
        {
            GameObject leader = SpawnInstance(entry.LeaderPrefab, null, _formationLeaderPositions[index]);

            GameObject followerPrefab = entry.FollowerPrefab;

            if (entry.AlternateFollowerPrefab != null && UnityEngine.Random.value < entry.AlternateFollowerChance)
            {
                followerPrefab = entry.AlternateFollowerPrefab;
            }

            GameObject follower = SpawnInstance(followerPrefab, null, _formationFollowerPositions[index]);
            ConfigureAsFormationFollower(follower);

            _pendingLeaders.Add(leader);
            _pendingFollowers.Add(follower);
        }

        /// <summary>
        /// 현재 전술 엔트리의 모든 쌍이 다 스폰된 뒤 대형 그룹을 확정하고, 다음 전술 엔트리로
        /// 넘어간다(더 없으면 spawnWithTactics 일반 웨이브를 이어서 처리).
        /// </summary>
        private void FinishTacticEntry(TacticSpawnEntry entry)
        {
            if (TacticStrategies.TryGetValue(entry.Type, out ITacticSpawnStrategy strategy))
            {
                _formationGroups.Add(strategy.CreateFormationGroup(_pendingLeaders, _pendingFollowers));
            }

            _pendingLeaders.Clear();
            _pendingFollowers.Clear();

            _tacticEntryIndex++;
            _tacticEntryStarted = false;

            if (_tacticEntryIndex >= _tacticEntries.Length)
            {
                SpawnImmediateEntries();
            }
        }

        /// <summary>
        /// spawnEntries 배열 앞쪽부터, spawnWithTactics가 켜진 항목을 만나는 동안 그 Count 전부를
        /// 시간차 없이 즉시 스폰하고 _entryIndex를 그만큼 건너뛴다. 전술 웨이브가 이미 배치를 끝낸
        /// 직후(같은 틱)에 호출되므로, BehindTacticFormation 배치가 방금 계산된 대형 좌표를 그대로
        /// 참조할 수 있다. 이후 TickEntries는 남은(즉시 스폰이 아닌) 항목부터 기존처럼 처리한다.
        /// </summary>
        private void SpawnImmediateEntries()
        {
            while (_entryIndex < _entries.Length && _entries[_entryIndex].SpawnWithTactics)
            {
                MonsterSpawnEntry entry = _entries[_entryIndex];

                for (int i = 0; i < entry.Count; i++)
                {
                    SpawnOne(entry);
                }

                _entryIndex++;
            }
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
        /// 줄의 중심은 그 방향 스폰 지점들의 along축 좌표 평균이다(points[0]만 기준으로 삼으면
        /// 그 지점이 화면 중앙이 아닌 한쪽 끝(예: SpawnPoint_Top_Left)에 있을 때 대형 전체가
        /// 그쪽으로 쏠려 배치된다). 배열 순서(=스폰 순서)도 중심에서 바깥으로(0, +1칸, -1칸,
        /// +2칸, -2칸, ...) 나가도록 채워, 대형이 한쪽 끝이 아니라 중앙에서부터 나타나 좌우로
        /// 벌어지는 것처럼 보이게 한다. entry.TotalUnitCount를 절반으로 나눈 쌍의 수를 반환한다.
        /// </summary>
        private int PrepareFormationLayout(TacticSpawnEntry entry, SpawnSide side)
        {
            Transform[] points = GetSpawnPoints(side);
            bool alongX = side == SpawnSide.Top || side == SpawnSide.Bottom;
            float outwardSign = side == SpawnSide.Top || side == SpawnSide.Right ? 1f : -1f;

            float anchorAlong = AverageAlong(points, alongX);
            float anchorOffAxis = alongX ? points[0].position.y : points[0].position.x;
            Vector3 anchor = alongX
                ? new Vector3(anchorAlong, anchorOffAxis, points[0].position.z)
                : new Vector3(anchorOffAxis, anchorAlong, points[0].position.z);

            _lastFormationAnchor = anchor;
            _lastFormationAlongX = alongX;
            _lastFormationOutwardSign = outwardSign;
            _hasFormationPlacement = true;

            int pairCount = Mathf.Max(entry.TotalUnitCount / 2, 0);
            _formationLeaderPositions = new Vector3[pairCount];
            _formationFollowerPositions = new Vector3[pairCount];

            for (int k = 0; k < pairCount; k++)
            {
                int ring = (k + 1) / 2;
                int ringSign = k % 2 == 1 ? 1 : -1;
                float along = anchorAlong + ring * ringSign * _tacticUnitSpacing;

                if (alongX)
                {
                    _formationLeaderPositions[k] = new Vector3(along, anchor.y, anchor.z);
                    _formationFollowerPositions[k] = new Vector3(along, anchor.y + outwardSign * _tacticRowSpacing, anchor.z);
                }
                else
                {
                    _formationLeaderPositions[k] = new Vector3(anchor.x, along, anchor.z);
                    _formationFollowerPositions[k] = new Vector3(anchor.x + outwardSign * _tacticRowSpacing, along, anchor.z);
                }
            }

            return pairCount;
        }

        /// <summary>
        /// 스폰 지점 배열의 along축(가로 또는 세로) 좌표 평균 - 대형 줄의 중심으로 쓴다.
        /// </summary>
        private static float AverageAlong(Transform[] points, bool alongX)
        {
            float sum = 0f;

            foreach (Transform point in points)
            {
                sum += alongX ? point.position.x : point.position.y;
            }

            return sum / points.Length;
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
            Vector3? explicitPosition = ResolveExplicitPosition(entry.Placement);
            SpawnInstance(entry.MonsterPrefab, entry.VisualSet, explicitPosition);
        }

        /// <summary>
        /// Automatic이면 null(=NextSpawnPoint()의 기존 4방향 자동 선택을 그대로 씀)을 반환한다.
        /// </summary>
        private Vector3? ResolveExplicitPosition(MonsterSpawnPlacement placement)
        {
            switch (placement)
            {
                case MonsterSpawnPlacement.LeftOrRight:
                    return NextLeftOrRightSpawnPoint().position;
                case MonsterSpawnPlacement.BehindTacticFormation:
                    return ComputeBehindFormationPosition();
                default:
                    return null;
            }
        }

        /// <summary>
        /// 좌/우 스폰 지점 중 무작위로 하나를 골라 그 방향의 커서를 순환한다(상/하 제외).
        /// </summary>
        private Transform NextLeftOrRightSpawnPoint()
        {
            bool useLeft = UnityEngine.Random.value < 0.5f;

            return useLeft
                ? _leftSpawnPoints[_leftCursor++ % _leftSpawnPoints.Length]
                : _rightSpawnPoints[_rightCursor++ % _rightSpawnPoints.Length];
        }

        /// <summary>
        /// 가장 최근에 배치된 전술 대형의 앵커에서, 그 대형이 물러난 방향(outwardSign)으로
        /// BehindFormationDistanceMultiplier배만큼 더 물러난 좌표. 아직 어떤 대형도 배치되지
        /// 않았으면(그 스테이지에 전술 웨이브가 없음) NextSpawnPoint()로 방어적 대체한다.
        /// </summary>
        private Vector3 ComputeBehindFormationPosition()
        {
            if (!_hasFormationPlacement)
            {
                return NextSpawnPoint().position;
            }

            Vector3 outwardAxis = _lastFormationAlongX ? Vector3.up : Vector3.right;
            return _lastFormationAnchor + outwardAxis * (_lastFormationOutwardSign * _tacticRowSpacing * BehindFormationDistanceMultiplier);
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
