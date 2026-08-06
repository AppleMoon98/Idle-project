using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace Skill
{
    /// <summary>
    /// Animator/스프라이트 시트 기반 스킬 이펙트 하나의 재생을 담당하는 범용 시각 컴포넌트.
    /// Skill.SkillEffectVfx(Particle System 전용)와 같은 역할이지만 재생 수단만 다르다 —
    /// Animator에는 Particle System의 Stop Action 콜백 같은 "재생 끝남" 이벤트가 없으므로,
    /// Play() 시점에 현재 스테이트 길이를 읽어 그 시간이 지나면 스스로 풀에 반납한다
    /// (Combat.DamageNumber와 동일한 "자기 타이머로 스스로 반납" 방식). 몇 번을 시전해도
    /// 애니메이션 클립 자체가 루프로 설정돼 있어도 상관없이, 클립 길이만큼만 보여주고 반납하므로
    /// 항상 정확히 1회 재생된 것처럼 보인다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class SkillEffectAnimationVfx : MonoBehaviour, IPoolable, ITickable
    {
        private Animator _animator;
        private PoolManager _pool;
        private float _remaining;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        /// <summary>
        /// 현재 위치에서 이펙트를 처음부터 1회 재생한다. 스폰 직후(PoolManager.Get 다음) 호출한다.
        /// </summary>
        public void Play()
        {
            if (_pool == null && GameBootstrapper.Services != null)
            {
                GameBootstrapper.Services.TryGet(out _pool);
            }

            _animator.Play(0, 0, 0f);
            _remaining = _animator.GetCurrentAnimatorStateInfo(0).length;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_remaining <= 0f)
            {
                return;
            }

            _remaining -= deltaTime;

            if (_remaining <= 0f)
            {
                _pool?.Release(gameObject);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _remaining = 0f;
        }
    }
}
