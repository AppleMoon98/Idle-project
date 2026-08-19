using System;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 배치 가능한 스폰 슬롯 하나. 어떤 프리팹을 스폰할지는 더 이상 여기서 고정하지 않고,
    /// SoldierDeploymentService.TryGetAssigned(SlotIndex)로 조회한 로스터 유닛의 Definition.Prefab을
    /// 스폰 시점에 사용한다(로스터 편성에 따라 이 슬롯에 나가는 병사 종류가 바뀔 수 있으므로).
    /// 스폰 위치는 이 슬롯 하나만으로 정할 수 없다(Soldier.SoldierGridPlacement가 현재 배치된 병사
    /// 전체를 AttackRange로 정렬해 그리드에 일괄 배치하므로) — 이 클래스는 계산된 좌표를 담아
    /// 돌려줄 스크래치 Transform(_anchor)만 지연 생성해 소유한다(Soldier.SoldierBehaviorController.
    /// _returnAnchor와 같은 "독립된 앵커, 매번 재배치" 관례).
    /// </summary>
    [Serializable]
    public sealed class SoldierSpawnSlot
    {
        [SerializeField]
        private int slotIndex;

        private Transform _anchor;

        /// <summary>
        /// SoldierDeploymentService에서 이 슬롯을 식별하는 번호.
        /// </summary>
        public int SlotIndex => slotIndex;

        /// <summary>
        /// position을 담아 돌려줄 앵커 Transform을 반환한다(지연 생성 후 재사용, 위치는 호출마다
        /// 갱신). 스폰 위치/회전을 제공하는 것은 물론, 배정된 유닛이 후퇴 모드일 때 돌아갈
        /// 지점으로도 쓰인다.
        /// </summary>
        public Transform ResolvePositionAnchor(Vector3 position)
        {
            if (_anchor == null)
            {
                _anchor = new GameObject($"SoldierSpawnAnchor_{slotIndex}").transform;
            }

            _anchor.SetPositionAndRotation(position, Quaternion.identity);
            return _anchor;
        }
    }
}
