using Core;
using Core.Pooling;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 범위 공격이 실제로 적중하는 순간 재생하는 폭발 이펙트. Skill.SkillEffectAnimationVfx와
    /// 동일한 재생 방식(Animator에는 Particle System의 Stop Action 콜백이 없으므로, Play() 시점에
    /// 현재 스테이트 길이를 읽어 그 시간이 지나면 스스로 풀에 반납)을 쓰되, 이 이펙트만의 특징으로
    /// 반경(radius)에 맞춰 시각적 크기를 함께 조절한다 — 공격마다 스플래시 반경이 다를 수 있어
    /// (Combat.SplashAttackBehavior.splashRadius/Combat.MortarShell의 착탄 반경) 폭발 크기도 그
    /// 범위를 그대로 나타내야 한다. War.Boss.WarBossTelegraphIndicator.Show가 코드로 생성한 원형
    /// 스프라이트를 반경에 맞춰 스케일하는 것과 같은 방향이지만, 이쪽은 실제 아트(Explosions.png,
    /// spritePixelsToUnits 64 기준 프레임 192px = 3유닛 지름)라 그 자연 크기(NaturalDiameter)를
    /// 기준으로 배율을 계산한다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class ExplosionEffect : MonoBehaviour, IPoolable, ITickable
    {
        /// <summary>Explosions.png 프레임의 스케일 1 기준 지름(유닛) = 192px / spritePixelsToUnits(64).</summary>
        private const float NaturalDiameter = 3f;

        /// <summary>
        /// 실제 판정 반경(radius)과는 무관한 순수 시각적 배율 — 폭발이 실제 스플래시 범위보다 더
        /// 커 보이도록 렌더링 크기만 키운다(요청 사양: "범위 말고 스프라이트 랜더링 크기만"). 데미지
        /// 판정에 쓰이는 radius 인자 자체는 이 배율과 무관하게 호출자가 넘긴 값 그대로 유지된다.
        /// </summary>
        private const float VisualSizeMultiplier = 2f;

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
        /// 지정된 위치에서, 지정된 반경(공격의 범위 반경과 동일한 값)만큼의 시각적 크기로 폭발을
        /// 처음부터 1회 재생한다. 스폰 직후(PoolManager.Get 다음) 호출한다.
        /// </summary>
        public void Play(Vector3 position, float radius)
        {
            transform.position = position;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, radius * 2f / NaturalDiameter * VisualSizeMultiplier);

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
