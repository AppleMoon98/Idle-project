using UnityEngine;

namespace Combat
{
    /// <summary>
    /// "위협 반대 방향 몇 가지 후보각(0/±45/±90도) 중 화면 안(마진 포함)에 남으면서 위협과 가장
    /// 멀어지는 지점을 고른다"는 카이팅 후퇴 지점 계산을 공용화한 순수 함수. 원래
    /// Soldier.SoldierBehaviorController의 원거리 병사 카이팅 안에 있던 로직을 뽑아낸 것으로,
    /// RangedKiter(몬스터 궁병)가 처음으로 재사용한다 — 나중에 병사에도 병종 시스템을 적용할 때
    /// SoldierBehaviorController 쪽도 이 함수를 쓰도록 정리하면 두 구현이 갈라지지 않는다.
    /// </summary>
    public static class KiteRetreatCalculator
    {
        private static readonly float[] CandidateAngles = { 0f, 45f, -45f, 90f, -90f };

        /// <summary>
        /// selfPosition을 기준으로 threatPosition의 반대 방향 후보각들을 시도해, 화면 안에 남는
        /// 후보 중 threatPosition과 가장 멀어지는 지점을 retreatPoint에 담아 반환한다. 화면 안에
        /// 남는 후보가 하나도 없으면 false(궁지에 몰림 — 호출자가 제자리 사수 등으로 처리해야 함).
        /// </summary>
        public static bool TryFindRetreatPoint(
            Camera camera,
            Vector3 selfPosition,
            Vector3 threatPosition,
            float stepDistance,
            float screenMargin,
            out Vector3 retreatPoint)
        {
            Vector3 awayDirection = (selfPosition - threatPosition).normalized;

            Vector3 bestPoint = Vector3.zero;
            float bestSqrDistance = -1f;
            bool found = false;

            foreach (float angle in CandidateAngles)
            {
                Vector3 candidateDirection = Quaternion.Euler(0f, 0f, angle) * awayDirection;
                Vector3 candidatePoint = selfPosition + candidateDirection * stepDistance;

                if (!CameraVisibility.IsOnScreen(camera, candidatePoint, screenMargin))
                {
                    continue;
                }

                float sqrDistance = (candidatePoint - threatPosition).sqrMagnitude;

                if (sqrDistance > bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = candidatePoint;
                    found = true;
                }
            }

            retreatPoint = bestPoint;
            return found;
        }
    }
}
