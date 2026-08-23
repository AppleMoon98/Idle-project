using System.Collections.Generic;
using Character;
using UnityEngine;

namespace Combat.BossPattern
{
    /// <summary>
    /// 보스 패턴의 두 원시 판정 도형(직사각형/부채꼴)에 대한 순수 히트-테스트 헬퍼. "겹치는
    /// 콜라이더를 모으고 죽었거나 Health가 없는 후보는 건너뛴다"는 Combat.NearestHealthScan/
    /// Skill.Effects.AreaDamageSkillEffect와 같은 관용구를 그대로 따른다. 판정을 언제/어디서
    /// 실행할지, 결과로 무엇을 할지는 전부 호출자가 소유하며 이 클래스는 순수 조회만 제공한다.
    /// </summary>
    public static class BossPatternShapes
    {
        /// <summary>
        /// center를 중심으로 angleDeg만큼 회전된 length(가로)×width(세로) 직사각형 범위 안의
        /// 살아있는 대상을 모은다. angleDeg=0이면 직사각형의 긴 변(length)이 월드 +X를 향한다.
        /// </summary>
        public static IEnumerable<Health> FindHitsInRectangle(Vector2 center, float length, float width, float angleDeg, LayerMask layerMask)
        {
            Collider2D[] candidates = Physics2D.OverlapBoxAll(center, new Vector2(length, width), angleDeg, layerMask);

            foreach (Collider2D candidate in candidates)
            {
                if (candidate.TryGetComponent(out Health health) && !health.IsDead)
                {
                    yield return health;
                }
            }
        }

        /// <summary>
        /// origin을 꼭짓점으로, forwardDirection 방향을 중심으로 angleDeg만큼 벌어진 부채꼴
        /// (반지름 radius) 범위 안의 살아있는 대상을 모은다. innerRadius가 0보다 크면 그 안쪽은
        /// 제외해 고리(도넛) 모양으로 판정한다(기본값 0 = 기존과 동일한 꽉 찬 부채꼴).
        /// </summary>
        public static IEnumerable<Health> FindHitsInSector(Vector2 origin, float radius, float angleDeg, Vector2 forwardDirection, LayerMask layerMask, float innerRadius = 0f)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(origin, radius, layerMask);
            float halfAngle = angleDeg * 0.5f;

            foreach (Collider2D candidate in candidates)
            {
                if (!candidate.TryGetComponent(out Health health) || health.IsDead)
                {
                    continue;
                }

                Vector2 toCandidate = (Vector2)candidate.transform.position - origin;

                if (innerRadius > 0f && toCandidate.sqrMagnitude < innerRadius * innerRadius)
                {
                    continue;
                }

                // 대상이 꼭짓점과 정확히 겹치면(0벡터) 각도가 정의되지 않으므로 그냥 판정 안에
                // 있는 것으로 취급한다.
                if (toCandidate.sqrMagnitude <= Mathf.Epsilon || Vector2.Angle(forwardDirection, toCandidate) <= halfAngle)
                {
                    yield return health;
                }
            }
        }

        /// <summary>
        /// angleDeg=0을 월드 +X로 보는 이 클래스 전체의 각도 규약에 맞춘 단위 방향 벡터.
        /// </summary>
        public static Vector2 AngleToDirection(float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>
        /// AngleToDirection의 역변환.
        /// </summary>
        public static float DirectionToAngle(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }
    }
}
