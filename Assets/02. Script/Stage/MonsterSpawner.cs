using System;
using System.Collections.Generic;
using Character;
using Combat;
using Core;
using Managers;
using Services;
using Stage.Tactics;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// StageSO에 정의된 스폰 웨이브를 순서대로 생성한다. 모든 몬스터는 항상 화면(최대 축소 기준
    /// 고정 범위) 위쪽 바깥의 13열 그리드(Combat.SpawnGridLayout)에서 등장한다 — 스포너 하나(=
    /// 스테이지 하나)의 수명 동안 계속 증가하는 단일 그리드 커서(_gridCursor)를 모든 스폰(전술
    /// 리더/추종자, 일반 웨이브, spawnWithTactics 즉시 웨이브 가리지 않고) 공통으로 소비한다 —
    /// 그래서 전술 대형이 먼저 앞쪽(얕은 행)을 채우고, 뒤이은 spawnWithTactics 웨이브(기마병/보스
    /// 등 지원군)는 자연히 더 깊은 행(화면 밖으로 더 먼 곳)에 등장한다(예전 BehindTacticFormation
    /// 배치가 하던 일과 같은 결과를 별도 좌표 계산 없이 얻는다). 병사(Soldier 도메인)의 스폰
    /// 그리드와 정확히 같은 유틸리티(Combat.SpawnGridLayout)를 공유하되 기준선(화면 위쪽 vs
    /// 아래쪽)과 행 진행 방향만 반대다. 전술 웨이브(TacticSpawnEntry)를 먼저 처리하고 - 스테이지에
    /// 입장하자마자 대형부터 구성되도록 - 그게 모두 끝나면 이어서 일반 웨이브(MonsterSpawnEntry)를
    /// 처리한다. 전술 대형은 쌍(리더+추종자) 단위로 <see cref="TacticSpawnEntry.PairSpawnInterval"/>
    /// 만큼 시간차를 두고 스폰된다 - 이 값이 0(기본값)이면 한 틱에 전원이 한꺼번에 스폰된다.
    /// 일반 웨이브(TickEntries/SpawnImmediateEntries)는 각자의 배치 안에서 AttackRange 오름차순으로
    /// 스폰 순서를 다시 정렬한다 — 사거리가 짧을수록 앞쪽(얕은 행), 길수록 뒤쪽(깊은 행)에
    /// 배치되도록(Soldier.SoldierGridPlacement와 같은 방향).
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
        private readonly Transform _playerTarget;
        private readonly StageProgressTracker _tracker;
        private readonly CameraFollowService _cameraFollowService;
        private readonly float _statMultiplier;
        private readonly List<GameObject> _pendingLeaders = new();
        private readonly List<GameObject> _pendingFollowers = new();
        private readonly List<ITacticFormationGroup> _formationGroups = new();

        private int _entryIndex;
        private int _tacticEntryIndex;
        private bool _tacticEntryStarted;
        private int _tacticPairIndex;
        private float _tacticPairElapsed;
        private bool _waitingForImmediateEntries;
        private float _immediateEntryDelay;
        private float _immediateEntryElapsed;
        private int _gridCursor;

        /// <summary>
        /// 모든 웨이브(일반 + 전술)의 스폰이 끝났는지 여부. 전술 대형이 아직 전멸하지 않았다면
        /// (뒤따를 일반 웨이브가 아직 대기 중이므로) 끝난 것으로 보지 않는다.
        /// </summary>
        public bool IsFinished => _entryIndex >= _entries.Length && _tacticEntryIndex >= _tacticEntries.Length && AllFormationGroupsCleared();

        public MonsterSpawner(
            StageSO stage,
            PoolManager pool,
            Transform playerTarget,
            StageProgressTracker tracker,
            CameraFollowService cameraFollowService,
            float statMultiplier)
        {
            _entries = stage.SpawnEntries ?? Array.Empty<MonsterSpawnEntry>();
            _tacticEntries = stage.TacticEntries ?? Array.Empty<TacticSpawnEntry>();
            _pool = pool;
            _playerTarget = playerTarget;
            _tracker = tracker;
            _cameraFollowService = cameraFollowService;
            _statMultiplier = statMultiplier;
        }

        public void Tick(float deltaTime)
        {
            if (_tacticEntryIndex < _tacticEntries.Length)
            {
                TickTactics(deltaTime);
                return;
            }

            if (_waitingForImmediateEntries)
            {
                TickImmediateEntryDelay(deltaTime);
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

        /// <summary>
        /// 마지막 전술 엔트리가 끝난 뒤 spawnWithTactics 웨이브를 곧바로 스폰하지 않고
        /// TacticSpawnEntry.ImmediateEntryDelay만큼 기다린다(0이면 다음 틱에 바로 스폰되어
        /// 기존과 사실상 동일). 대형이 갖춰진 뒤 별도의 지연 시간을 두고 지원군(기마병/보스 등)이
        /// 합류하는 연출을 위한 것.
        /// </summary>
        private void TickImmediateEntryDelay(float deltaTime)
        {
            _immediateEntryElapsed += deltaTime;

            if (_immediateEntryElapsed < _immediateEntryDelay)
            {
                return;
            }

            _waitingForImmediateEntries = false;
            SpawnImmediateEntries();
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

        /// <summary>
        /// 남은 일반 웨이브 전체(모든 엔트리의 Count 전부)를 시간차 없이 한 틱에 즉시 스폰한다.
        /// entry.SpawnInterval은 더 이상 이 경로에서 쓰이지 않는다(기존 스테이지 데이터 호환을
        /// 위해 필드 자체는 남겨둠). 이 배치 안에서는 AttackRange 오름차순으로 스폰 순서를 다시
        /// 정렬한다 — 사거리가 짧을수록 그리드 앞쪽(얕은 행, 화면에 더 가까움), 사거리가
        /// 길수록 뒤쪽(깊은 행)에 배치되도록(Soldier.SoldierGridPlacement와 같은 방향).
        /// </summary>
        private void TickEntries(float deltaTime)
        {
            List<MonsterSpawnEntry> sorted = BuildRangeSortedBatch(_entryIndex, _entries.Length);
            _entryIndex = _entries.Length;

            foreach (MonsterSpawnEntry entry in sorted)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    SpawnOne(entry);
                }
            }
        }

        /// <summary>
        /// 현재 전술 엔트리를 쌍(리더+추종자) 단위로 시간차를 두고 스폰한다.
        /// <see cref="TacticSpawnEntry.PairSpawnInterval"/>이 0(기본값)이면 첫 틱에 모든 쌍이
        /// 즉시 스폰되어 기존 동작과 동일하다. 마지막 전술 엔트리의 마지막 쌍까지 다 스폰되면
        /// 이어서 spawnWithTactics 일반 웨이브(기마병/기사 등)를 처리한다.
        /// </summary>
        private void TickTactics(float deltaTime)
        {
            TacticSpawnEntry entry = _tacticEntries[_tacticEntryIndex];

            if (!_tacticEntryStarted)
            {
                _tacticPairIndex = 0;
                _tacticPairElapsed = entry.PairSpawnInterval;
                _tacticEntryStarted = true;

                if (entry.TotalUnitCount <= 0)
                {
                    FinishTacticEntry(entry);
                    return;
                }
            }

            int totalPairs = Mathf.Max(entry.TotalUnitCount / 2, 0);
            _tacticPairElapsed += deltaTime;

            while (_tacticPairIndex < totalPairs && _tacticPairElapsed >= entry.PairSpawnInterval)
            {
                _tacticPairElapsed -= entry.PairSpawnInterval;
                SpawnFormationPair(entry);
                _tacticPairIndex++;
            }

            if (_tacticPairIndex >= totalPairs)
            {
                FinishTacticEntry(entry);
            }
        }

        /// <summary>
        /// 대형의 쌍(리더+추종자) 하나를 그리드의 다음 두 칸에 스폰한다 — 리더가 먼저(더 얕은
        /// 칸), 추종자가 바로 다음(더 깊거나 옆 칸)을 차지한다. 예전처럼 리더 줄/추종자 줄을
        /// 별도로 나란히 배치하지 않는다 — 실제 대형 대열은 스폰 이후 FormationFollower가 리더를
        /// 따라가며 만들어지므로(Stage.Tactics.ShieldWallFormationGroup), 스폰 좌표 자체가 정교한
        /// 두 줄일 필요는 없다.
        /// </summary>
        private void SpawnFormationPair(TacticSpawnEntry entry)
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
        /// 현재 전술 엔트리의 모든 쌍이 다 스폰된 뒤 대형 그룹을 확정하고, 다음 전술 엔트리로
        /// 넘어간다(더 없으면 entry.ImmediateEntryDelay만큼 기다렸다가 spawnWithTactics 일반
        /// 웨이브를 처리 - TickImmediateEntryDelay 참고).
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
                _immediateEntryDelay = entry.ImmediateEntryDelay;
                _immediateEntryElapsed = 0f;
                _waitingForImmediateEntries = true;
            }
        }

        /// <summary>
        /// spawnEntries 배열 앞쪽부터, spawnWithTactics가 켜진 항목을 만나는 동안 그 Count 전부를
        /// 시간차 없이 즉시 스폰하고 _entryIndex를 그만큼 건너뛴다. 이후 TickEntries는 남은(즉시
        /// 스폰이 아닌) 항목부터 기존처럼 처리한다. TickEntries와 동일하게 이 배치 안에서도
        /// AttackRange 오름차순으로 스폰 순서를 다시 정렬한다.
        /// </summary>
        private void SpawnImmediateEntries()
        {
            int batchEnd = _entryIndex;

            while (batchEnd < _entries.Length && _entries[batchEnd].SpawnWithTactics)
            {
                batchEnd++;
            }

            List<MonsterSpawnEntry> sorted = BuildRangeSortedBatch(_entryIndex, batchEnd);
            _entryIndex = batchEnd;

            foreach (MonsterSpawnEntry entry in sorted)
            {
                for (int i = 0; i < entry.Count; i++)
                {
                    SpawnOne(entry);
                }
            }
        }

        /// <summary>
        /// _entries[startIndex, endIndex)를 AttackRange 오름차순(동률이면 원래 배열 순서 유지 —
        /// 결과의 결정성을 위함)으로 정렬한 목록을 만든다. Soldier.SoldierGridPlacement의 정렬
        /// 기준과 동일한 이유·형태.
        /// </summary>
        private List<MonsterSpawnEntry> BuildRangeSortedBatch(int startIndex, int endIndex)
        {
            var batch = new List<(MonsterSpawnEntry entry, int order)>();

            for (int i = startIndex; i < endIndex; i++)
            {
                batch.Add((_entries[i], i));
            }

            batch.Sort((a, b) =>
            {
                int cmp = ResolveAttackRange(a.entry.MonsterPrefab).CompareTo(ResolveAttackRange(b.entry.MonsterPrefab));
                return cmp != 0 ? cmp : a.order.CompareTo(b.order);
            });

            var result = new List<MonsterSpawnEntry>(batch.Count);

            foreach ((MonsterSpawnEntry entry, int _) in batch)
            {
                result.Add(entry);
            }

            return result;
        }

        /// <summary>
        /// Soldier.SoldierGridPlacement.ResolveAttackRange와 동일한 형태 — CharacterStatsProvider/
        /// BaseStats가 없으면 사거리 0(그리드 맨 앞줄)으로 취급한다.
        /// </summary>
        private static float ResolveAttackRange(GameObject prefab)
        {
            if (prefab != null && prefab.TryGetComponent(out CharacterStatsProvider provider) && provider.BaseStats != null)
            {
                return provider.BaseStats.AttackRange;
            }

            return 0f;
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

        /// <summary>
        /// 다음 그리드 칸(화면 위쪽 바깥, Combat.SpawnGridLayout)에 prefab을 스폰한다. 커서는
        /// 이 스포너(=이번 스테이지 시도)의 수명 동안 스폰할 때마다 1씩 증가하며, 종류(전술 리더/
        /// 추종자/일반/즉시 웨이브)를 가리지 않는 단일 카운터다.
        /// </summary>
        private GameObject SpawnInstance(GameObject prefab, MonsterVisualSetSO visualSet)
        {
            Vector3 boundsCenter = _cameraFollowService != null ? _cameraFollowService.HomeLocalPosition : Vector3.zero;
            Vector2 boundsHalfExtent = _cameraFollowService != null ? _cameraFollowService.GetWorldBoundsHalfExtent() : Vector2.zero;
            Vector3 origin = SpawnGridLayout.ComputeTopOrigin(boundsCenter, boundsHalfExtent);
            Vector3 position = SpawnGridLayout.ComputePosition(_gridCursor, origin, 1f);
            _gridCursor++;

            GameObject instance = _pool.Get(prefab, position, Quaternion.identity);

            // 대형(전술) 편입 스폰이 켜뒀던 FormationFollower/꺼뒀던 RangedKiter 상태가 이후 이
            // 인스턴스가 전술 없는 일반 웨이브로 재사용될 때 그대로 남아있으면, 죽은(비활성화만
            // 됐을 뿐 파괴되지 않아 == null이 false인) 이전 리더를 계속 쫓으려 해 제자리에
            // 얼어붙는다(실사용 중 발견 — Monster_Archer가 창병 대체로 대형에 들어갔다가 리더가
            // 죽은 뒤 다른 스테이지의 일반 웨이브로 재사용된 경우). 매 스폰마다 먼저 "일반 상태"로
            // 되돌려두고, 대형 편입이 필요하면 호출부가 ConfigureAsFormationFollower로 바로 다시
            // 뒤집는다.
            if (instance.TryGetComponent(out FormationFollower staleFollower))
            {
                staleFollower.enabled = false;
                staleFollower.SetLeader(null);
            }

            if (instance.TryGetComponent(out RangedKiter staleKiter))
            {
                staleKiter.enabled = true;
            }

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

            _tracker.RegisterSpawned(instance, position);
            return instance;
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
