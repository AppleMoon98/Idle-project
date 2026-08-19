using System.Collections.Generic;
using Character;
using Combat;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 현재 배치돼 있는 병사 전체(부대 구분 없이 하나의 공용 그리드)를 Combat.SpawnGridLayout의
    /// 화면 아래쪽 바깥 그리드에 배치한다 — AttackRange 오름차순으로 정렬해 행 우선으로 채운다
    /// (row 0=화면에 가장 가까움=사거리 짧은 유닛, row가 깊어질수록 화면 밖으로 더 멀어짐=사거리
    /// 긴 유닛). 몬스터(Stage.MonsterSpawner)와 정확히 같은 그리드 유틸리티를 공유하되 기준선(화면
    /// 아래쪽)과 정렬 기준(스폰 순서가 아니라 사거리)만 다르다.
    /// </summary>
    public static class SoldierGridPlacement
    {
        /// <summary>
        /// slots 중 deployment에 실제 배정이 있는 것만 골라 AttackRange 오름차순(동률이면
        /// SlotIndex 오름차순, 계산 결과의 결정성을 위함)으로 정렬한 뒤 그리드 좌표를 계산해
        /// SlotIndex → Vector3 맵으로 돌려준다. 프리팹에 CharacterStatsProvider/BaseStats가 없으면
        /// AttackRange 0(가장 앞줄)으로 취급한다.
        /// </summary>
        public static Dictionary<int, Vector3> ComputePlacements(SoldierSpawnSlot[] slots, SoldierDeploymentService deployment, Vector3 boundsCenter, Vector2 boundsHalfExtent)
        {
            var entries = new List<(int slotIndex, float range)>();

            foreach (SoldierSpawnSlot slot in slots)
            {
                if (deployment != null && deployment.TryGetAssigned(slot.SlotIndex, out OwnedSoldier owned) && owned.Definition.Prefab != null)
                {
                    entries.Add((slot.SlotIndex, ResolveAttackRange(owned.Definition.Prefab)));
                }
            }

            entries.Sort((a, b) =>
            {
                int cmp = a.range.CompareTo(b.range);
                return cmp != 0 ? cmp : a.slotIndex.CompareTo(b.slotIndex);
            });

            Vector3 origin = SpawnGridLayout.ComputeBottomOrigin(boundsCenter, boundsHalfExtent);
            var result = new Dictionary<int, Vector3>(entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                result[entries[i].slotIndex] = SpawnGridLayout.ComputePosition(i, origin, -1f);
            }

            return result;
        }

        private static float ResolveAttackRange(GameObject prefab)
        {
            if (prefab.TryGetComponent(out CharacterStatsProvider provider) && provider.BaseStats != null)
            {
                return provider.BaseStats.AttackRange;
            }

            return 0f;
        }
    }
}
