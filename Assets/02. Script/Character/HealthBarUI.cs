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
    /// </summary>
    public sealed class HealthBarUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GameObject visualRoot;

        [SerializeField]
        private Image fillImage;

        [SerializeField]
        private float fillTweenDuration = 0.15f;

        private Health _health;
        private float _targetFillAmount;

        private void Awake()
        {
            _health = GetComponentInParent<Health>();
        }

        private void OnEnable()
        {
            visualRoot.SetActive(false);
            fillImage.fillAmount = 1f;
            _targetFillAmount = 1f;

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
            if (Mathf.Approximately(fillImage.fillAmount, _targetFillAmount))
            {
                return;
            }

            float step = deltaTime / fillTweenDuration;
            fillImage.fillAmount = Mathf.MoveTowards(fillImage.fillAmount, _targetFillAmount, step);
        }

        private void OnHealthChanged(CharacterHealthChangedEvent evt)
        {
            if (_health == null || evt.Character != _health.gameObject)
            {
                return;
            }

            bool damaged = evt.Current < evt.Max;
            visualRoot.SetActive(damaged);
            _targetFillAmount = evt.Max > 0f ? evt.Current / evt.Max : 0f;

            if (!damaged)
            {
                fillImage.fillAmount = _targetFillAmount;
            }
        }
    }
}
