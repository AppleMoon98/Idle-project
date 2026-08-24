using Character.Events;
using Core;
using UnityEngine;
using UnityEngine.UI;

namespace Character
{
    /// <summary>
    /// 부모 캐릭터(Health)의 체력 변화를 표시하는 월드 스페이스 체력바.
    /// 최대 체력 미만일 때(=피격 1회 이상)만 표시되고, 최대 체력이면 숨겨진다.
    /// 풀에서 재사용되어 체력이 초기화될 때도 Health.OnSpawned가 발행하는
    /// CharacterHealthChangedEvent(Current == Max)를 그대로 받아 자동으로 다시 숨겨진다.
    /// fillAmount는 즉시 스냅하지 않고 fillTweenDuration에 걸쳐 부드럽게 줄어들어,
    /// 체력이 낮아 순식간에 죽는 대상이라도 줄어드는 게 눈에 보이도록 한다.
    ///
    /// 같은 오브젝트(부모)에 Character.ShieldGuard가 있으면, 몸 체력 Fill 위에 흰색 반투명
    /// shieldFillImage를 같은 자리·같은 크기로 겹쳐 방패 체력 비율을 표시한다(shieldFillImage는
    /// 옵션 - ShieldGuard가 없는 대다수 캐릭터에서는 항상 fillAmount=0으로 비어 보이거나, 필드
    /// 자체를 비워두면 조용히 무시된다). ShieldGuard.AbsorbDamage는 몸 체력(CharacterHealthChangedEvent)
    /// 을 전혀 건드리지 않고 방패만 깎으므로, 방패만 소모되는 동안에는 몸 체력 이벤트가 전혀 안 와
    /// visualRoot가 계속 숨겨진 채로 남을 수 있다 - 그래서 방패 비율이 1 미만이면 Tick에서 직접
    /// visualRoot를 켠다(몸 체력 쪽 표시/숨김 로직과 별개의 조건). 방패는 이벤트가 없어(AbsorbDamage/
    /// ResetShield 어느 쪽도 이벤트를 발행하지 않음) 매 틱 폴링으로 값을 읽는다 - 체력바 자체가
    /// 이미 Tick 기반이라 새 이벤트를 만들지 않고 재사용했다.
    /// </summary>
    public sealed class HealthBarUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject visualRoot;

        [SerializeField]
        private Image fillImage;

        [SerializeField]
        private Image shieldFillImage;

        [SerializeField]
        private float fillTweenDuration = 0.15f;

        [SerializeField]
        private float referenceOrthographicSize = 8f;

        private Health _health;
        private ShieldGuard _shieldGuard;
        private Camera _camera;
        private Vector3 _baseScale;
        private float _targetFillAmount;
        private bool _isDamaged;
        private bool _forceHidden;

        private void Awake()
        {
            _health = GetComponentInParent<Health>();
            _shieldGuard = GetComponentInParent<ShieldGuard>();
            _camera = Camera.main;
            _baseScale = transform.localScale;
        }

        private void OnEnable()
        {
            visualRoot.SetActive(false);
            fillImage.fillAmount = 1f;
            _targetFillAmount = 1f;
            _isDamaged = false;
            _forceHidden = false;

            // ShieldGuard가 없는(대다수) 캐릭터는 0으로 시작해 계속 빈 채로 남아야 한다 - 1로
            // 시작해두면 TickShieldFill이 _shieldGuard==null일 때 조용히 아무것도 안 하고
            // 리턴하기 때문에 이 흰 오버레이가 영원히 꽉 찬 채(=몸 체력 Fill을 반투명 흰색으로
            // 통째로 덮는 것처럼 보임)로 남는 버그가 있었다(실사용 중 발견). ShieldGuard가 있는
            // 캐릭터는 스폰 시 항상 방패가 가득 찬 상태(ShieldGuard.ResetShield)이므로 1로
            // 시작해야 스폰 직후 "빈 방패가 차오르는" 튐 없이 바로 가득 찬 채로 보인다.
            if (shieldFillImage != null)
            {
                shieldFillImage.fillAmount = _shieldGuard != null ? 1f : 0f;
            }

            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnHealthChanged);
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnHealthChanged);
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!Mathf.Approximately(fillImage.fillAmount, _targetFillAmount))
            {
                float step = deltaTime / fillTweenDuration;
                fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, _targetFillAmount, step);
            }

            TickShieldFill(deltaTime);
            ApplyZoomCompensation();
        }

        /// <summary>
        /// 카메라 핀치 줌(UI.CameraPinchZoomUI)으로 Camera.orthographicSize가 바뀌어도 체력바의
        /// 화면상 크기가 항상 일정해 보이도록, Combat.DamageNumber와 동일한 방식으로 현재
        /// orthographicSize와 기준 크기의 비율만큼 자기 자신의 localScale을 보정한다. Awake에서
        /// 캐싱한 _baseScale(예: Monster_Elite/_Boss가 몸집 확대를 상쇄하려고 이미 걸어둔
        /// 1/1.2, 1/1.5 카운터 스케일, section X)을 기준으로 곱해, 등급별 카운터 스케일과
        /// 줌 보정이 서로 덮어쓰지 않고 함께 적용된다.
        /// </summary>
        private void ApplyZoomCompensation()
        {
            if (_camera == null)
            {
                return;
            }

            float scale = _camera.orthographicSize / referenceOrthographicSize;
            transform.localScale = _baseScale * scale;
        }

        private void TickShieldFill(float deltaTime)
        {
            if (_shieldGuard == null || shieldFillImage == null)
            {
                return;
            }

            float shieldMax = _shieldGuard.MaxShieldHealth;
            float shieldFraction = shieldMax > 0f ? _shieldGuard.CurrentShieldHealth / shieldMax : 0f;

            if (!_forceHidden && shieldFraction < 1f && !visualRoot.activeSelf)
            {
                visualRoot.SetActive(true);
            }

            if (!Mathf.Approximately(shieldFillImage.fillAmount, shieldFraction))
            {
                float step = deltaTime / fillTweenDuration;
                shieldFillImage.fillAmount = Mathf.MoveTowards(shieldFillImage.fillAmount, shieldFraction, step);
            }
        }

        private void OnHealthChanged(CharacterHealthChangedEvent evt)
        {
            if (_health == null || evt.Character != _health.gameObject)
            {
                return;
            }

            _isDamaged = evt.Current < evt.Max;
            _targetFillAmount = evt.Max > 0f ? evt.Current / evt.Max : 0f;

            if (!_isDamaged)
            {
                fillImage.fillAmount = _targetFillAmount;
            }

            RefreshVisibility();
        }

        /// <summary>
        /// 체력 변화와 무관하게 외부(보스 패턴 등)에서 강제로 숨긴다/되돌린다 - 예: 랭크 승급전
        /// 보스가 체력 50% 페이즈 도중 몸(SpriteRenderer)을 숨기는 동안 체력바만 허공에 남아있는
        /// 것을 막기 위함(실사용 중 발견). fillAmount/피격 상태 자체는 그대로 유지한 채 표시만
        /// 끄고 켜므로, 다시 켰을 때 마지막 실제 체력 그대로 이어서 보인다 - visualRoot 자체를
        /// SetActive(false)했다 켜면 OnEnable이 다시 돌아 fillAmount가 1로 스냅되므로 그 방식은
        /// 쓸 수 없다.
        /// </summary>
        public void SetForceHidden(bool hidden)
        {
            _forceHidden = hidden;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            visualRoot.SetActive(_isDamaged && !_forceHidden);
        }
    }
}
