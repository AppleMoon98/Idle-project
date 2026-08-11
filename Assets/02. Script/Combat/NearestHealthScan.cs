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
        /// boundsCenter/boundsHalfExtent 사각형 범위 안, layerMask에 해당하는 콜라이더 중
        /// 살아있는 Health만 걸러 visit에 하나씩 전달한다. 거리 반경(range) 대신 고정 사각형
        /// 범위로 후보를 모으는 버전 — Combat.EnemyTracker처럼 "탐지 거리"라는 별도 스탯 없이
        /// "그 범위 안에 있으면 후보"만으로 판정해야 하는 경우에 쓴다.
        /// </summary>
        public static void ForEachAliveCandidateInBounds(Vector3 boundsCenter, Vector2 boundsHalfExtent, LayerMask layerMask, Action<Collider2D, Health> visit)
        {
            Vector2 pointA = new Vector2(boundsCenter.x - boundsHalfExtent.x, boundsCenter.y - boundsHalfExtent.y);
            Vector2 pointB = new Vector2(boundsCenter.x + boundsHalfExtent.x, boundsCenter.y + boundsHalfExtent.y);
            Collider2D[] candidates = Physics2D.OverlapAreaAll(pointA, pointB, layerMask);

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

        /// <summary>
        /// FindNearest의 고정 사각형 범위 버전 — origin에서 가장 가깝되, boundsCenter/boundsHalfExtent
        /// 범위 밖의 후보는 애초에 고려하지 않는다. Combat.CavalryCharge처럼 "범위 밖 대상을 향해
        /// 무제한으로 돌진/추격하지 않아야" 하는 이동 컴포넌트가 쓴다.
        /// </summary>
        public static Health FindNearestInBounds(Vector3 origin, Vector3 boundsCenter, Vector2 boundsHalfExtent, LayerMask layerMask)
        {
            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            ForEachAliveCandidateInBounds(boundsCenter, boundsHalfExtent, layerMask, (candidate, health) =>
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
