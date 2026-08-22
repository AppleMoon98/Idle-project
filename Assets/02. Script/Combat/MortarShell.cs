using Character;
using Core;
using Managers;
using Stage.Events;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 공성병(Siege) 박격포탄 - Combat.SiegeMortarAttackBehavior가 발사한 뒤, 고정된 착탄
    /// 지점(Launch 시점에 한 번만 고정, 호밍 없음 - Combat.Projectile과 동일한 관례)까지 직선으로
    /// 날아가며 계속 제자리 회전(spin)한다. 도착하면 원래 타겟에게 정타 피해를, 착탄 지점 주변
    /// splashRadius 안의 다른 대상에게 splashDamage를 입힌 뒤 스스로 반납된다(Combat.
    /// SplashAttackBehavior의 즉시 적용 로직을 "도착 시점"으로 미룬 것과 같은 공식). 타겟이 비행
    /// 중 죽어도 착탄 지점 자체는 이미 고정돼 있어 경로에 영향이 없다 - Combat.Projectile이 겪었던
    /// "타겟 사망 시 처리"(section FO) 문제가 애초에 없다.
    /// </summary>
    public sealed class MortarShell : MonoBehaviour, ITickable
    {
        private const float HitDistance = 0.15f;

        [SerializeField]
        private float spinDegreesPerSecond = 720f;

        private Health _target;
        private float _damage;
        private bool _isCritical;
        private float _speed;
        private int _spinSign;
        private float _splashRadius;
        private float _splashDamageMultiplier;
        private LayerMask _splashLayerMask;
        private Vector3 _destination;
        private GameObject _telegraphInstance;
        private bool _released;

        private void OnEnable()
        {
            _released = false;
            transform.rotation = Quaternion.identity;
            TickerRegistration.Register(this);
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            ReleaseSelf();
        }

        /// <summary>
        /// 포탄을 발사한다. 풀에서 꺼낸 직후 호출되어야 한다. spinClockwise는 발사 순간 공성병이
        /// 바라보던 방향(우측이면 시계 방향, 좌측이면 반시계 방향)으로 정한다. telegraphInstance는
        /// 착탄 지점에 이미 표시돼 있는 War.Boss.WarBossTelegraphIndicator 인스턴스 - 도착 시점에
        /// 이 포탄이 함께 반납한다(포탄과 예고 표시의 생명주기를 하나로 묶는다).
        /// </summary>
        public void Launch(Vector3 destination, Health target, float damage, bool isCritical, float speed, bool spinClockwise, float splashRadius, float splashDamageMultiplier, LayerMask splashLayerMask, GameObject telegraphInstance)
        {
            _destination = destination;
            _target = target;
            _damage = damage;
            _isCritical = isCritical;
            _speed = speed;
            _spinSign = spinClockwise ? -1 : 1;
            _splashRadius = splashRadius;
            _splashDamageMultiplier = splashDamageMultiplier;
            _splashLayerMask = splashLayerMask;
            _telegraphInstance = telegraphInstance;
        }

        void ITickable.Tick(float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, _destination, _speed * deltaTime);
            transform.Rotate(0f, 0f, _spinSign * spinDegreesPerSecond * deltaTime);

            if (Vector3.Distance(transform.position, _destination) <= HitDistance)
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            if (_target != null && !_target.IsDead)
            {
                _target.TakeDamage(_damage, _isCritical);
            }

            float splashDamage = _damage * _splashDamageMultiplier;

            if (splashDamage > 0f)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(_destination, _splashRadius, _splashLayerMask);

                foreach (Collider2D hit in hits)
                {
                    if (hit.TryGetComponent(out Health hitHealth) && hitHealth != _target && !hitHealth.IsDead)
                    {
                        hitHealth.TakeDamage(splashDamage, _isCritical);
                    }
                }
            }

            ReleaseSelf();
        }

        private void ReleaseSelf()
        {
            if (_released)
            {
                return;
            }

            _released = true;

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            if (_telegraphInstance != null)
            {
                pool.Release(_telegraphInstance);
                _telegraphInstance = null;
            }

            pool.Release(gameObject);
        }
    }
}
