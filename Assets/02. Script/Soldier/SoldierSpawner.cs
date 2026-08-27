using System.Collections.Generic;
using Character;
using Core;
using Managers;
using Rank;
using Rank.Events;
using Services;
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

        [SerializeField]
        private SquadRaidCoordinator raidCoordinator;

        private SoldierRespawner _respawner;
        private PoolManager _pool;
        private SoldierDeploymentService _deployment;
        private CameraFollowService _cameraFollowService;
        private bool _spawned;

        private void Start()
        {
            if (!GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            _pool = pool;
            GameBootstrapper.Services.TryGet(out _deployment);
            GameBootstrapper.Services.TryGet(out _cameraFollowService);
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
            raidCoordinator?.OnStageStarted();
        }

        /// <summary>
        /// 현재 스폰된 병사 전부를 잠깐 비활성화(true로 복귀 전까지 전투/이동/리스폰 판정 정지)한다.
        /// 병사 동행이 금지된 콘텐츠(예: 던전 오버레이) 진입/종료 시 사용한다. 아직 한 번도
        /// 스폰되지 않았으면(예: 랭크 미달) 아무 일도 하지 않는다.
        /// raidCoordinator.SetDungeonHidden도 함께 호출한다(GitHub 이슈 #41) — 그렇지 않으면
        /// 습격 전술 타이머가 던전 안에서도 계속 돌아 병사가 되살아나거나(진입), 아직 대기 중인
        /// 습격 부대까지 SetActiveAll이 강제로 드러낸다(퇴장). 던전 컨트롤러는 이 메서드 하나만
        /// 호출하므로 별도 배선 없이 자동으로 함께 적용된다.
        /// </summary>
        public void SetSoldiersActive(bool active)
        {
            raidCoordinator?.SetDungeonHidden(!active);
            _respawner?.SetActiveAll(active);
        }

        /// <summary>
        /// 현재 스폰된 병사 전부를 각자의 그리드 자리(Soldier.SoldierGridPlacement)로 되돌린다.
        /// 아직 한 번도 스폰되지 않았으면(예: 랭크 미달) 아무 일도 하지 않는다.
        /// </summary>
        public void ResetSoldierPositions()
        {
            _respawner?.ResetPositions(slots);
        }

        /// <summary>
        /// 살아있는 병사 전원의 체력을 최대치로 되돌리고, 이번 시도 중 사망해 빈 채로 남아있던
        /// 슬롯은 재배치한다. OnStageChanged가 호출하는 것과 정확히 같은 로직(Soldier.SoldierRespawner.
        /// ResetForNewStage) — 던전 오버레이 재도전/복귀처럼 StageChangedEvent가 발행되지 않는
        /// 지점에서 병사 상태만 "새로 시작"한 것처럼 되돌려야 할 때 호출한다. 아직 한 번도
        /// 스폰되지 않았으면(예: 랭크 미달) 아무 일도 하지 않는다.
        /// </summary>
        public void ResetSoldiersForRetry()
        {
            _respawner?.ResetForNewStage(slots);
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

            _respawner = new SoldierRespawner(GameBootstrapper.Events, _pool, _deployment, playerStats, _cameraFollowService, raidCoordinator);

            Dictionary<int, Vector3> placements = _respawner.ComputePlacements(slots);

            foreach (SoldierSpawnSlot slot in slots)
            {
                if (placements.TryGetValue(slot.SlotIndex, out Vector3 position))
                {
                    _respawner.TrySpawnSlot(slot, position);
                }
            }

            raidCoordinator?.OnStageStarted();
        }

        /// <summary>
        /// slotIndex의 현재 점유자를 정리하고(있다면) 최신 배정으로 다시 스폰을 시도한다. 새 배정이
        /// 사거리 정렬 순서를 바꿀 수 있으므로, 이 순간의 전체 슬롯 배정 기준으로 그리드 좌표를
        /// 다시 계산한다(이미 스폰돼 있는 다른 병사들의 위치는 건드리지 않는다 — 그 슬롯 하나만).
        /// </summary>
        private void RefreshSlot(int slotIndex)
        {
            SoldierSpawnSlot slot = FindSlot(slotIndex);

            if (slot == null)
            {
                return;
            }

            _respawner.ReleaseSlot(slotIndex);

            Dictionary<int, Vector3> placements = _respawner.ComputePlacements(slots);

            if (placements.TryGetValue(slotIndex, out Vector3 position))
            {
                _respawner.TrySpawnSlot(slot, position);
            }
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
    }
}
