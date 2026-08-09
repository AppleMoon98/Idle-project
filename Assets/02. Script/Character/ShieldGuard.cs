using Core.Pooling;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 이 유닛이 든 방패 — 자기 체력과 별도로 방패 자체의 체력을 가진다. Health.TakeDamage가 이
    /// 컴포넌트가 있으면 먼저 방패로 피해를 흡수시킨다(근접/원거리/돌진 등 피해 종류 구분 없음,
    /// 방향 구분도 없음 — "방패가 기마병 돌격이나 궁병 화살을 막는다"는 요청을 데미지 소스를
    /// 가리지 않는 단순한 형태로 구현한 것). 방패가 다 흡수하지 못한 나머지(overflow)는
    /// Health.TakeDamage가 반환값을 이어받아 이 유닛 자신의 몸 체력에 적용한다 — 방패는 오직
    /// 이 유닛 자신만 지킨다. 다른 유닛(예: 대형 뒤에 선 창병)에게 피해를 대신 전달하지
    /// 않는다 — 방패병과 창병은 서로 다른 개체이므로 데미지를 공유하지 않으며, 창병의 "보호"는
    /// 순전히 위치(방패병 뒤에 서는 것, Combat.FormationFollower/GuardPositioner)에서만
    /// 나온다 — 빠른 유닛이 우회해 창병을 직접 노리거나, 애초에 창병이 먼저 표적이 되는 것도
    /// 자연스러운 결과로 허용한다. shieldVisual(옵션)이 지정돼 있으면 방패가 남아있는 동안만
    /// 보이고 깨지는 순간 숨겨져, 스프라이트가 같아 구분이 어려운 방패병들 사이에서도 방패가
    /// 아직 있는지 한눈에 알 수 있다.
    /// </summary>
    public sealed class ShieldGuard : MonoBehaviour, IPoolable
    {
        [SerializeField]
        private float maxShieldHealth = 25f;

        [SerializeField]
        private GameObject shieldVisual;

        private float _currentShieldHealth;

        /// <summary>
        /// 방패가 아직 남아있는지(0보다 크면 true).
        /// </summary>
        public bool HasShield => _currentShieldHealth > 0f;

        public float CurrentShieldHealth => _currentShieldHealth;

        private void Awake()
        {
            ResetShield();
        }

        void IPoolable.OnSpawned()
        {
            // 풀에서 재사용되는 인스턴스는 Awake가 다시 실행되지 않으므로, 이전 생에서 방패가
            // 깨진 채였다면 여기서 다시 채워주지 않으면 영구히 방패 없는 상태로 남는다.
            ResetShield();
        }

        void IPoolable.OnDespawned()
        {
        }

        private void ResetShield()
        {
            _currentShieldHealth = maxShieldHealth;

            if (shieldVisual != null)
            {
                shieldVisual.SetActive(true);
            }
        }

        /// <summary>
        /// 들어온 피해를 방패로 흡수하고, 방패가 못 막은 나머지(overflow)를 반환한다. 방패가 이미
        /// 깨졌으면 전체를 그대로 통과시킨다.
        /// </summary>
        public float AbsorbDamage(float amount)
        {
            if (_currentShieldHealth <= 0f)
            {
                return amount;
            }

            float absorbed = Mathf.Min(_currentShieldHealth, amount);
            _currentShieldHealth -= absorbed;
            float overflow = amount - absorbed;

            if (_currentShieldHealth <= 0f && shieldVisual != null)
            {
                shieldVisual.SetActive(false);
            }

            return overflow;
        }
    }
}
