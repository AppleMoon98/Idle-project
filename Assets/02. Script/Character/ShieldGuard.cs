using UnityEngine;

namespace Character
{
    /// <summary>
    /// 이 유닛이 든 방패 — 자기 체력과 별도로 방패 자체의 체력을 가진다. Health.TakeDamage가 이
    /// 컴포넌트가 있으면 먼저 방패로 피해를 흡수시킨다(근접/원거리/돌진 등 피해 종류 구분 없음,
    /// 방향 구분도 없음 — "방패가 기마병 돌격이나 궁병 화살을 막는다"는 요청을 데미지 소스를
    /// 가리지 않는 단순한 형태로 구현한 v1). 방패가 깨지는 순간 넘친 피해(overflow)는 본체
    /// 체력에도 그대로 이어지고(Health.TakeDamage가 반환값을 이어받아 처리), SetProtectedUnit으로
    /// 등록해 둔 뒤쪽 유닛(예: 창병)에도 같은 양만큼 추가로 전달된다 — "방패를 부수면 보병도,
    /// 그 뒤의 창병도 다친다"는 요청을 그대로 구현한 것.
    /// </summary>
    public sealed class ShieldGuard : MonoBehaviour
    {
        [SerializeField]
        private float maxShieldHealth = 25f;

        private float _currentShieldHealth;
        private Health _protectedUnit;

        /// <summary>
        /// 방패가 아직 남아있는지(0보다 크면 true).
        /// </summary>
        public bool HasShield => _currentShieldHealth > 0f;

        public float CurrentShieldHealth => _currentShieldHealth;

        private void Awake()
        {
            _currentShieldHealth = maxShieldHealth;
        }

        /// <summary>
        /// 이 방패가 지키고 있는 뒤쪽 유닛(예: 창병)을 등록한다. 방패가 깨지는 순간 같은 피해가
        /// 그 유닛에도 전달된다. 아직 실제 진형 스폰 시스템이 없어(나중에 병법 시스템에서 호출할
        /// 예정) 지금은 외부에서 직접 호출해 주입해야 한다.
        /// </summary>
        public void SetProtectedUnit(Health protectedUnit)
        {
            _protectedUnit = protectedUnit;
        }

        /// <summary>
        /// 들어온 피해를 방패로 흡수하고, 방패가 못 막은 나머지(overflow)를 반환한다. 방패가 이미
        /// 깨졌으면 전체를 그대로 통과시킨다. 이번 피해로 방패가 막 깨졌다면(overflow가 있다면)
        /// 등록된 protectedUnit에도 같은 양을 즉시 적용한다.
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

            if (_currentShieldHealth <= 0f && overflow > 0f && _protectedUnit != null && !_protectedUnit.IsDead)
            {
                _protectedUnit.TakeDamage(overflow);
            }

            return overflow;
        }
    }
}
