using Behavior;

namespace Soldier
{
    /// <summary>
    /// 플레이어가 보유 중인 병사 개별 유닛. OwnedEquipment(스택형 라인)와 달리, 같은 SoldierSO를
    /// 여러 번 뽑아도 각각 별개의 유닛으로 관리된다(전용 장비를 개별로 장착할 수 있어야 하므로).
    /// InstanceId는 SoldierRosterService가 발급하는 고유 번호로, 같은 Definition을 가진
    /// 유닛끼리 구분하는 유일한 수단이다.
    /// </summary>
    public sealed class OwnedSoldier
    {
        /// <summary>
        /// 이 유닛이 어떤 병사 원형인지.
        /// </summary>
        public SoldierSO Definition { get; }

        /// <summary>
        /// 로스터 내에서 이 유닛을 유일하게 식별하는 번호.
        /// </summary>
        public int InstanceId { get; }

        /// <summary>
        /// 이 유닛에 배정된 행동 프로필. 배정하지 않았으면(null) SoldierBehaviorController가
        /// 항상 Engage(교전)로 취급한다.
        /// </summary>
        public BehaviorProfileSO BehaviorProfile { get; internal set; }

        public OwnedSoldier(SoldierSO definition, int instanceId)
        {
            Definition = definition;
            InstanceId = instanceId;
        }
    }
}
