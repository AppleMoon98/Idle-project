using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 카메라 뷰포트 안의 랜덤한 지점을 목표로 계속 갱신해 방황하듯 이동시킨다.
    /// 목표에 도달하거나 wanderInterval이 지나면 새 지점을 다시 고른다.
    /// 공격/타겟팅과 무관하며, CharacterMover만 이용해 이동한다.
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
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }

            PickNewDestination();
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
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

            if (_camera == null)
            {
                return;
            }

            float viewportX = Random.Range(viewportMargin, 1f - viewportMargin);
            float viewportY = Random.Range(viewportMargin, 1f - viewportMargin);
            float depth = Mathf.Abs(_camera.transform.position.z);

            Vector3 worldPoint = _camera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, depth));
            worldPoint.z = transform.position.z;

            _wanderAnchor.position = worldPoint;
            _mover.Target = _wanderAnchor;
            _mover.StoppingDistance = 0f;
        }
    }
}
