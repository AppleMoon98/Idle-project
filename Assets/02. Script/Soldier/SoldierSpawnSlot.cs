using System;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 배치 가능한 스폰 슬롯 하나. 어떤 프리팹을 스폰할지는 더 이상 여기서 고정하지 않고,
    /// SoldierDeploymentService.TryGetAssigned(SlotIndex)로 조회한 로스터 유닛의 Definition.Prefab을
    /// 스폰 시점에 사용한다(로스터 편성에 따라 이 슬롯에 나가는 병사 종류가 바뀔 수 있으므로).
    /// </summary>
    [Serializable]
    public sealed class SoldierSpawnSlot
    {
        [SerializeField]
        private int slotIndex;

        [SerializeField]
        private Transform spawnPoint;

        /// <summary>
        /// SoldierDeploymentService에서 이 슬롯을 식별하는 번호.
        /// </summary>
        public int SlotIndex => slotIndex;

        /// <summary>
        /// 스폰 위치/회전을 제공하는 지점. 배정된 유닛이 후퇴 모드일 때 돌아갈 지점으로도 쓰인다.
        /// </summary>
        public Transform SpawnPoint => spawnPoint;
    }
}
