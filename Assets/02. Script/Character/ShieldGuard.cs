using Core.Pooling;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 이 유닛이 든 방패 — 자기 체력과 별도로 방패 자체의 체력을 가진다. 방패 최대 체력은 고정값이
    /// 아니라 이 유닛 자신의 MaxHealth × shieldHealthMultiplier로 매번 다시 계산된다 - 스테이지
    /// 난이도 배율(StageMonsterScaler)로 몸 체력이 커지면 방패도 같은 비율로 같이 커진다.
    /// Health.TakeDamage가 이 컴포넌트가 있으면 먼저 방패로 피해를 흡수시킨다(근접/원거리/돌진 등
    /// 피해 종류 구분 없음, 방향 구분도 없음 — "방패가 기마병 돌격이나 궁병 화살을 막는다"는 요청을
    /// 데미지 소스를 가리지 않는 단순한 형태로 구현한 것). 방패가 다 흡수하지 못한 나머지(overflow)는
    /// Health.TakeDamage가 반환값을 이어받아 이 유닛 자신의 몸 체력에 적용한다 — 방패는 오직
    /// 이 유닛 자신만 지킨다. 다른 유닛(예: 대형 뒤에 선 창병)에게 피해를 대신 전달하지
    /// 않는다 — 방패병과 창병은 서로 다른 개체이므로 데미지를 공유하지 않으며, 창병의 "보호"는
    /// 순전히 위치(방패병 뒤에 서는 것, Combat.FormationFollower/GuardPositioner)에서만
    /// 나온다 — 빠른 유닛이 우회해 창병을 직접 노리거나, 애초에 창병이 먼저 표적이 되는 것도
    /// 자연스러운 결과로 허용한다. shieldVisual(옵션)이 지정돼 있으면 방패가 남아있는 동안만
    /// 보이고 깨지는 순간 숨겨져, 스프라이트가 같아 구분이 어려운 방패병들 사이에서도 방패가
    /// 아직 있는지 한눈에 알 수 있다.
    /// </summary>
    /// <remarks>
    /// 같은 GameObject의 다른 컴포넌트(예: SoldierStatReceiver)가 자신의 OnEnable에서
    /// Health.Revive()를 거쳐 이 컴포넌트의 ResetShield()를 간접 호출할 수 있는데, Unity는
    /// 같은 GameObject 위 컴포넌트들의 Awake/OnEnable을 선언 순서대로 인터리브해서 실행한다
    /// (Character.CharacterStatsProvider가 Stats를 지연 초기화하는 것과 같은 이유, section C) —
    /// 이 컴포넌트가 SoldierStatReceiver보다 뒤에 선언되면 아직 자신의 Awake가 돌기 전에
    /// ResetShield가 호출될 수 있다. 그래서 _statsProvider를 Awake에서만 캐싱하지 않고
    /// StatsProvider 프로퍼티로 지연 조회한다(호출 순서와 무관하게 항상 안전).
    /// </remarks>
    [RequireComponent(typeof(CharacterStatsProvider))]
    public sealed class ShieldGuard : MonoBehaviour, IPoolable
    {
        [SerializeField]
        private float shieldHealthMultiplier = 1.5f;

        [SerializeField]
        private GameObject shieldVisual;

        private CharacterStatsProvider _statsProvider;
        private float _currentShieldHealth;

        private CharacterStatsProvider StatsProvider => _statsProvider != null ? _statsProvider : (_statsProvider = GetComponent<CharacterStatsProvider>());

        /// <summary>
        /// 방패가 아직 남아있는지(0보다 크면 true).
        /// </summary>
        public bool HasShield => _currentShieldHealth > 0f;

        public float CurrentShieldHealth => _currentShieldHealth;

        /// <summary>
        /// 방패 최대 체력 - ResetShield()와 같은 공식(현재 MaxHealth × shieldHealthMultiplier)을
        /// 그때그때 다시 계산해서 돌려준다(HealthBarUI가 체력바 흰 구간 비율을 그리는 데 쓴다).
        /// </summary>
        public float MaxShieldHealth => StatsProvider.Stats.MaxHealth * shieldHealthMultiplier;

        private void Awake()
        {
            ResetShield();
        }

        void IPoolable.OnSpawned()
        {
            // 풀에서 재사용되는 인스턴스는 Awake가 다시 실행되지 않으므로, 이전 생에서 방패가
            // 깨진 채였다면 여기서 다시 채워주지 않으면 영구히 방패 없는 상태로 남는다. 이 시점의
            // MaxHealth는 아직 이전 스폰의 스케일이 남아있을 수 있는데, 뒤이어 Health.OnSpawned가
            // 호출하는 Revive()도 ResetShield()를 다시 부르고, StageMonsterScaler.ApplyScale이 그
            // 뒤에 Revive()를 한 번 더 호출해 최종적으로 정확히 스케일된 MaxHealth 기준 값으로
            // 덮어써진다(Health.Revive 참고) - 그러니 이 호출은 그 사이 잠깐의 기본값일 뿐이다.
            ResetShield();
        }

        void IPoolable.OnDespawned()
        {
        }

        /// <summary>
        /// 방패를 이 유닛 현재 MaxHealth 기준으로 가득 채운다. Health.Revive()가 (풀 재사용/부활
        /// 시점마다) 함께 호출해, 방패 체력이 항상 최신 MaxHealth(스테이지 난이도 배율 반영 후)를
        /// 따라가게 한다.
        /// </summary>
        public void ResetShield()
        {
            _currentShieldHealth = StatsProvider.Stats.MaxHealth * shieldHealthMultiplier;

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
