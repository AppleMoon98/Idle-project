using Core;
using Managers;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 골드/강화석 던전 오버레이 컨트롤러들이 공통으로 쓰는 스폰 좌표 계산 및 PoolManager 조회.
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

        /// <summary>
        /// "Services가 있고 PoolManager를 얻을 수 있으면"이라는 반복 조회 보일러플레이트를 한 줄로 줄여준다.
        /// </summary>
        public static bool TryGetPool(out PoolManager pool)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out pool))
            {
                return true;
            }

            pool = null;
            return false;
        }
    }
}
