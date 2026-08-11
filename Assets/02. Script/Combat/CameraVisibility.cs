using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 월드 좌표가 카메라 화면(뷰포트) 안에 들어와 있는지 판정하는 공용 헬퍼.
    /// EnemyTracker(타겟 후보 필터링)와 Soldier의 화면 복귀/카이팅 로직이 동일 기준을 공유한다.
    /// </summary>
    public static class CameraVisibility
    {
        /// <summary>
        /// worldPosition이 camera의 뷰포트 안에 있는지 확인한다. viewportMargin(0~0.5)만큼 가장자리를
        /// 깎아내 판정하므로, 화면에 딱 걸친 위치는 마진이 0보다 크면 "밖"으로 취급된다. camera가
        /// null이면 판정을 생략하고 항상 안에 있는 것으로 취급한다(기존 동작으로 안전하게 폴백).
        /// </summary>
        public static bool IsOnScreen(Camera camera, Vector3 worldPosition, float viewportMargin = 0f)
        {
            if (camera == null)
            {
                return true;
            }

            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPosition);

            return viewportPoint.z > 0f
                && viewportPoint.x >= viewportMargin && viewportPoint.x <= 1f - viewportMargin
                && viewportPoint.y >= viewportMargin && viewportPoint.y <= 1f - viewportMargin;
        }

        /// <summary>
        /// worldPosition이 center를 중심으로 한 halfExtent 크기의 사각형 안에 있는지 확인한다.
        /// IsOnScreen과 달리 실시간 카메라 뷰포트가 아니라 고정된 월드 사각형 기준이다 -
        /// Services.CameraFollowService의 최광각(wideOrthographicSize) 기준 경계처럼, 플레이어가
        /// 줌으로 화면을 얼마나 좁혔는지와 무관하게 항상 같은 범위를 판정해야 하는 경우(적 타겟
        /// 후보 필터링 등)에 쓴다. margin(0~0.5)은 IsOnScreen의 viewportMargin과 같은 비율
        /// 의미로, 각 변에서 halfExtent의 2*margin만큼을 깎아낸다.
        /// </summary>
        public static bool IsWithinBounds(Vector3 center, Vector2 halfExtent, Vector3 worldPosition, float margin = 0f)
        {
            float marginX = halfExtent.x * 2f * margin;
            float marginY = halfExtent.y * 2f * margin;

            return worldPosition.x >= center.x - halfExtent.x + marginX && worldPosition.x <= center.x + halfExtent.x - marginX
                && worldPosition.y >= center.y - halfExtent.y + marginY && worldPosition.y <= center.y + halfExtent.y - marginY;
        }

        /// <summary>
        /// worldPosition을 IsWithinBounds와 같은 사각형(center/halfExtent/margin) 안쪽으로 잘라낸
        /// 좌표를 반환한다. Soldier.SoldierBehaviorController의 화면 복귀 목적지 계산처럼, "고정
        /// 범위를 벗어났으니 그 범위 안 가장 가까운 지점으로 되돌린다"는 용도에 쓴다.
        /// </summary>
        public static Vector3 ClampToBounds(Vector3 center, Vector2 halfExtent, Vector3 worldPosition, float margin = 0f)
        {
            float marginX = halfExtent.x * 2f * margin;
            float marginY = halfExtent.y * 2f * margin;

            float clampedX = Mathf.Clamp(worldPosition.x, center.x - halfExtent.x + marginX, center.x + halfExtent.x - marginX);
            float clampedY = Mathf.Clamp(worldPosition.y, center.y - halfExtent.y + marginY, center.y + halfExtent.y - marginY);

            return new Vector3(clampedX, clampedY, worldPosition.z);
        }
    }
}
