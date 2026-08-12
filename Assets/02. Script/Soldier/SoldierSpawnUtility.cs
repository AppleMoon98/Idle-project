using Character;
using Managers;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// SoldierSpawner.SpawnSlot과 SoldierRespawner.Respawn이 공유하는 "슬롯의 현재 배정을 조회해
    /// 프리팹을 풀에서 꺼내고 SoldierBehaviorController를 초기화하는" 절차를 한곳에 모은다.
    /// </summary>
    public static class SoldierSpawnUtility
    {
        /// <summary>
        /// slot에 배정된 유닛을 pool에서 스폰하고 SoldierBehaviorController/SoldierGradeScaler를
        /// 초기화한다. playerStats는 SoldierGradeScaler가 "플레이어 스탯 대비 등급 지분"을
        /// 계산하는 데 쓰인다(null이면 그레이드 컴포넌트가 아무 보정도 하지 않고 조용히 넘어감).
        /// 배정이 없거나(로스터 미배치/해제) 프리팹이 지정되지 않았으면 스폰하지 않고 false를 반환한다.
        /// </summary>
        public static bool TrySpawnAssigned(PoolManager pool, SoldierDeploymentService deployment, SoldierSpawnSlot slot, CharacterStatsProvider playerStats, out GameObject instance)
        {
            instance = null;

            if (deployment == null || !deployment.TryGetAssigned(slot.SlotIndex, out OwnedSoldier owned) || owned.Definition.Prefab == null)
            {
                return false;
            }

            pool.EnsurePool(owned.Definition.Prefab, 1, 1);
            instance = pool.Get(owned.Definition.Prefab, slot.SpawnPoint.position, slot.SpawnPoint.rotation);

            if (instance.TryGetComponent(out SoldierBehaviorController controller))
            {
                controller.Initialize(owned.InstanceId, slot.SpawnPoint);
            }

            if (instance.TryGetComponent(out SoldierGradeScaler gradeScaler))
            {
                gradeScaler.Initialize(playerStats, owned.Definition.Grade);
            }

            return true;
        }
    }
}
