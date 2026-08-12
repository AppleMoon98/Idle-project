using Character;
using Core;
using Managers;
using Rank;
using Rank.Events;
using Soldier.Events;
using Stage.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 배치 슬롯마다 SoldierDeploymentService에 배정된 로스터 유닛을 스폰한다. requiredRank가
    /// 설정돼 있으면 현재 랭크가 그 이상이 될 때까지 스폰을 미루고, RankChangedEvent를 구독해
    /// 승급 즉시 스폰한다(null이면 조건 없이 항상 활성). 슬롯에 배정이 없으면(로스터가 비었거나
    /// 아직 아무도 배치하지 않았으면) 그 슬롯은 스폰하지 않는다. SoldierDeploymentChangedEvent도
    /// 구독해, 이미 활성화된 뒤에 플레이어가 슬롯을 재편성하면(새로 배정/교체/해제) 그 슬롯만
    /// 즉시 정리하고 다시 스폰한다 — 그렇지 않으면 씬 시작 시점 배정만 영원히 유효하게 된다.
    /// 전투 중 사망한 병사는 자동으로 리스폰되지 않는다 — 그 슬롯은 다음 스테이지가 시작될
    /// 때(OnStageChanged가 SoldierRespawner.ResetForNewStage를 호출)까지 빈 채로 남는다.
    /// </summary>
    public sealed class SoldierSpawner : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawnSlot[] slots;

        [SerializeField]
        private RankSO requiredRank;

        [SerializeField]
        private CharacterStatsProvider playerStats;

        private SoldierRespawner _respawner;
        private PoolManager _pool;
        private SoldierDeploymentService _deployment;
        private bool _spawned;

        private void Start()
        {
            if (!GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            _pool = pool;
            GameBootstrapper.Services.TryGet(out _deployment);
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            GameBootstrapper.Events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);

            TrySpawn();
        }

        private void OnDestroy()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
            GameBootstrapper.Events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);

            if (_respawner == null)
            {
                return;
            }

            _respawner.Dispose();
            _respawner = null;
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            TrySpawn();
        }

        /// <summary>
        /// 스테이지가 바뀔 때마다(진행/반복/사망 후퇴 전부) 살아있는 병사 전원의 체력을
        /// 최대치로 되돌리고, 이번 시도 중 사망해 빈 채로 남아있던 슬롯은 재배치한다.
        /// 전투 중 사망은 더 이상 자동으로 리스폰되지 않는다 — 스테이지 시작이 슬롯을
        /// 되돌리는 유일한 시점이다(Soldier.SoldierRespawner.ResetForNewStage 참고).
        /// </summary>
        private void OnStageChanged(StageChangedEvent evt)
        {
            _respawner?.ResetForNewStage(slots);
        }

        /// <summary>
        /// 현재 스폰된 병사 전부를 잠깐 비활성화(true로 복귀 전까지 전투/이동/리스폰 판정 정지)한다.
        /// 병사 동행이 금지된 콘텐츠(예: 던전 오버레이) 진입/종료 시 사용한다. 아직 한 번도
        /// 스폰되지 않았으면(예: 랭크 미달) 아무 일도 하지 않는다.
        /// </summary>
        public void SetSoldiersActive(bool active)
        {
            _respawner?.SetActiveAll(active);
        }

        /// <summary>
        /// 현재 스폰된 병사 전부를 각자의 슬롯 스폰 지점으로 되돌린다. 아직 한 번도 스폰되지
        /// 않았으면(예: 랭크 미달) 아무 일도 하지 않는다.
        /// </summary>
        public void ResetSoldierPositions()
        {
            _respawner?.ResetPositions();
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            if (!_spawned)
            {
                // 아직 랭크로 활성화되지 않았으면(예: 슬롯이 이미 잠긴 상태) 손댈 활성 스폰이 없다 —
                // TrySpawn이 나중에 랭크 승급 시 그 시점의 배정을 그대로 읽어 처리한다.
                return;
            }

            RefreshSlot(evt.SlotIndex);
        }

        private void TrySpawn()
        {
            if (_spawned)
            {
                return;
            }

            if (!GameBootstrapper.Services.TryGet(out RankService rankService) || !rankService.IsAtLeast(requiredRank))
            {
                return;
            }

            _spawned = true;

            _respawner = new SoldierRespawner(GameBootstrapper.Events, _pool, _deployment, playerStats);

            foreach (SoldierSpawnSlot slot in slots)
            {
                SpawnSlot(slot);
            }
        }

        /// <summary>
        /// slotIndex의 현재 점유자를 정리하고(있다면) 최신 배정으로 다시 스폰을 시도한다.
        /// </summary>
        private void RefreshSlot(int slotIndex)
        {
            SoldierSpawnSlot slot = FindSlot(slotIndex);

            if (slot == null)
            {
                return;
            }

            _respawner.ReleaseSlot(slotIndex);
            SpawnSlot(slot);
        }

        private SoldierSpawnSlot FindSlot(int slotIndex)
        {
            foreach (SoldierSpawnSlot slot in slots)
            {
                if (slot.SlotIndex == slotIndex)
                {
                    return slot;
                }
            }

            return null;
        }

        private void SpawnSlot(SoldierSpawnSlot slot)
        {
            if (!SoldierSpawnUtility.TrySpawnAssigned(_pool, _deployment, slot, playerStats, out GameObject instance))
            {
                return;
            }

            _respawner.RegisterSpawned(instance, slot);
        }
    }
}
