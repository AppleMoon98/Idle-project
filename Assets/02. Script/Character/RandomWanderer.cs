using Core;
using Services;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 고정된 실제 플레이 구역(Services.CameraFollowService의 최광각 기준 경계) 안의 랜덤한 지점을
    /// 목표로 계속 갱신해 방황하듯 이동시킨다. 목표에 도달하거나 wanderInterval이 지나면 새 지점을
    /// 다시 고른다. 공격/타겟팅과 무관하며, CharacterMover만 이용해 이동한다.
    ///
    /// 예전에는 실시간 Camera.main 뷰포트 안에서 목적지를 골랐는데, 그러면 플레이어가 화면을
    /// 확대(줌인)할수록 방황 범위 자체가 좁아져 넓게 스폰된 개체들이 시간이 지나며 좁은 화면
    /// 쪽으로 계속 몰려드는 것처럼 보였다(골드 던전에서 스폰 위치만 고치고 방황 로직은 놓쳐서
    /// 실사용 중 재발견된 문제 — Dungeon.DungeonSpawnUtility.RandomWithinPlayAreaPosition, 섹션 CG와
    /// 같은 버그 계열). CameraFollowService를 못 구하면(테스트 등) 방어적으로 실시간 카메라
    /// 뷰포트로 대체한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    public sealed class RandomWanderer : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float viewportMargin = 0.05f;

        [SerializeField]
        private float wanderInterval = 3f;

        [SerializeField]
        private float arrivalDistance = 0.3f;

        private CharacterMover _mover;
        private Transform _wanderAnchor;
        private Camera _camera;
        private float _elapsedSinceLastPick;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _camera = Camera.main;

            _wanderAnchor = new GameObject($"{name}_WanderAnchor").transform;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
            PickNewDestination();
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_wanderAnchor != null)
            {
                Destroy(_wanderAnchor.gameObject);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsedSinceLastPick += deltaTime;

            float distance = Vector3.Distance(transform.position, _wanderAnchor.position);

            if (distance <= arrivalDistance || _elapsedSinceLastPick >= wanderInterval)
            {
                PickNewDestination();
            }
        }

        private void PickNewDestination()
        {
            _elapsedSinceLastPick = 0f;

            Vector3 worldPoint;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out CameraFollowService followService))
            {
                worldPoint = followService.GetRandomPointWithinBounds(viewportMargin);
            }
            else if (_camera != null)
            {
                float viewportX = Random.Range(viewportMargin, 1f - viewportMargin);
                float viewportY = Random.Range(viewportMargin, 1f - viewportMargin);
                float depth = Mathf.Abs(_camera.transform.position.z);

                worldPoint = _camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, depth));
            }
            else
            {
                return;
            }

            worldPoint.z = transform.position.z;

            _wanderAnchor.position = worldPoint;
            _mover.Target = _wanderAnchor;
            _mover.StoppingDistance = 0f;
        }
    }
}
