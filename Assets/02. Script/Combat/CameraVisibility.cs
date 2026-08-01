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
    }
}
