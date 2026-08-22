using Core;
using Managers;
using Services;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 골드/강화석/병사 구출 던전 오버레이 컨트롤러들이 공통으로 쓰는 스폰 좌표 계산 및
    /// PoolManager 조회.
    /// </summary>
    public static class DungeonSpawnUtility
    {
        /// <summary>
        /// Services.CameraFollowService의 최광각(줌 슬라이더 value=0) 기준 고정 범위 안의 랜덤한
        /// 월드 좌표를 반환한다(margin은 그 범위 가장자리로부터 제외할 비율, 0~0.5). 실시간
        /// Camera.main 뷰포트가 아니라 줌 배율과 무관한 고정 범위를 쓰는 이유 — 예전에는 현재
        /// 화면 뷰포트를 그대로 썼는데, 그러면 플레이어가 화면을 최대로 확대(줌인)한 채 던전에
        /// 입장할 경우 스폰 범위 자체가 좁아져 몬스터들이 한곳에 밀집 스폰됐다(골드 던전에서 실제
        /// 발견된 버그 — 좁게 뭉친 몬스터를 광역 공격 한 번에 farming할 수 있어 축소 상태로
        /// 입장했을 때보다 유리했다). CameraFollowService를 못 구하면(테스트 등) 방어적으로 실시간
        /// 카메라 뷰포트로 대체한다.
        /// </summary>
        public static Vector3 RandomWithinPlayAreaPosition(float margin)
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraFollowService followService))
            {
                return followService.GetRandomPointWithinBounds(margin);
            }

            Camera camera = Camera.main;

            if (camera == null)
            {
                return Vector3.zero;
            }

            float viewportX = Random.Range(margin, 1f - margin);
            float viewportY = Random.Range(margin, 1f - margin);
            float depth = Mathf.Abs(camera.transform.position.z);

            Vector3 worldPoint = camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, depth));
            worldPoint.z = 0f;

            return worldPoint;
        }

        /// <summary>
        /// 던전 보스 전용 고정 스폰 좌표 - 최광각 기준 고정 범위의 정중앙에서 위로 세로 반폭
        /// (GetWorldBoundsHalfExtent().y)의 10%만큼 띄운 지점("정 중앙에서 10%정도 위" 요청
        /// 사양). 골드 던전 파밍 몬스터처럼 여러 마리가 무작위로 흩어져야 하는 경우
        /// (RandomWithinPlayAreaPosition)와 달리, 보스는 한 마리뿐이라 항상 이 고정 지점에
        /// 등장한다 - 승급전/강화석/스킬/보스 던전 4곳의 보스 스폰이 모두 공유한다.
        /// CameraFollowService를 못 구하면(테스트 등) 방어적으로 화면 중앙보다 살짝 위로 대체한다.
        /// </summary>
        public static Vector3 BossSpawnPosition()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraFollowService followService))
            {
                Vector3 center = followService.HomeLocalPosition;
                float halfExtentY = followService.GetWorldBoundsHalfExtent().y;

                return new Vector3(center.x, center.y + halfExtentY * 0.1f, 0f);
            }

            Camera camera = Camera.main;

            if (camera == null)
            {
                return Vector3.zero;
            }

            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 worldPoint = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.6f, depth));
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
