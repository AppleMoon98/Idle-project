using System;
using Character;
using Core;
using Services;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 사거리 내 최근접 타겟을 주기적으로 자동 공격한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class Attacker : MonoBehaviour, ITickable
    {
        [SerializeField]
        private LayerMask targetLayerMask;

        /// <summary>
        /// 실제 발사 이 값(초)만큼 전에 AttackWindupStarted를 한 번 발행한다. 0(기본값)이면 발행하지
        /// 않는다 — opt-in이라 이 필드를 건드리지 않는 기존 프리팹은 전혀 영향받지 않는다.
        /// </summary>
        [SerializeField]
        private float attackWindupLeadTime = 0f;

        /// <summary>
        /// 공격을 실제로 실행한 직후 발행된다. 같은 GameObject의 다른 컴포넌트가 구독해 후속 동작을
        /// 트리거할 수 있도록 하기 위한 것으로(예: Soldier.SoldierBehaviorController의 원거리 카이팅),
        /// EventBus를 쓰지 않는다 — 이미 같은 캐릭터 위에서 직접 참조하는 컴포넌트 사이의 알림이다.
        /// </summary>
        public event Action AttackPerformed;

        /// <summary>
        /// attackWindupLeadTime이 0보다 클 때, 실제 발사 attackWindupLeadTime초 전(사이클당 1회)
        /// 그 시점의 예상 타겟과 함께 발행된다 — 실제 발사 시점의 타겟 판정과는 독립적인 예고용
        /// 스캔이라, 아주 드물게 실제로 맞는 대상과 다를 수 있다(Combat.RangedAttackTelegraph 참고).
        /// </summary>
        public event Action<Health> AttackWindupStarted;

        private CharacterStatsProvider _statsProvider;
        private IAttackBehavior _attackBehavior;
        private CameraFollowService _cameraFollowService;
        private float _elapsed;
        private bool _windupFired;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _attackBehavior = GetComponent<IAttackBehavior>();
            GameBootstrapper.Services?.TryGet(out _cameraFollowService);
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            RuntimeStats stats = _statsProvider.Stats;

            if (_elapsed < stats.AttackInterval)
            {
                TryFireWindup(stats);
                return;
            }

            _elapsed = 0f;
            _windupFired = false;

            // 줌 슬라이더 최광각 기준 고정 범위 밖(스폰 대기 구역 등)에 있는 공격자는 공격을
            // 실행하지 않는다 - EnemyTracker의 탐지 후보 필터링과 동일 기준(실시간 카메라 뷰포트가
            // 아닌 고정 범위)을 재사용해, 아직 화면에 들어오지 않은 몬스터가 사거리만으로 화면
            // 가장자리의 아군을 미리 때리는 것을 막는다.
            if (_cameraFollowService != null
                && !CameraVisibility.IsWithinBounds(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), transform.position))
            {
                return;
            }

            Health target = FindNearestTarget(stats.AttackRange);

            if (target != null && _attackBehavior != null)
            {
                bool isCritical = UnityEngine.Random.value < stats.CriticalChance;
                float damage = isCritical ? stats.AttackPower * (1f + stats.CriticalDamageMultiplier) : stats.AttackPower;

                _attackBehavior.Execute(transform, target, damage, isCritical);
                AttackPerformed?.Invoke();
            }
        }

        /// <summary>
        /// attackWindupLeadTime이 설정돼 있으면, 이번 사이클에서 남은 시간이 그 값 이하로 처음
        /// 줄어드는 순간 한 번만(_windupFired) 예상 타겟과 함께 AttackWindupStarted를 발행한다.
        /// </summary>
        private void TryFireWindup(RuntimeStats stats)
        {
            if (attackWindupLeadTime <= 0f || _windupFired || _elapsed < stats.AttackInterval - attackWindupLeadTime)
            {
                return;
            }

            _windupFired = true;

            if (_cameraFollowService != null
                && !CameraVisibility.IsWithinBounds(_cameraFollowService.HomeLocalPosition, _cameraFollowService.GetWorldBoundsHalfExtent(), transform.position))
            {
                return;
            }

            Health target = FindNearestTarget(stats.AttackRange);

            if (target != null)
            {
                AttackWindupStarted?.Invoke(target);
            }
        }

        private Health FindNearestTarget(float range)
        {
            return NearestHealthScan.FindNearest(transform.position, range, targetLayerMask);
        }
    }
}
