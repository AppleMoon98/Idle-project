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
    ///
    /// <para>
    /// **NonAlloc 전환(GitHub 이슈 #23):** Attacker/EnemyTracker/MonsterTargetSelector/RangedKiter/
    /// FormationFollower/GuardPositioner/BearCharge/SoldierBehaviorController/여러 스킬 이펙트 등
    /// 16곳이 이 클래스를 공유해서, 옛 OverlapCircleAll/OverlapAreaAll 기반 구현은 전투 중 매우
    /// 높은 빈도로 배열을 새로 할당하고 있었다. GameTicker.Update()가 모든 ITickable.Tick()을 한
    /// 스레드에서 순차 호출하므로(재진입 없음), 이 클래스 전체가 static 버퍼 하나를 공유해도
    /// 안전하다.
    /// </para>
    /// </summary>
    public static class NearestHealthScan
    {
        private static Collider2D[] _overlapBuffer = new Collider2D[32];

        /// <summary>
        /// _overlapBuffer가 지금까지 실제로 확장된 횟수(진단용, GitHub 이슈 #23).
        /// </summary>
        public static int BufferGrowthCount { get; private set; }

        /// <summary>
        /// origin 주변 range 안, layerMask에 해당하는 콜라이더 중 살아있는 Health만 걸러
        /// visit에 하나씩 전달한다.
        /// </summary>
        public static void ForEachAliveCandidate(Vector3 origin, float range, LayerMask layerMask, Action<Collider2D, Health> visit)
        {
            int count = OverlapCircleNonAlloc(origin, range, layerMask);

            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = _overlapBuffer[i];

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
            int count = OverlapAreaNonAlloc(boundsCenter, boundsHalfExtent, layerMask);

            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = _overlapBuffer[i];

                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                visit(candidate, health);
            }
        }

        /// <summary>
        /// 위 스캔 결과 중 origin에서 가장 가까운 살아있는 Health 하나만 필요한 경우의 편의 메서드.
        /// ForEachAliveCandidate를 통하지 않고 직접 반복문으로 구현한다(GitHub 이슈 #23) - 델리게이트를
        /// 거치면 origin/nearest/nearestSqrDistance를 캡처하는 클로저가 호출마다 새로 할당됐다.
        /// Attacker/MonsterTargetSelector 등 매 틱 이 메서드를 부르는 호출부가 많아 그 할당이
        /// 누적됐다.
        /// </summary>
        public static Health FindNearest(Vector3 origin, float range, LayerMask layerMask)
        {
            int count = OverlapCircleNonAlloc(origin, range, layerMask);

            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = _overlapBuffer[i];

                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - origin).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            }

            return nearest;
        }

        /// <summary>
        /// FindNearest의 고정 사각형 범위 버전 — origin에서 가장 가깝되, boundsCenter/boundsHalfExtent
        /// 범위 밖의 후보는 애초에 고려하지 않는다. Combat.BearCharge처럼 "범위 밖 대상을 향해
        /// 무제한으로 돌진/추격하지 않아야" 하는 이동 컴포넌트가 쓴다. FindNearest와 같은 이유로
        /// 델리게이트 없는 직접 반복문으로 구현한다(GitHub 이슈 #23).
        /// </summary>
        public static Health FindNearestInBounds(Vector3 origin, Vector3 boundsCenter, Vector2 boundsHalfExtent, LayerMask layerMask)
        {
            int count = OverlapAreaNonAlloc(boundsCenter, boundsHalfExtent, layerMask);

            Health nearest = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = _overlapBuffer[i];

                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                float sqrDistance = (candidate.transform.position - origin).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = health;
                }
            }

            return nearest;
        }

        /// <summary>
        /// useTriggers는 프로젝트 전역 설정(Physics2D.queriesHitTriggers)을 그대로 따라야
        /// 레거시 OverlapCircleAll/OverlapAreaAll 오버로드와 정확히 같은 결과를 낸다(둘 다
        /// 내부적으로 이 전역 설정을 그대로 쓴다). 버퍼가 가득 차면(count == 버퍼 길이) 결과가
        /// 잘렸을 수 있다는 뜻이라, 2배로 늘려 재시도한다(GitHub 이슈 #23 - "버퍼 초과 시 누락
        /// 없이 안전하게 확장").
        /// </summary>
        private static int OverlapCircleNonAlloc(Vector2 origin, float range, LayerMask layerMask)
        {
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(layerMask);
            filter.useLayerMask = true;

            int count = Physics2D.OverlapCircle(origin, range, filter, _overlapBuffer);

            while (count == _overlapBuffer.Length)
            {
                Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
                BufferGrowthCount++;
                count = Physics2D.OverlapCircle(origin, range, filter, _overlapBuffer);
            }

            return count;
        }

        private static int OverlapAreaNonAlloc(Vector3 boundsCenter, Vector2 boundsHalfExtent, LayerMask layerMask)
        {
            Vector2 pointA = new Vector2(boundsCenter.x - boundsHalfExtent.x, boundsCenter.y - boundsHalfExtent.y);
            Vector2 pointB = new Vector2(boundsCenter.x + boundsHalfExtent.x, boundsCenter.y + boundsHalfExtent.y);

            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(layerMask);
            filter.useLayerMask = true;

            int count = Physics2D.OverlapArea(pointA, pointB, filter, _overlapBuffer);

            while (count == _overlapBuffer.Length)
            {
                Array.Resize(ref _overlapBuffer, _overlapBuffer.Length * 2);
                BufferGrowthCount++;
                count = Physics2D.OverlapArea(pointA, pointB, filter, _overlapBuffer);
            }

            return count;
        }
    }
}
