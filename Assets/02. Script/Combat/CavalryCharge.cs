using System.Collections.Generic;
using Character;
using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 기마병(Cavalry) 전용 이동+공격 컴포넌트 — MonsterTargetSelector를 대신한다. 위협과 거리가
    /// chargeStartDistance보다 가까우면 KiteRetreatCalculator로 물러나 거리를 벌리고(Positioning),
    /// 충분히 벌어지면 그 순간 위협의 위치를 향해 방향을 고정하고 돌진을 시작한다(Charging).
    /// 돌진 중에는 CharacterMover를 거치지 않고 이 컴포넌트가 직접 transform을 이동시켜, 가속과
    /// 조향을 정밀하게 제어한다:
    /// - 가속: 속도가 매초 chargeAcceleration만큼 증가해 maxChargeSpeed에서 상한.
    /// - 조향: 상대가 옆으로 피하면 그쪽으로 방향을 틀 수 있지만, 회전 가능 각속도가 현재 속도에
    ///   반비례해서 줄어든다(빠를수록 핸들링 저하) — minTurnRateDegreesPerSecond 밑으로는 내려가지
    ///   않아 완전히 조향 불능이 되지는 않는다.
    /// 충돌(자기 몸 반경 + hitCheckReach 안에 위협이 들어옴)하면 기본 공격력에 (현재속도-시작속도)×
    /// bonusDamagePerSpeed만큼 추가 피해를 Health.TakeDamage로 직접 적용하고(공격 주기 시스템을
    /// 거치지 않는다 — War.Boss.WarBossPatternRunner의 광역딜과 같은 이유), 맞은 대상에
    /// KnockbackReceiver가 있으면 넉백시킨다(넉백 방향은 돌진 정면이 아니라 knockbackSidewaysAngleDegrees
    /// 만큼 옆으로 꺾은 방향 — 정면 그대로 밀어내면 돌진 속도가 넉백 속도보다 빨라 계속 따라붙으며
    /// 대상을 끝까지 끌고 가는 것처럼 보였다(실사용 중 발견). 옆으로 꺾어야 관통 경로에서 대상이
    /// 완전히 벗어난다). 명중해도 돌진을 끊지 않고 그대로
    /// 관통해서 직진한다(같은 돌진에서 이미 맞힌 대상은 _hitTargetsThisCharge로 걸러 중복 피해를
    /// 주지 않는다) — 최대 돌진 시간(maxChargeDuration)을 넘겨야 비로소 Positioning으로 돌아가
    /// 다음 돌진을 준비한다. 첫 명중 이후에는 위협을 다시 조준하는 조향(TickCharging의 방향 보정)도
    /// 멈춘다 — 그렇지 않으면 방금 지나친 대상이 여전히 가장 가까운 위협으로 잡혀 그쪽으로 다시
    /// 돌아서려 해, "관통해서 직진"이 아니라 제자리에서 맴도는 것처럼 보인다. 돌진 중에는 자신의
    /// CharacterSeparation(모든 캐릭터가 서로 겹치지 않게 매 틱 밀어내는 범용 컴포넌트)도 꺼둔다 —
    /// 켜둔 채로 대상과 충돌하면 그 반작용으로 기마병 자신도 반대 방향으로 밀려나 버려, 관통은커녕
    /// 튕겨 나가는 것처럼 보였다(실사용 중 발견). 근접 기본 공격(Attacker+MeleeAttackBehavior)은
    /// 이 컴포넌트와 별개로 그대로 동작한다 — 돌진 사이사이 위협이 우연히 근접 사거리에 들어오면
    /// 평범한 공격도 함께 들어간다.
    ///
    /// 돌진을 시작하기 직전, 자기 위치에서 위협까지 직선 경로(자기 몸 반경 폭)에 다른 몬스터
    /// (allyMonsterLayerMask - 이 컴포넌트의 allyLayerMask는 반대로 "공격 대상" 레이어를 뜻하므로
    /// 헷갈리지 않도록 이름을 분리했다)가 있으면 그대로 돌진하지 않는다. 대신 위협 방향 기준
    /// 좌우 측면 후보 지점들을 순서대로 시도해 직선 경로가 막히지 않는 첫 지점으로 이동만 하고,
    /// 다음 재평가(retargetInterval)에서 다시 판정한다 — 아군이 비켜나거나 자신이 옆으로 이동해
    /// 경로가 뚫리면 그때 돌진을 시작한다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class CavalryCharge : MonoBehaviour, ITickable, IMonsterMovementInitializer
    {
        private enum ChargeState
        {
            Positioning,
            Charging
        }

        [SerializeField]
        private float retargetInterval = 0.15f;

        [SerializeField]
        private float detectionRange = 20f;

        [SerializeField]
        private LayerMask allyLayerMask;

        [Header("거리 벌리기")]
        [SerializeField]
        private float chargeStartDistance = 5f;

        [SerializeField]
        private float retreatStepDistance = 2f;

        [SerializeField]
        private float retreatScreenMargin = 0.05f;

        [Header("돌진 가속/조향")]
        [SerializeField]
        private float chargeStartSpeed = 3f;

        [SerializeField]
        private float maxChargeSpeed = 9f;

        [SerializeField]
        private float chargeAcceleration = 4f;

        [SerializeField]
        private float baseTurnRateDegreesPerSecond = 180f;

        [SerializeField]
        private float minTurnRateDegreesPerSecond = 20f;

        [SerializeField]
        private float turnRatePenaltyPerSpeed = 25f;

        [SerializeField]
        private float maxChargeDuration = 3f;

        [Header("충돌")]
        [SerializeField]
        private float hitCheckReach = 0.3f;

        [SerializeField]
        private float bonusDamagePerSpeed = 2f;

        [SerializeField]
        private float knockbackDistance = 2f;

        [SerializeField]
        private float knockbackDuration = 0.3f;

        [SerializeField]
        private float knockbackSidewaysAngleDegrees = 70f;

        [Header("아군 충돌 회피")]
        [SerializeField]
        private LayerMask allyMonsterLayerMask;

        [SerializeField]
        private float allySidestepDistance = 2f;

        private static readonly float[] AllySidestepOffsets = { 1f, -1f, 2f, -2f };

        private CharacterMover _mover;
        private CharacterStatsProvider _statsProvider;
        private CharacterSeparation _separation;
        private Camera _camera;
        private Transform _retreatAnchor;

        private ChargeState _state;
        private float _elapsed;
        private Vector3 _chargeDirection;
        private float _chargeSpeed;
        private float _chargeElapsed;
        private bool _hasHitThisCharge;
        private readonly HashSet<Health> _hitTargetsThisCharge = new HashSet<Health>();

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때의 기본 위협(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _separation = GetComponent<CharacterSeparation>();
            _camera = Camera.main;
            _retreatAnchor = new GameObject("CavalryRetreatAnchor").transform;
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        private void OnDestroy()
        {
            if (_retreatAnchor != null)
            {
                Destroy(_retreatAnchor.gameObject);
            }
        }

        public void Initialize(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
            _state = ChargeState.Positioning;
            EvaluatePositioning();
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_state == ChargeState.Charging)
            {
                TickCharging(deltaTime);
                return;
            }

            _elapsed += deltaTime;

            if (_elapsed < retargetInterval)
            {
                return;
            }

            _elapsed = 0f;
            EvaluatePositioning();
        }

        private Transform FindThreat()
        {
            Health nearest = NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);
            return nearest != null ? nearest.transform : PlayerTransform;
        }

        private void EvaluatePositioning()
        {
            Transform threat = FindThreat();

            if (threat == null)
            {
                _mover.Target = null;
                return;
            }

            float distance = Vector3.Distance(transform.position, threat.position);

            if (distance < chargeStartDistance)
            {
                if (KiteRetreatCalculator.TryFindRetreatPoint(_camera, transform.position, threat.position, retreatStepDistance, retreatScreenMargin, out Vector3 retreatPoint))
                {
                    _retreatAnchor.position = retreatPoint;
                    _mover.Target = _retreatAnchor;
                    _mover.StoppingDistance = 0f;
                }
                else
                {
                    _mover.Target = null;
                }

                return;
            }

            if (IsChargeLaneBlockedByAlly(transform.position, threat.position))
            {
                if (TryFindClearSidestepPoint(threat.position, out Vector3 sidestepPoint))
                {
                    _retreatAnchor.position = sidestepPoint;
                    _mover.Target = _retreatAnchor;
                    _mover.StoppingDistance = 0f;
                }
                else
                {
                    _mover.Target = null;
                }

                return;
            }

            BeginCharge(threat.position);
        }

        /// <summary>
        /// origin에서 targetPosition까지 직선 경로(자기 몸 반경 폭)에 다른 몬스터가 있는지 확인한다.
        /// </summary>
        private bool IsChargeLaneBlockedByAlly(Vector3 origin, Vector3 targetPosition)
        {
            float bodyRadius = _separation != null ? _separation.BodyRadius : 0.5f;
            Vector3 offset = targetPosition - origin;
            float distance = offset.magnitude;

            if (distance <= 0f)
            {
                return false;
            }

            Vector3 direction = offset / distance;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, bodyRadius, direction, distance, allyMonsterLayerMask);

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider != null && hit.collider.gameObject != gameObject)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 위협 방향을 기준으로 좌우 측면 후보 지점(±1칸, ±2칸)을 순서대로 시도해, 그 지점에서
        /// 위협까지의 직선 경로가 아군으로 막히지 않는 첫 후보를 반환한다. 하나도 없으면
        /// false(제자리 대기 - 다음 재평가 때 다시 시도).
        /// </summary>
        private bool TryFindClearSidestepPoint(Vector3 targetPosition, out Vector3 sidestepPoint)
        {
            Vector3 toTarget = (targetPosition - transform.position).normalized;
            Vector3 perpendicular = new Vector3(-toTarget.y, toTarget.x, 0f);

            foreach (float multiplier in AllySidestepOffsets)
            {
                Vector3 candidate = transform.position + perpendicular * (multiplier * allySidestepDistance);

                if (!IsChargeLaneBlockedByAlly(candidate, targetPosition))
                {
                    sidestepPoint = candidate;
                    return true;
                }
            }

            sidestepPoint = Vector3.zero;
            return false;
        }

        private void BeginCharge(Vector3 targetPosition)
        {
            _state = ChargeState.Charging;
            _mover.Target = null;
            _chargeDirection = (targetPosition - transform.position).normalized;
            _chargeSpeed = chargeStartSpeed;
            _chargeElapsed = 0f;
            _hasHitThisCharge = false;
            _hitTargetsThisCharge.Clear();

            if (_separation != null)
            {
                _separation.enabled = false;
            }
        }

        private void TickCharging(float deltaTime)
        {
            _chargeElapsed += deltaTime;

            if (_chargeElapsed >= maxChargeDuration)
            {
                EndCharge();
                return;
            }

            _chargeSpeed = Mathf.Min(maxChargeSpeed, _chargeSpeed + chargeAcceleration * deltaTime);

            if (!_hasHitThisCharge)
            {
                Transform threat = FindThreat();

                if (threat != null)
                {
                    Vector3 idealDirection = (threat.position - transform.position).normalized;
                    float turnRateDegrees = Mathf.Max(minTurnRateDegreesPerSecond, baseTurnRateDegreesPerSecond - turnRatePenaltyPerSpeed * (_chargeSpeed - chargeStartSpeed));
                    _chargeDirection = Vector3.RotateTowards(_chargeDirection, idealDirection, turnRateDegrees * Mathf.Deg2Rad * deltaTime, 0f).normalized;
                }
            }

            transform.position += _chargeDirection * (_chargeSpeed * deltaTime);

            CheckChargeHit();
        }

        private void CheckChargeHit()
        {
            float bodyRadius = _separation != null ? _separation.BodyRadius : 0.5f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, bodyRadius + hitCheckReach, allyLayerMask);

            foreach (Collider2D hit in hits)
            {
                if (!hit.TryGetComponent(out Health health) || health.IsDead || _hitTargetsThisCharge.Contains(health))
                {
                    continue;
                }

                _hitTargetsThisCharge.Add(health);
                _hasHitThisCharge = true;

                float bonusDamage = (_chargeSpeed - chargeStartSpeed) * bonusDamagePerSpeed;
                health.TakeDamage(_statsProvider.Stats.AttackPower + bonusDamage);

                if (hit.TryGetComponent(out KnockbackReceiver knockback))
                {
                    Vector2 knockbackDirection = ComputeSidewaysKnockbackDirection(hit.transform.position);
                    knockback.ApplyKnockback(knockbackDirection, knockbackDistance, knockbackDuration);
                }
            }
        }

        /// <summary>
        /// 돌진 방향(_chargeDirection)을 기준으로 knockbackSidewaysAngleDegrees만큼 옆으로 꺾은
        /// 방향을 계산한다. 어느 쪽(좌/우)으로 꺾을지는 대상이 돌진 경로에서 실제로 살짝 치우친
        /// 쪽을 그대로 따른다(부딪힌 순간 두 콜라이더 중심이 정확히 일치하는 경우는 거의 없다) —
        /// 완전히 정중앙(오차 이내)이면 좌우를 무작위로 고른다.
        /// </summary>
        private Vector2 ComputeSidewaysKnockbackDirection(Vector3 targetPosition)
        {
            Vector2 forward = _chargeDirection;
            Vector2 perpendicular = new Vector2(-forward.y, forward.x);
            Vector2 toTarget = (Vector2)(targetPosition - transform.position);
            float lateralDot = Vector2.Dot(toTarget, perpendicular);
            float side = Mathf.Abs(lateralDot) > 0.01f ? Mathf.Sign(lateralDot) : (Random.value < 0.5f ? 1f : -1f);

            float angleRad = knockbackSidewaysAngleDegrees * Mathf.Deg2Rad * side;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);
            return new Vector2(forward.x * cos - forward.y * sin, forward.x * sin + forward.y * cos);
        }

        private void EndCharge()
        {
            _state = ChargeState.Positioning;
            _elapsed = retargetInterval;

            if (_separation != null)
            {
                _separation.enabled = true;
            }
        }
    }
}
