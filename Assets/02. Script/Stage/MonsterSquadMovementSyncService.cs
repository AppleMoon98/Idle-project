using System.Collections.Generic;
using Character;
using Core;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 현재 스테이지에 살아있는 몬스터 전체가, 교전(자기 CharacterMover.Target에 도달) 전까지는
    /// 그중 최저속 유닛 기준으로 함께 이동하도록 RuntimeStats.MoveSpeed를 조정한다.
    /// Soldier.SquadMovementSyncService(병사 부대 이동속도 동기화)와 정확히 같은 방향의 매커니즘을
    /// 몬스터 쪽에도 적용한 것 — 다만 몬스터는 병사처럼 영구적인 배치 슬롯/부대 구분이 없으므로,
    /// 현재 필드에 살아있는 몬스터 전체를 하나의 그룹으로 취급한다(스테이지가 바뀌면 이전 몬스터는
    /// 전부 죽거나 강제 반환되어 자연히 그룹에서 빠지고, 새 스테이지의 몬스터가 새 그룹을 이룬다).
    ///
    /// 등록/해제는 Combat.MonsterMarchingTracker가 자신의 OnEnable/OnDisable에서 스스로 수행한다 —
    /// Managers.PoolManager.Get/Release가 항상 GameObject.SetActive(true/false)를 거치므로(죽어서
    /// 반환되든, 스테이지 전환으로 강제 반환되든 동일하게 SetActive(false)를 거친다), 별도의 이벤트
    /// 구독이나 스포너 쪽 명시적 해제 호출 없이도 이 그룹은 항상 "현재 활성 상태인 몬스터"만
    /// 정확히 추적한다.
    ///
    /// 기마병/기마궁수(Combat.CavalryCharge/OrbitKiter — 목표에 다가가 멈춘다는 개념 자체가 없는
    /// 자체 상태 기계)는 MonsterMarchingTracker가 스스로 감지해 아예 등록하지 않으므로, 이 서비스는
    /// 그 존재를 몰라도 된다(Soldier 쪽의 IsExempt 플래그와 달리, 등록 자체를 안 하는 쪽이 더 단순).
    ///
    /// "본연 속도"는 Character.CharacterStatsProvider.BaseStats.MoveSpeed 그대로다 — 몬스터는
    /// Character.StageMonsterScaler가 MaxHealth/AttackPower만 재계산할 뿐 MoveSpeed는 건드리지
    /// 않고, 병사처럼 이동속도 강화 시스템도 없어 Soldier.SquadMovementSyncService.NaturalMoveSpeed와
    /// 달리 추가 계산이 필요 없다.
    /// </summary>
    public sealed class MonsterSquadMovementSyncService : IManager, IService
    {
        private sealed class Member
        {
            public CharacterStatsProvider StatsProvider;
            public bool IsMarching = true;
        }

        private readonly Dictionary<GameObject, Member> _members = new();

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _members.Clear();
        }

        /// <summary>
        /// Combat.MonsterMarchingTracker가 자신의 OnEnable에서 호출한다.
        /// </summary>
        public void Register(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent(out CharacterStatsProvider statsProvider))
            {
                return;
            }

            _members[instance] = new Member { StatsProvider = statsProvider, IsMarching = true };
            Recompute();
        }

        /// <summary>
        /// Combat.MonsterMarchingTracker가 자신의 OnDisable에서 호출한다(사망/강제 반환 구분 없음).
        /// </summary>
        public void Unregister(GameObject instance)
        {
            if (instance != null && _members.Remove(instance))
            {
                Recompute();
            }
        }

        /// <summary>
        /// Combat.MonsterMarchingTracker가 폴링 주기마다 호출한다 — 아직 자기 목표(CharacterMover.Target)
        /// 사거리 안에 들어오지 못했으면 true(그룹 클램프 대상), 도달했으면 false(자기 본연 속도로
        /// 즉시 복귀 + 그룹 전체가 함께 전투태세로 전환).
        /// </summary>
        public void SetMarching(GameObject instance, bool isMarching)
        {
            if (!_members.TryGetValue(instance, out Member member) || member.IsMarching == isMarching)
            {
                return;
            }

            member.IsMarching = isMarching;
            Recompute();
        }

        private void Recompute()
        {
            if (_members.Count == 0)
            {
                return;
            }

            bool groupMarching = true;

            foreach (Member member in _members.Values)
            {
                if (!member.IsMarching)
                {
                    groupMarching = false;
                    break;
                }
            }

            float groupMinSpeed = float.MaxValue;

            if (groupMarching)
            {
                foreach (Member member in _members.Values)
                {
                    float natural = member.StatsProvider.BaseStats.MoveSpeed;

                    if (natural < groupMinSpeed)
                    {
                        groupMinSpeed = natural;
                    }
                }
            }

            bool hasClampTarget = groupMarching && groupMinSpeed < float.MaxValue;

            foreach (Member member in _members.Values)
            {
                float natural = member.StatsProvider.BaseStats.MoveSpeed;
                member.StatsProvider.Stats.MoveSpeed = hasClampTarget ? groupMinSpeed : natural;
            }
        }
    }
}
