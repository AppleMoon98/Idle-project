using System.Collections.Generic;
using Character.Events;
using Combat;
using Core;
using Soldier.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// SquadTacticType.ShieldWall이 배정된 부대에서, 현재 배치·생존 중인 창병/궁병(Combat.
    /// FormationFollower 보유)마다 부대 편성 배치 그리드상 자기 슬롯의 "앞줄" 슬롯
    /// (SoldierDeploymentService.GetProtectorSlotIndex)에 방패보병(Character.ShieldGuard 보유)이
    /// 있으면 그 방패보병을 리더로 짝짓는다 - 몬스터 쪽 Stage.Tactics.ShieldWallFormationGroup이
    /// 등록 순서로 아무 방패병이나 나란히 짝짓는 것과 달리, 플레이어 부대는 배치 UI에서 실제로
    /// "누구를 누구 뒤에 세웠는지"가 그대로 반영돼야 하므로 슬롯 위치 기준으로 짝짓는다. 배치 UI로
    /// 언제든 구성이 바뀔 수 있어 고정 그룹을 만들지 않고, 관련 이벤트가 올 때마다 그 시점의
    /// SquadMovementSyncService.GetSquadMembers로 다시 조회해 대상 부대를 완전히 새로 계산한다
    /// (부분 diff 없음 — 이 프로젝트 전반의 "매번 깨끗하게 재계산" 관례, StageMonsterScaler.
    /// ApplyScale/ShieldWallFormationGroup.Rebalance와 동일한 방향). 앞줄에 방패병이 없거나(빈
    /// 슬롯/다른 병종) 자기가 이미 부대 내 마지막 행(가장 앞줄)이면 짝지어지지 않고 평범하게
    /// (리더 없이 혼자 카이팅) 움직인다.
    /// </summary>
    public sealed class SquadShieldWallCoordinator : IManager, IService
    {
        private readonly EventBus _events;
        private readonly SquadTacticService _tactics;
        private readonly SquadMovementSyncService _movementSync;

        public SquadShieldWallCoordinator(EventBus events, SquadTacticService tactics, SquadMovementSyncService movementSync)
        {
            _events = events;
            _tactics = tactics;
            _movementSync = movementSync;
        }

        public void Initialize()
        {
            _events?.Subscribe<SquadTacticChangedEvent>(OnTacticChanged);
            _events?.Subscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            _events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        public void Shutdown()
        {
            _events?.Unsubscribe<SquadTacticChangedEvent>(OnTacticChanged);
            _events?.Unsubscribe<SoldierDeploymentChangedEvent>(OnDeploymentChanged);
            _events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void OnTacticChanged(SquadTacticChangedEvent evt)
        {
            if (evt.Tactic == SquadTacticType.ShieldWall)
            {
                RecomputeSquad(evt.SquadIndex);
            }
            else
            {
                UnpairAll(evt.SquadIndex);
            }
        }

        private void OnDeploymentChanged(SoldierDeploymentChangedEvent evt)
        {
            RecomputeAllShieldWallSquads();
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            RecomputeAllShieldWallSquads();
        }

        private void RecomputeAllShieldWallSquads()
        {
            for (int squadIndex = 0; squadIndex < SoldierDeploymentService.SquadCount; squadIndex++)
            {
                if (_tactics.GetTactic(squadIndex) == SquadTacticType.ShieldWall)
                {
                    RecomputeSquad(squadIndex);
                }
            }
        }

        private void RecomputeSquad(int squadIndex)
        {
            IReadOnlyList<GameObject> members = _movementSync.GetSquadMembers(squadIndex);

            // 슬롯 인덱스로 즉시 조회할 수 있도록 먼저 맵을 만든다.
            var bySlot = new Dictionary<int, GameObject>();

            foreach (GameObject member in members)
            {
                if (member != null && _movementSync.TryGetSlotIndex(member, out int slotIndex))
                {
                    bySlot[slotIndex] = member;
                }
            }

            foreach (GameObject member in members)
            {
                if (member == null || !member.TryGetComponent(out FormationFollower _))
                {
                    continue;
                }

                if (!_movementSync.TryGetSlotIndex(member, out int slotIndex))
                {
                    Unpair(member);
                    continue;
                }

                int? protectorSlot = SoldierDeploymentService.GetProtectorSlotIndex(slotIndex);

                if (protectorSlot.HasValue
                    && bySlot.TryGetValue(protectorSlot.Value, out GameObject protector)
                    && protector.GetComponent<Character.ShieldGuard>() != null)
                {
                    Pair(protector, member);
                }
                else
                {
                    Unpair(member);
                }
            }
        }

        private void UnpairAll(int squadIndex)
        {
            IReadOnlyList<GameObject> members = _movementSync.GetSquadMembers(squadIndex);

            foreach (GameObject member in members)
            {
                if (member != null && member.GetComponent<FormationFollower>() != null)
                {
                    Unpair(member);
                }
            }
        }

        /// <summary>
        /// 리더를 배정한다. 이 창병이 이전에 리더 없이 위협을 만나 RangedKiter로 이미 넘어간
        /// 상태(FormationFollower.HandOffToKiter, 되돌리지 않는 전환)였더라도, 새 리더가 생기면
        /// 강제로 되돌린다 — 몬스터 쪽 고정 스폰 그룹과 달리 배치 UI로 언제든 재편성될 수 있는
        /// 병사 부대는 이 "한 번 넘어가면 끝" 가정이 성립하지 않는다.
        /// </summary>
        private static void Pair(GameObject shieldBearer, GameObject spearman)
        {
            if (spearman.TryGetComponent(out RangedKiter kiter))
            {
                kiter.enabled = false;
            }

            if (spearman.TryGetComponent(out FormationFollower follower))
            {
                follower.enabled = true;
                follower.SetLeader(shieldBearer.transform);
            }
        }

        private static void Unpair(GameObject spearman)
        {
            if (spearman.TryGetComponent(out FormationFollower follower))
            {
                follower.SetLeader(null);
                follower.enabled = false;
            }

            if (spearman.TryGetComponent(out RangedKiter kiter))
            {
                kiter.enabled = true;
            }
        }
    }
}
