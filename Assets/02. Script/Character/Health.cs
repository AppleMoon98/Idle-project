using Character.Events;
using Core;
using Core.Pooling;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 캐릭터의 체력을 관리한다. 데미지/힐 처리와 사망 판정을 담당하며,
    /// 변화가 있을 때마다 EventBus로 이벤트를 발행한다.
    /// </summary>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class Health : MonoBehaviour, IPoolable
    {
        private CharacterStatsProvider _statsProvider;
        private ShieldGuard _shieldGuard;
        private float _current;

        /// <summary>
        /// 사망 여부.
        /// </summary>
        public bool IsDead { get; private set; }

        /// <summary>
        /// 무적 여부. true인 동안 TakeDamage는 완전히 무시된다(이벤트 발행도 없음).
        /// 기본값 false — SetInvulnerable을 명시적으로 호출하는 소수의 특수 시퀀스(보스 페이즈
        /// 전환 등)만 opt-in으로 쓴다.
        /// </summary>
        public bool IsInvulnerable { get; private set; }

        /// <summary>
        /// 현재 체력.
        /// </summary>
        public float Current => _current;

        private void Awake()
        {
            _statsProvider = GetComponent<CharacterStatsProvider>();
            _shieldGuard = GetComponent<ShieldGuard>();
            _current = _statsProvider.Stats.MaxHealth;
        }

        /// <summary>
        /// 데미지를 적용한다. 이미 사망했거나 amount가 0 이하이면 무시한다. 순서: 자신의
        /// Stats.DamageReductionPercent만큼 정률로 먼저 줄이고 → 그 결과에서 Stats.DamageReduction만큼
        /// 정액으로 깎고(기본값 둘 다 0이라 대부분의 유닛에는 영향 없음) → ShieldGuard가 있고
        /// 방패가 남아있으면 방패가 먼저 흡수한다(창병 진형의 방패 보병 등) → 그러고도 남은
        /// 만큼만 실제 체력을 깎는다. 어느 단계에서든 0 이하로 떨어지면 완전히 무효화된 것으로
        /// 취급한다(이벤트 발행도, 체력 변화도 없음).
        /// </summary>
        public void TakeDamage(float amount, bool isCritical = false, bool isPoison = false)
        {
            if (IsDead || IsInvulnerable || amount <= 0f)
            {
                return;
            }

            float afterPercent = amount * (1f - _statsProvider.Stats.DamageReductionPercent);
            float mitigatedAmount = Mathf.Max(0f, afterPercent - _statsProvider.Stats.DamageReduction);

            if (mitigatedAmount <= 0f)
            {
                return;
            }

            if (_shieldGuard != null && _shieldGuard.HasShield)
            {
                mitigatedAmount = _shieldGuard.AbsorbDamage(mitigatedAmount);

                if (mitigatedAmount <= 0f)
                {
                    return;
                }
            }

            SetCurrent(_current - mitigatedAmount);
            GameBootstrapper.Events?.Publish(new DamageAppliedEvent(gameObject, mitigatedAmount, isCritical, isPoison));

            if (_current <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// 체력을 회복한다. 이미 사망했거나 amount가 0 이하이면 무시한다.
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            SetCurrent(_current + amount);
        }

        /// <summary>
        /// MaxHealth가 바뀌었지만(강화/장비/랭크 등) 현재 체력은 그대로일 때 UI를 갱신시키기 위한
        /// 알림. SetCurrent는 clamp 결과가 기존 _current와 같으면(=MaxHealth가 늘어나기만 하고
        /// Current는 원래도 그 범위 안이었던 흔한 경우) 값이 안 바뀌었다고 보고 이벤트를 발행하지
        /// 않으므로, MaxHealth만 바뀐 경우는 그 경로로 커버되지 않는다 - 이게 없으면 체력 스탯을
        /// 찍어도 체력바의 "최대치" 표시가 실제로 피격당하거나(TakeDamage) 스테이지가 바뀌어
        /// Revive가 불릴 때까지 갱신되지 않는다(실사용 중 발견). MaxHealth를 바꾸는 모든
        /// receiver(StatEnhancementReceiver/EquipmentStatReceiver/EquipmentPossessionStatReceiver/
        /// RankStatReceiver)가 적용 직후 이 메서드를 호출해야 한다.
        /// </summary>
        public void NotifyMaxHealthChanged()
        {
            float max = _statsProvider.Stats.MaxHealth;
            _current = Mathf.Clamp(_current, 0f, max);
            GameBootstrapper.Events?.Publish(new CharacterHealthChangedEvent(gameObject, _current, max));
        }

        /// <summary>
        /// 사망 상태를 풀고 체력을 최대치로 되돌린다. 풀링되지 않는 캐릭터(Player 등)가
        /// 죽은 뒤 다시 전투에 나설 수 있도록 하는 명시적 API — OnSpawned는 PoolManager만 호출한다.
        /// </summary>
        public void Revive()
        {
            IsDead = false;
            SetCurrent(_statsProvider.Stats.MaxHealth);
            _shieldGuard?.ResetShield();
        }

        /// <summary>
        /// 무적 여부를 설정한다. 보스 페이즈 전환처럼 짧은 시간 동안 완전한 피해 면역이
        /// 필요한 특수 시퀀스가 호출한다 - 일반 전투 흐름에서는 아무도 호출하지 않는다.
        /// </summary>
        public void SetInvulnerable(bool invulnerable)
        {
            IsInvulnerable = invulnerable;
        }

        void IPoolable.OnSpawned()
        {
            Revive();
        }

        void IPoolable.OnDespawned()
        {
        }

        private void SetCurrent(float value)
        {
            float max = _statsProvider.Stats.MaxHealth;
            float clamped = Mathf.Clamp(value, 0f, max);

            if (Mathf.Approximately(clamped, _current))
            {
                return;
            }

            _current = clamped;
            GameBootstrapper.Events?.Publish(new CharacterHealthChangedEvent(gameObject, _current, max));
        }

        private void Die()
        {
            IsDead = true;
            GameBootstrapper.Events?.Publish(new CharacterDiedEvent(gameObject));
        }
    }
}
