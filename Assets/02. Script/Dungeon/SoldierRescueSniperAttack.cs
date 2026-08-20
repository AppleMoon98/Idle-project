using Character;
using Combat;
using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 병사 구출 던전 전용 저격 공격 하나의 전체 생애주기(경고 → 발사 → 명중 또는 빗나감)를 스스로
    /// 관리하는 자기완결형 컴포넌트(War.Boss.WarBossPatternRunner와 같은 성격). 플레이어의 현재
    /// 위치를 관통하는 무작위 각도의 선을 Launch() 시점에 한 번 고정해 telegraphDuration 동안
    /// 경고로 표시한다(선 좌표는 플레이어를 따라오지 않는다 — 경고 중 그 선에서 벗어나면 회피
    /// 가능). 경고가 끝나면 같은 선을 따라 발사체가 실제로 날아가고, 발사체가 플레이어의 실시간
    /// 위치에 닿으면 명중 — 그 순간의 진행 방향 그대로 화면(줌 최소 기준 고정 범위) 가장자리까지
    /// 넉백시킨다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SoldierRescueSniperAttack : MonoBehaviour, ITickable, IPoolable
    {
        private enum State
        {
            Warning,
            Flying
        }

        [SerializeField]
        private LineRenderer line;

        [SerializeField]
        private float telegraphDuration = 2f;

        [SerializeField]
        private float projectileSpeed = 20f;

        [SerializeField]
        private float hitRadius = 0.5f;

        [SerializeField]
        private float knockbackDuration = 0.4f;

        [SerializeField]
        private float projectileSegmentLength = 1.5f;

        [SerializeField]
        private float warningLineWidth = 0.08f;

        [SerializeField]
        private float projectileLineWidth = 0.2f;

        [SerializeField]
        private Color warningColor = new Color(1f, 0.15f, 0.1f, 0.35f);

        [SerializeField]
        private Color projectileColor = new Color(1f, 0.15f, 0.1f, 1f);

        private PoolManager _pool;
        private SoldierRescueSniperAttackSpawner _spawner;
        private Transform _playerTransform;
        private KnockbackReceiver _playerKnockback;

        private Vector3 _pointA;
        private Vector3 _pointB;
        private Vector2 _direction;
        private Vector3 _boundsCenter;
        private Vector2 _boundsHalfExtent;
        private Vector3 _currentPosition;
        private float _remainingWarning;
        private State _state;

        private void Awake()
        {
            if (line == null)
            {
                line = GetComponent<LineRenderer>();
            }

            // 프리팹의 LineRenderer 설정값과 무관하게, 스폰마다 재사용해도 항상 같은 상태로
            // 시작하도록 코드에서 직접 구성한다(Combat.RangedAttackTelegraph가 런타임에 LineRenderer를
            // 통째로 생성하는 것과 같은 이유 — 이쪽은 프리팹으로 미리 만들어두되 초기화 책임은
            // 코드가 갖는다).
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.sortingOrder = 5;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.enabled = false;
        }

        private void OnEnable()
        {
            if (_pool == null)
            {
                GameBootstrapper.Services?.TryGet(out _pool);
            }

            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// 저격 공격을 시작한다. pointA/pointB는 이미 확정된 관통선의 양 끝 — 이 컴포넌트는 판정/
        /// 타이밍만 소유하고, 좌표 계산은 스포너(SoldierRescueSniperAttackSpawner) 몫이다.
        /// </summary>
        public void Launch(
            Vector3 pointA,
            Vector3 pointB,
            Transform playerTransform,
            KnockbackReceiver playerKnockback,
            Vector3 boundsCenter,
            Vector2 boundsHalfExtent,
            SoldierRescueSniperAttackSpawner spawner)
        {
            _pointA = pointA;
            _pointB = pointB;
            _direction = ((Vector2)(pointB - pointA)).normalized;
            _playerTransform = playerTransform;
            _playerKnockback = playerKnockback;
            _boundsCenter = boundsCenter;
            _boundsHalfExtent = boundsHalfExtent;
            _spawner = spawner;
            _remainingWarning = telegraphDuration;
            _state = State.Warning;

            line.enabled = true;
            line.startWidth = warningLineWidth;
            line.endWidth = warningLineWidth;
            line.startColor = warningColor;
            line.endColor = warningColor;
            line.SetPosition(0, _pointA);
            line.SetPosition(1, _pointB);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_state == State.Warning)
            {
                TickWarning(deltaTime);
            }
            else
            {
                TickFlying(deltaTime);
            }
        }

        private void TickWarning(float deltaTime)
        {
            _remainingWarning -= deltaTime;

            float progress = telegraphDuration > 0f ? 1f - Mathf.Clamp01(_remainingWarning / telegraphDuration) : 1f;
            Color color = warningColor;
            color.a = Mathf.Lerp(warningColor.a, 0.75f, progress);
            line.startColor = color;
            line.endColor = color;

            if (_remainingWarning <= 0f)
            {
                BeginFlying();
            }
        }

        private void BeginFlying()
        {
            _state = State.Flying;
            _currentPosition = _pointA;

            line.startWidth = projectileLineWidth;
            line.endWidth = projectileLineWidth;
            line.startColor = projectileColor;
            line.endColor = projectileColor;
        }

        private void TickFlying(float deltaTime)
        {
            _currentPosition = Vector3.MoveTowards(_currentPosition, _pointB, projectileSpeed * deltaTime);

            Vector3 trailStart = _currentPosition - (Vector3)(_direction * projectileSegmentLength);
            line.SetPosition(0, trailStart);
            line.SetPosition(1, _currentPosition);

            if (_playerTransform != null && Vector3.Distance(_currentPosition, _playerTransform.position) <= hitRadius)
            {
                ApplyHit();
                return;
            }

            if (Vector3.Distance(_currentPosition, _pointB) <= 0.05f)
            {
                ReleaseSelf();
            }
        }

        /// <summary>
        /// 넉백 거리는 원래 선 좌표가 아니라 명중한 순간의 플레이어 실제 위치를 기준으로 다시
        /// 계산한다 — 경고 중 플레이어가 움직였을 수 있으므로, 항상 "지금 이 위치에서 진행 방향
        /// 그대로 화면 가장자리까지"가 되도록 한다.
        /// </summary>
        private void ApplyHit()
        {
            if (_playerKnockback != null && _playerTransform != null)
            {
                float distance = CameraVisibility.DistanceToBoundsEdge(_playerTransform.position, _direction, _boundsCenter, _boundsHalfExtent);
                _playerKnockback.ApplyKnockback(_direction, distance, knockbackDuration);
            }

            ReleaseSelf();
        }

        private void ReleaseSelf()
        {
            _pool?.Release(gameObject);
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            line.enabled = false;
            _spawner?.NotifyAttackReleased(gameObject);
            _spawner = null;
            _playerTransform = null;
            _playerKnockback = null;
        }
    }
}
