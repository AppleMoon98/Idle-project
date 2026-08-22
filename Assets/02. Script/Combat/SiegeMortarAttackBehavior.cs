using Character;
using Core;
using Managers;
using UnityEngine;
using War.Boss;

namespace Combat
{
    /// <summary>
    /// 공성병(Siege) 전용 공격 전략 - Combat.SplashAttackBehavior(즉시 적용)를 대체한다. 1초짜리
    /// 예고선(Combat.RangedAttackTelegraph, Attacker.attackWindupLeadTime로 이미 구동됨)이 끝나고
    /// Attacker가 Execute를 호출하는 순간을 "공격 모션이 실행되는" 시점으로 삼아, 그로부터
    /// launchDelaySeconds(기본 0.3초 = Siege_Attack.anim 10fps 기준 3프레임)가 지난 뒤에야 실제
    /// 포탄(Combat.MortarShell)을 발사한다 - 즉시 발사하던 RangedAttackBehavior/기존
    /// SplashAttackBehavior와 달리, Execute와 실제 발사 사이에 지연이 있는 유일한 공격 전략이라
    /// ITickable로 직접 카운트다운한다(같은 GameObject의 Attacker와는 별개 타이머). 포탄이 착탄
    /// 지점에 도착해야 비로소 데미지가 들어간다(Combat.MortarShell 참고) - Execute 시점에 데미지를
    /// 확정하는 다른 모든 공격 전략과 다른, 이 컴포넌트만의 특징이다.
    /// </summary>
    [RequireComponent(typeof(Attacker))]
    public sealed class SiegeMortarAttackBehavior : MonoBehaviour, IAttackBehavior, ITickable
    {
        /// <summary>
        /// 착탄 지점 범위 표시 색 - 적의 공격 예고(빨강, WarBossTelegraphIndicator 기본색)와 구분되는
        /// 파란색을 쓴다(Skill.Effects.MeteorSkillEffect의 포탄 낙하 예고와 동일한 색).
        /// </summary>
        private static readonly Color TelegraphColor = new(0.2f, 0.5f, 1f, 1f);

        [SerializeField]
        private WeaponMotion weaponMotion;

        [SerializeField]
        private GameObject shellPrefab;

        [SerializeField]
        private GameObject telegraphIndicatorPrefab;

        [SerializeField]
        private GameObject explosionPrefab;

        [SerializeField]
        private Transform muzzle;

        /// <summary>
        /// Execute(공격 모션 시작) 이후 실제 포탄이 발사되기까지의 지연 - 3프레임 @ 10fps = 0.3초.
        /// </summary>
        [SerializeField]
        private float launchDelaySeconds = 0.3f;

        [SerializeField]
        private float projectileSpeed = 5f;

        [SerializeField]
        private float splashRadius = 2.5f;

        [SerializeField]
        private float splashDamageMultiplier = 0.5f;

        [SerializeField]
        private LayerMask splashLayerMask;

        [SerializeField]
        private int shellPoolCapacity = 2;

        [SerializeField]
        private int shellPoolMaxSize = 4;

        [SerializeField]
        private int telegraphPoolCapacity = 2;

        [SerializeField]
        private int telegraphPoolMaxSize = 4;

        [SerializeField]
        private int explosionPoolCapacity = 2;

        [SerializeField]
        private int explosionPoolMaxSize = 4;

        private PoolManager _pool;
        private SpriteRenderer _spriteRenderer;

        private bool _pendingLaunch;
        private float _launchElapsed;
        private Vector3 _pendingDestination;
        private Health _pendingTarget;
        private float _pendingDamage;
        private bool _pendingIsCritical;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
            _pendingLaunch = false;

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out _pool))
            {
                return;
            }

            if (shellPrefab != null)
            {
                _pool.EnsurePool(shellPrefab, shellPoolCapacity, shellPoolMaxSize);
            }

            if (telegraphIndicatorPrefab != null)
            {
                _pool.EnsurePool(telegraphIndicatorPrefab, telegraphPoolCapacity, telegraphPoolMaxSize);
            }

            if (explosionPrefab != null)
            {
                _pool.EnsurePool(explosionPrefab, explosionPoolCapacity, explosionPoolMaxSize);
            }
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            _pendingLaunch = false;
        }

        public void Execute(Transform origin, Health target, float damage, bool isCritical)
        {
            _pendingLaunch = true;
            _launchElapsed = 0f;
            _pendingDestination = target.transform.position;
            _pendingTarget = target;
            _pendingDamage = damage;
            _pendingIsCritical = isCritical;

            if (weaponMotion != null)
            {
                weaponMotion.Play();
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_pendingLaunch)
            {
                return;
            }

            _launchElapsed += deltaTime;

            if (_launchElapsed >= launchDelaySeconds)
            {
                _pendingLaunch = false;
                LaunchShell();
            }
        }

        private void LaunchShell()
        {
            if (_pool == null || shellPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = muzzle != null ? muzzle.position : transform.position;
            GameObject shellInstance = _pool.Get(shellPrefab, spawnPosition, Quaternion.identity);

            if (!shellInstance.TryGetComponent(out MortarShell shell))
            {
                return;
            }

            // 스프라이트가 기본적으로 오른쪽을 보고 그려져 있다는 이 프로젝트의 관례(flipX=false가
            // 오른쪽)를 그대로 따른다 - 우측을 보고 있으면 시계 방향, 좌측을 보고 있으면 반시계 방향.
            bool facingRight = _spriteRenderer == null || !_spriteRenderer.flipX;

            GameObject telegraphInstance = null;

            if (telegraphIndicatorPrefab != null)
            {
                telegraphInstance = _pool.Get(telegraphIndicatorPrefab, _pendingDestination, Quaternion.identity);

                if (telegraphInstance.TryGetComponent(out WarBossTelegraphIndicator telegraph))
                {
                    telegraph.Show(_pendingDestination, splashRadius, TelegraphColor);
                }
            }

            shell.Launch(_pendingDestination, _pendingTarget, _pendingDamage, _pendingIsCritical, projectileSpeed, facingRight, splashRadius, splashDamageMultiplier, splashLayerMask, telegraphInstance, explosionPrefab);
        }
    }
}
