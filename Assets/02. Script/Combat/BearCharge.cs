using System.Collections.Generic;
using Character;
using Core;
using Services;
using Stage.Events;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 기마병(곰) 전용 이동+공격 컴포넌트 — MonsterTargetSelector를 대신한다. 위협과의 거리가
    /// chargeStartDistance 이상이면 그 순간 위협의 위치를 향해 방향을 고정하고 즉시 최대 속도로
    /// 돌진을 시작한다(Charging). chargeStartDistance보다 가까우면 물러나지 않고 CharacterMover로
    /// 위협에게 다가가 Stats.AttackRange(사거리 2 안팎의 근접 교전 거리)에서 멈춘다(Engaging) —
    /// 그 뒤로는 같은 오브젝트의 Attacker+Combat.SplashAttackBehavior가 완전히 독립적으로 사거리를
    /// 판정해 광역 공격을 반복한다(EnemyTracker가 CharacterMover를 세팅해두고 Attacker가 알아서
    /// 쏘는 것과 동일한 분업 — 이 컴포넌트는 "어디에 세워둘지"만 결정한다).
    ///
    /// 돌진(Charging) 중에는 CharacterMover를 거치지 않고 이 컴포넌트가 직접 transform을 이동시켜
    /// 조향을 정밀하게 제어한다:
    /// - 속도: BeginCharge 시점에 즉시 maxChargeSpeed로 시작한다(더 이상 가속 구간이 없다 — "즉시
    ///   최대 이동속도로 돌진"이라는 요청 사양). chargeStartSpeed는 실제 시작 속도가 아니라 보너스
    ///   데미지/조향 둔화 계산의 기준값(0점)으로만 남아있다.
    /// - 조향: 상대가 옆으로 피하면 그쪽으로 방향을 틀 수 있지만, 회전 가능 각속도가 현재 속도에
    ///   반비례해서 줄어든다(빠를수록 핸들링 저하) — minTurnRateDegreesPerSecond 밑으로는 내려가지
    ///   않아 완전히 조향 불능이 되지는 않는다. 이제 속도가 항상 maxChargeSpeed로 일정하므로 이
    ///   각속도도 돌진 내내 일정하다(예전처럼 초반엔 민첩하다가 점점 둔해지는 곡선이 아니다).
    /// 충돌(자기 몸 반경 + hitCheckReach 안에 위협이 들어옴)하면 기본 공격력에 (현재속도-
    /// chargeStartSpeed)×bonusDamagePerSpeed만큼 추가 피해를 Health.TakeDamage로 직접 적용하고
    /// (공격 주기 시스템을 거치지 않는다 — War.Boss.WarBossPatternRunner의 광역딜과 같은 이유),
    /// 맞은 대상에 KnockbackReceiver가 있으면 넉백시킨다(넉백 방향은 돌진 정면이 아니라
    /// knockbackSidewaysAngleDegrees만큼 옆으로 꺾은 방향 — 정면 그대로 밀어내면 돌진 속도가
    /// 넉백 속도보다 빨라 계속 따라붙으며 대상을 끝까지 끌고 가는 것처럼 보였다(실사용 중 발견).
    /// 옆으로 꺾어야 관통 경로에서 대상이 완전히 벗어난다). 명중해도 돌진을 끊지 않고 그대로
    /// 관통해서 직진한다(같은 돌진에서 이미 맞힌 대상은 _hitTargetsThisCharge로 걸러 중복 피해를
    /// 주지 않는다) — 첫 명중 시점(_hitElapsedMark)부터 chargeOverrunDuration만큼 더 지나야 비로소
    /// Positioning으로 돌아가 다음 판단을 준비한다(ShouldEndCharge). 아직 한 번도 못 맞혔으면
    /// maxChargeDuration을 안전망 삼아 그때까지는 절대 조기 종료되지 않는다 — 위협(특히 계속
    /// 움직이는 플레이어)을 따라잡는 데 필요한 실제 거리를 BeginCharge 시점 스냅샷 거리로 미리
    /// 못박아두지 않기 위함이다. 첫 명중 이후에는 위협을 다시 조준하는 조향(TickCharging의 방향
    /// 보정)도 멈춘다 — 그렇지 않으면 방금 지나친 대상이 여전히 가장 가까운 위협으로 잡혀 그쪽으로
    /// 다시 돌아서려 해, "관통해서 직진"이 아니라 제자리에서 맴도는 것처럼 보인다. 돌진 중에는
    /// 자신의 CharacterSeparation도 꺼둔다 — 켜둔 채로 대상과 충돌하면 그 반작용으로 기마병 자신도
    /// 반대 방향으로 밀려나 버려, 관통은커녕 튕겨 나가는 것처럼 보였다(실사용 중 발견).
    ///
    /// 돌진 시작 전 아군이 경로를 막고 있는지 확인해 옆으로 비켜서던 회피 판정(및 그 판정이 계속
    /// 실패할 때의 강제 돌진 폴백)은 제거됐다 — Character.CharacterSeparation.ignoreLayerMask가 이
    /// 유닛의 아군 레이어를 무시하도록 프리팹에 설정돼 있어, 애초에 아군끼리 서로 밀어내지 않으므로
    /// 경로가 아군으로 막힐 일 자체가 없다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class BearCharge : MonoBehaviour, ITickable, IMonsterMovementInitializer
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

        [Header("교전 판단")]
        [SerializeField]
        private float chargeStartDistance = 5f;

        [Header("돌진 속도/조향")]
        [SerializeField]
        private float chargeStartSpeed = 3f;

        [SerializeField]
        private float maxChargeSpeed = 9f;

        [SerializeField]
        private float baseTurnRateDegreesPerSecond = 180f;

        [SerializeField]
        private float minTurnRateDegreesPerSecond = 20f;

        [SerializeField]
        private float turnRatePenaltyPerSpeed = 25f;

        [SerializeField]
        private float chargeOverrunDuration = 0.6f;

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

        private CharacterMover _mover;
        private CharacterStatsProvider _statsProvider;
        private CharacterSeparation _separation;
        private CameraFollowService _cameraFollowService;

        private ChargeState _state;
        private float _elapsed;
        private Vector3 _chargeDirection;
        private float _chargeSpeed;
        private float _chargeElapsed;
        private float _hitElapsedMark;
        private bool _hasHitThisCharge;
        private readonly HashSet<Health> _hitTargetsThisCharge = new HashSet<Health>();

        /// <summary>
        /// 탐지 범위 안에 아무 대상도 없을 때의 기본 위협(플레이어). MonsterSpawner가 스폰 직후 주입한다.
        /// </summary>
        public Transform PlayerTransform { get; private set; }

        /// <summary>
        /// 지금 돌진 중인지 여부 — 이 컴포넌트가 CharacterMover를 거치지 않고 직접 이동시키는 동안은
        /// CharacterMover 기반의 일반적인 "이동 중" 판정(Target != null && 거리 > StoppingDistance)이
        /// 통하지 않으므로, 애니메이션 컨트롤러(Character.Animation.BearAnimationController)가 Run 재생
        /// 여부를 판단하는 데 이 값을 직접 읽는다.
        /// </summary>
        public bool IsCharging => _state == ChargeState.Charging;

        /// <summary>
        /// 돌진 중 실제 이동 방향(정규화됨). 애니메이션 컨트롤러가 좌우 반전을 판정하는 데 쓴다 —
        /// 돌진 중에는 CharacterMover.Target이 없어(직접 transform을 옮기므로) 그 판정을 대신할
        /// 기준이 필요하다.
        /// </summary>
        public Vector3 ChargeDirection => _chargeDirection;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _separation = GetComponent<CharacterSeparation>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        /// <summary>
        /// 몬스터 쪽은 스테이지 종료 시 이미 풀로 반환·비활성화된 뒤라 이 이벤트를 받을 일이 없다
        /// - 실질적으로는 병사(Soldier_Bear)처럼 스테이지 전환에도 파괴되지 않고 그 자리에서
        /// 순간이동만 당하는 경우를 위한 것이다. Charging 상태로 돌진 중이던 유닛을 텔레포트만
        /// 시키고 _chargeDirection/_chargeSpeed 등 내부 상태를 그대로 두면, 다음 틱에 텔레포트된
        /// 새 위치에서 이전 방향 그대로 다시 튀어나가 "잠깐 계속 움직이는" 것처럼 보인다(실사용 중
        /// 발견) - 텔레포트 전후 순서와 무관하게(이 핸들러는 위치가 아니라 자기 내부 상태만
        /// 리셋한다) 즉시 Positioning으로 되돌리고 이동을 멈춘다.
        /// </summary>
        private void OnStageChanged(StageChangedEvent evt)
        {
            _state = ChargeState.Positioning;
            _mover.Target = null;
            _chargeSpeed = 0f;
            _hasHitThisCharge = false;
            _hitTargetsThisCharge.Clear();

            if (_separation != null)
            {
                _separation.enabled = true;
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

        /// <summary>
        /// 카메라 최광각 고정 범위(줌 배율과 무관) 안의 대상만 위협으로 고려한다 — 범위 밖 대상을
        /// 향해 detectionRange만 보고 돌진을 걸면, 최대 3초·27유닛까지 무경계로 직진하는 돌진
        /// 특성상 기마병 자신은 물론 그걸 쫓아오는 몬스터 무리 전체가 고정 범위 밖으로 끌려나가는
        /// 문제가 실사용 중 발견됐다(그 결과 EnemyTracker 기반 아군들이 범위 안에서 대상을 못 찾아
        /// 정지한 것처럼 보였다). CameraFollowService를 못 구했을 때만(방어적 폴백) 기존 raw-radius
        /// 스캔으로 대체한다.
        /// </summary>
        private Transform FindThreat()
        {
            Health nearest = _cameraFollowService != null
                ? NearestHealthScan.FindNearestInBounds(transform.position, _cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), allyLayerMask)
                : NearestHealthScan.FindNearest(transform.position, detectionRange, allyLayerMask);

            return nearest != null ? nearest.transform : PlayerTransform;
        }

        /// <summary>
        /// 위협과의 거리가 chargeStartDistance 이상이면 돌진을 시작하고, 그보다 가까우면 물러나지
        /// 않고 CharacterMover.Target을 위협으로 세팅해 Stats.AttackRange까지 다가가게 한다 —
        /// 이후 광역 공격은 같은 오브젝트의 Attacker+SplashAttackBehavior가 완전히 독립적으로
        /// 처리한다(이 메서드는 "어디로 걸어갈지"만 결정한다).
        /// </summary>
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
                _mover.Target = threat;
                _mover.StoppingDistance = _statsProvider.Stats.AttackRange;
                return;
            }

            BeginCharge(threat.position);
        }

        private void BeginCharge(Vector3 targetPosition)
        {
            _state = ChargeState.Charging;
            _mover.Target = null;
            _chargeDirection = (targetPosition - transform.position).normalized;
            _chargeSpeed = maxChargeSpeed;
            _chargeElapsed = 0f;
            _hitElapsedMark = 0f;
            _hasHitThisCharge = false;
            _hitTargetsThisCharge.Clear();

            if (_separation != null)
            {
                _separation.enabled = false;
            }
        }

        /// <summary>
        /// 아직 아무것도 못 맞혔으면 maxChargeDuration(안전망 — 위협이 계속 피하거나 다른 이유로
        /// 영영 안 맞을 때만 발동)을, 이미 맞혔으면 그 순간(_hitElapsedMark)부터
        /// chargeOverrunDuration만 더 지나면 돌진을 끝낸다. 명중 시점 기준으로 재는 게 핵심 —
        /// 위협이 아무리 움직여도 실제로 따라잡을 때까지는 절대 조기 종료되지 않는다.
        /// </summary>
        private bool ShouldEndCharge()
        {
            return _hasHitThisCharge
                ? _chargeElapsed - _hitElapsedMark >= chargeOverrunDuration
                : _chargeElapsed >= maxChargeDuration;
        }

        private void TickCharging(float deltaTime)
        {
            _chargeElapsed += deltaTime;

            if (ShouldEndCharge())
            {
                EndCharge();
                return;
            }

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

                if (!_hasHitThisCharge)
                {
                    _hasHitThisCharge = true;
                    _hitElapsedMark = _chargeElapsed;
                }

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
