using System.Collections.Generic;
using Character;
using Character.Events;
using Core;
using Enhancement;
using SoldierEnhancement;
using SoldierEnhancement.Events;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 같은 부대(슬롯 인덱스 / SoldierDeploymentService.SlotsPerSquad)에 배치된 병사들이 교전
    /// 전까지 부대 내 최저속 유닛 기준으로 함께 이동하도록 RuntimeStats.MoveSpeed를 조정한다.
    /// 기마병(Combat.CavalryCharge 보유 — 등록 시 IsExempt로 표시)은 부대에
    /// 배치돼 있어도 항상 각개 행동이라 클램프 대상/기준 계산 양쪽에서 완전히 제외된다.
    /// SoldierBehaviorController.Evaluate가 매 결정 주기마다 SetMarching으로 "지금 이 유닛이
    /// 부대와 함께 행군 중인지(Engage 모드 + 아직 교전 상대를 못 찾음)"를 알려준다. 부대 안
    /// 비면제(기마병/기마궁수 제외) 유닛 중 단 하나라도 교전에 들어가면(SetMarching false) 그
    /// 순간 부대 전체가 함께 전투태세로 전환된다 — "한 명이라도 교전하면 전원 교전" 요청에 따라,
    /// 각 유닛의 로컬 판정을 그대로 쓰지 않고 부대 전체의 AND 집계(GetEffectiveMarching)를
    /// 별도로 유지해 이동속도 클램프와 SoldierBehaviorController.ApplyMode의 대형 이탈 판단
    /// 양쪽에 공통으로 반영한다. 기마병/기마궁수는 원래부터 각개행동이라(IsExempt) 이 집계에서
    /// 완전히 제외된다 — 다른 유닛의 교전에 끌려들지도, 자신의 교전이 부대에 전파되지도 않는다.
    ///
    /// "본연 속도"는 캐싱하지 않고 매번 CharacterStatsProvider.BaseStats.MoveSpeed + 병사 전역
    /// 이동속도 강화 레벨로 새로 계산한다(SoldierStatReceiver가 RuntimeStats.MoveSpeed에 적용하는
    /// 것과 동일한 공식, Character.RuntimeStatApplier 참고). RuntimeStats.MoveSpeed 자체는 이
    /// 서비스가 계속 덮어쓰므로, 그 값을 입력으로 삼으면 자기 자신이 이전에 써둔 클램프 값을
    /// "본연 속도"로 착각하게 된다 — 항상 원본 데이터에서 다시 계산해 마지막에 덮어쓰므로,
    /// SoldierStatReceiver.OnSoldierStatEnhanced(같은 이벤트를 구독)와의 처리 순서가 어느 쪽이든
    /// 최종적으로 항상 올바른 값으로 수렴한다.
    /// </summary>
    public sealed class SquadMovementSyncService : IManager, IService
    {
        private sealed class Member
        {
            public GameObject Instance;
            public CharacterStatsProvider StatsProvider;
            public int SlotIndex;
            public bool IsExempt;
            public bool IsMarching = true;
        }

        private readonly EventBus _events;
        private readonly Dictionary<GameObject, Member> _members = new();
        private readonly Dictionary<int, List<Member>> _squadMembers = new();
        private readonly Dictionary<int, bool> _squadEffectiveMarching = new();

        public SquadMovementSyncService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
            _events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            _events?.Subscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
        }

        public void Shutdown()
        {
            _events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            _events?.Unsubscribe<SoldierStatEnhancedEvent>(OnSoldierStatEnhanced);
            _members.Clear();
            _squadMembers.Clear();
            _squadEffectiveMarching.Clear();
        }

        /// <summary>
        /// 스폰(최초 배치/재소환)된 병사를 등록한다. SoldierSpawnUtility.TrySpawnAssigned가 등급
        /// 스케일까지 끝낸 직후 호출한다. isExempt는 기마병 여부(호출부가 CavalryCharge
        /// 컴포넌트 존재로 판단).
        /// </summary>
        public void Register(GameObject instance, int slotIndex, bool isExempt)
        {
            if (instance == null || !instance.TryGetComponent(out CharacterStatsProvider statsProvider))
            {
                return;
            }

            var member = new Member
            {
                Instance = instance,
                StatsProvider = statsProvider,
                SlotIndex = slotIndex,
                IsExempt = isExempt,
                IsMarching = true,
            };

            _members[instance] = member;
            GetOrCreateSquadList(SquadIndexOf(slotIndex)).Add(member);

            RecomputeSquad(SquadIndexOf(slotIndex));
        }

        /// <summary>
        /// squadIndex에 현재 배치·생존 중인 병사 인스턴스 목록. 다른 부대 단위 기능(예: 부대
        /// 전술 조율자)이 "이 부대에 지금 누가 있는지"를 다시 추적하지 않고 이 서비스가 이미
        /// 관리하는 등록/해제 상태를 그대로 재사용할 수 있게 공개한다.
        /// </summary>
        public IReadOnlyList<GameObject> GetSquadMembers(int squadIndex)
        {
            if (!_squadMembers.TryGetValue(squadIndex, out List<Member> members))
            {
                return System.Array.Empty<GameObject>();
            }

            var result = new List<GameObject>(members.Count);

            foreach (Member member in members)
            {
                result.Add(member.Instance);
            }

            return result;
        }

        /// <summary>
        /// instance가 현재 등록된 인스턴스라면 그 슬롯 인덱스를 반환한다(예: 좌우 습격 전술이
        /// 부대원을 슬롯 상대 번호 홀/짝으로 나눌 때 사용). 등록돼 있지 않으면 false.
        /// </summary>
        public bool TryGetSlotIndex(GameObject instance, out int slotIndex)
        {
            if (instance != null && _members.TryGetValue(instance, out Member member))
            {
                slotIndex = member.SlotIndex;
                return true;
            }

            slotIndex = -1;
            return false;
        }

        /// <summary>
        /// SoldierBehaviorController가 매 결정 주기마다 호출한다 — Engage 모드이면서 아직 교전
        /// 상대를 찾지 못한 상태만 true(부대 클램프 대상), 그 외(Hold/Retreat, 또는 이미 교전 시작)는
        /// false(자기 본연 속도로 즉시 복귀).
        /// </summary>
        public void SetMarching(GameObject instance, bool isMarching)
        {
            if (!_members.TryGetValue(instance, out Member member) || member.IsMarching == isMarching)
            {
                return;
            }

            member.IsMarching = isMarching;
            RecomputeSquad(SquadIndexOf(member.SlotIndex));
        }

        /// <summary>
        /// instance가 실제로 "행군 중"으로 취급돼야 하는지 — 이 유닛 자신의 로컬 판정이 아니라
        /// 소속 부대의 AND 집계다(부대원 중 하나라도 교전 중이면 전원 false). 예외(기마병/기마궁수)는
        /// 부대 집계와 무관하게 항상 자기 로컬 판정을 그대로 반환한다. SoldierBehaviorController.
        /// ApplyMode가 이 값으로 궁병의 대형 이탈 여부를 판단한다 — SetMarching으로 보고한 로컬
        /// isMarching을 그대로 쓰면 "한 명이라도 교전하면 전원 교전" 전파가 되지 않는다.
        /// </summary>
        public bool GetEffectiveMarching(GameObject instance)
        {
            if (instance == null || !_members.TryGetValue(instance, out Member member))
            {
                return true;
            }

            if (member.IsExempt)
            {
                return member.IsMarching;
            }

            return !_squadEffectiveMarching.TryGetValue(SquadIndexOf(member.SlotIndex), out bool marching) || marching;
        }

        /// <summary>
        /// instance가 등록된 부대의 이동속도 클램프를 강제로 다시 계산한다. 던전 오버레이가 병사
        /// 전체를 SetActive(false)→(true)로 순환시키는 경로(Soldier.SoldierRespawner.SetActiveAll)는
        /// Register/SetMarching 어느 쪽도 거치지 않는데, 재활성화 시 OnEnable이
        /// Soldier.SoldierStatReceiver를 통해 RuntimeStats.MoveSpeed를 본연 속도로 되돌려버려
        /// 클램프가 저절로 재적용되지 않는다 — 그 경로가 끝난 뒤 이 메서드로 명시적으로 되돌린다.
        /// 등록되지 않은 인스턴스는 조용히 무시한다.
        /// </summary>
        public void Resync(GameObject instance)
        {
            if (instance == null || !_members.TryGetValue(instance, out Member member))
            {
                return;
            }

            RecomputeSquad(SquadIndexOf(member.SlotIndex));
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character == null || !_members.TryGetValue(evt.Character, out Member member))
            {
                return;
            }

            _members.Remove(evt.Character);

            int squadIndex = SquadIndexOf(member.SlotIndex);

            if (_squadMembers.TryGetValue(squadIndex, out List<Member> list))
            {
                list.Remove(member);
            }

            RecomputeSquad(squadIndex);
        }

        private void OnSoldierStatEnhanced(SoldierStatEnhancedEvent evt)
        {
            if (evt.StatType != EnhancementStatType.MoveSpeed)
            {
                return;
            }

            // 병사 이동속도 강화는 배치된 모든 병사에게 전역 적용되므로, 모든 부대의 본연 속도가
            // 한꺼번에 바뀐다 — 부대별로 다시 계산해야 한다.
            foreach (int squadIndex in _squadMembers.Keys)
            {
                RecomputeSquad(squadIndex);
            }
        }

        private void RecomputeSquad(int squadIndex)
        {
            if (!_squadMembers.TryGetValue(squadIndex, out List<Member> members) || members.Count == 0)
            {
                return;
            }

            // 1패스: 비면제 유닛 중 하나라도 IsMarching=false면 부대 전체가 전투태세(squadMarching=false),
            // 동시에 클램프 후보 최저속(squadMinSpeed)도 함께 구한다 — squadMarching이 최종적으로
            // false로 판명나면 이 값은 그냥 버려진다(아래서 hasClampTarget이 걸러냄).
            bool squadMarching = true;
            float squadMinSpeed = float.MaxValue;

            foreach (Member member in members)
            {
                if (member.IsExempt)
                {
                    continue;
                }

                if (!member.IsMarching)
                {
                    squadMarching = false;
                }

                float natural = NaturalMoveSpeed(member);

                if (natural < squadMinSpeed)
                {
                    squadMinSpeed = natural;
                }
            }

            _squadEffectiveMarching[squadIndex] = squadMarching;

            bool hasClampTarget = squadMarching && squadMinSpeed < float.MaxValue;

            // 2패스: 클램프 대상(비면제 + 부대 전체 행군 중)만 squadMinSpeed로, 나머지는 각자 본연 속도로.
            foreach (Member member in members)
            {
                member.StatsProvider.Stats.MoveSpeed = hasClampTarget && !member.IsExempt
                    ? squadMinSpeed
                    : NaturalMoveSpeed(member);
            }
        }

        private static float NaturalMoveSpeed(Member member)
        {
            float baseSpeed = member.StatsProvider.BaseStats.MoveSpeed;

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out SoldierEnhancementService enhancementService))
            {
                return baseSpeed;
            }

            float valuePerLevel = enhancementService.GetValuePerLevel(EnhancementStatType.MoveSpeed);
            int level = enhancementService.GetLevel(EnhancementStatType.MoveSpeed);

            return baseSpeed * (1f + valuePerLevel * level);
        }

        private static int SquadIndexOf(int slotIndex)
        {
            return slotIndex / SoldierDeploymentService.SlotsPerSquad;
        }

        private List<Member> GetOrCreateSquadList(int squadIndex)
        {
            if (!_squadMembers.TryGetValue(squadIndex, out List<Member> list))
            {
                list = new List<Member>();
                _squadMembers[squadIndex] = list;
            }

            return list;
        }
    }
}
