using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 던전 오버레이가 몬스터를 화면 안 랜덤 위치에 스폰할 때 공통으로 쓰는 좌표 계산.
    /// </summary>
    public static class DungeonSpawnUtility
    {
        /// <summary>
        /// 메인 카메라 뷰포트 안(가장자리로부터 viewportMargin만큼 제외)의 랜덤한 월드 좌표를 반환한다.
        /// 카메라가 없으면 원점을 반환한다.
        /// </summary>
        public static Vector3 RandomOnScreenPosition(float viewportMargin)
        {
            Camera camera = Camera.main;

            if (camera == null)
            {
                return Vector3.zero;
            }

            float viewportX = Random.Range(viewportMargin, 1f - viewportMargin);
            float viewportY = Random.Range(viewportMargin, 1f - viewportMargin);
            float depth = Mathf.Abs(camera.transform.position.z);

            Vector3 worldPoint = camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, depth));
            worldPoint.z = 0f;

            return worldPoint;
        }
    }
}
