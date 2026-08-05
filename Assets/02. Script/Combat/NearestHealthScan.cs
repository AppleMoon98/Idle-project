using System;
using Character;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// "Physics2D.OverlapCircleAll로 범위 내 콜라이더를 모으고, 죽었거나 Health가 없는 후보는
    /// 건너뛴다"라는 보일러플레이트를 한곳에 모은 헬퍼. Attacker/EnemyTracker/MonsterTargetSelector가
    /// 각자 이 스캔 뒤에 서로 다른 방식으로 "가장 가까운 것"을 집계하던 부분(단순 최근접,
    /// 선호 후보 우선 등)은 그대로 호출자에 남겨둔다.
    /// </summary>
    public static class NearestHealthScan
    {
        /// <summary>
        /// origin 주변 range 안, layerMask에 해당하는 콜라이더 중 살아있는 Health만 걸러
        /// visit에 하나씩 전달한다.
        /// </summary>
        public static void ForEachAliveCandidate(Vector3 origin, float range, LayerMask layerMask, Action<Collider2D, Health> visit)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, range, layerMask);

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                visit(candidate, health);
            }
        }

        /// <summary>
        /// 위 스캔 결과 중 origin에서 가장 가까운 살아있는 Health 하나만 필요한 경우의 편의 메서드.
        /// </summary>
        public static Health FindNearest(Vector3 origin, float range, LayerMask layerMask)
        {
            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            ForEachAliveCandidate(origin, range, layerMask, (candidate, health) =>
            {
                float sqrDistance = (candidate.transform.position - origin).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            });

            return nearest;
        }
    }
}
