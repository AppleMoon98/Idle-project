using Core;
using Services;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 이동↔대기를 무작위로 오가며 방황하다가(각 상태 전환마다 50% 확률로 다음 상태를 다시 뽑는
    /// 동전 던지기 - 같은 상태가 연속으로 나올 수도 있다), 이동 상태에서 자기 애니메이션이 한 번
    /// 루프를 돌 때마다 주변 아군(Character.KnockbackReceiver를 가진 대상)을 한 번씩 밀어낸다.
    /// 강화석 던전 보스(Boss_TrainingDummy)처럼 원래 완전히 정지해 있던 대상에 "가끔 움직이며
    /// 주변을 밀어내는" 얕은 위협을 추가하기 위한 것 - RandomWanderer(골드 던전 파밍 몬스터)와
    /// 목적지 탐색 방식은 같지만, 고정 지속시간 상태 전환·넉백 펄스가 필요해 별도 컴포넌트로 뒀다
    /// (RandomWanderer는 순수 방황 전용으로 남겨둠).
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(Animator))]
    public sealed class PulseKnockbackWanderer : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float moveDuration = 3f;

        [SerializeField]
        private float idleDuration = 5f;

        [SerializeField]
        private float viewportMargin = 0.05f;

        [SerializeField]
        private float arrivalDistance = 0.3f;

        [SerializeField]
        private float knockbackRadius = 3f;

        [SerializeField]
        private float knockbackDistance = 2f;

        [SerializeField]
        private float knockbackDuration = 0.3f;

        [SerializeField]
        private LayerMask allyLayerMask;

        private CharacterMover _mover;
        private Animator _animator;
        private Camera _camera;
        private Transform _wanderAnchor;

        private bool _isMoving;
        private float _stateElapsed;
        private float _currentStateDuration;
        private int _lastAnimationLoopIndex;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _animator = GetComponent<Animator>();
            _camera = Camera.main;

            _wanderAnchor = new GameObject($"{name}_PulseWanderAnchor").transform;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
            EnterRandomState();
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
            _stateElapsed += deltaTime;

            if (_isMoving)
            {
                TickMovement();
                TickKnockbackPulse();
            }

            if (_stateElapsed >= _currentStateDuration)
            {
                EnterRandomState();
            }
        }

        /// <summary>
        /// 다음 상태를 50% 확률로 새로 뽑는다 - 직전 상태가 무엇이었는지와 무관한 독립 시행이라
        /// 이동-이동-대기-이동처럼 같은 상태가 연속될 수 있다(요청 사양).
        /// </summary>
        private void EnterRandomState()
        {
            _stateElapsed = 0f;
            _isMoving = Random.value < 0.5f;

            if (_isMoving)
            {
                _currentStateDuration = moveDuration;
                PickNewDestination();

                // 직전 상태도 이동이었다면(연속 당첨) 애니메이터는 계속 같은 Bouncing 상태를 재생
                // 중이라 normalizedTime이 리셋되지 않는다 - 그 시점의 현재 루프 수를 기준선으로
                // 다시 잡아야, 실제로는 새 루프가 아직 안 돌았는데도 상태 전환 자체를 "루프 완료"로
                // 오인해 펄스가 헛발동하는 것을 막을 수 있다.
                _lastAnimationLoopIndex = CurrentAnimationLoopIndex();
            }
            else
            {
                _currentStateDuration = idleDuration;
                _mover.Target = null;
            }
        }

        private void TickMovement()
        {
            float distance = Vector3.Distance(transform.position, _wanderAnchor.position);

            if (distance <= arrivalDistance)
            {
                PickNewDestination();
            }
        }

        /// <summary>
        /// RandomWanderer.PickNewDestination과 동일한 방식(CameraFollowService의 고정 범위 기준,
        /// 못 구하면 실시간 카메라 뷰포트로 방어적 대체) - 줌 배율과 무관하게 항상 같은 범위에서
        /// 뽑아야 한다는 원칙(section CD/CG)도 동일하게 적용된다.
        /// </summary>
        private void PickNewDestination()
        {
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

        private int CurrentAnimationLoopIndex()
        {
            return Mathf.FloorToInt(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
        }

        private void TickKnockbackPulse()
        {
            int loopIndex = CurrentAnimationLoopIndex();

            if (loopIndex <= _lastAnimationLoopIndex)
            {
                return;
            }

            _lastAnimationLoopIndex = loopIndex;
            PushNearbyAllies();
        }

        private void PushNearbyAllies()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, knockbackRadius, allyLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (!hit.TryGetComponent(out KnockbackReceiver receiver))
                {
                    continue;
                }

                Vector2 direction = (Vector2)hit.transform.position - (Vector2)transform.position;

                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Random.insideUnitCircle.normalized;
                }

                receiver.ApplyKnockback(direction, knockbackDistance, knockbackDuration);
            }
        }
    }
}
