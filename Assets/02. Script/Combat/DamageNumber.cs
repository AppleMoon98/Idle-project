using Core;
using Managers;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 데미지 숫자를 표시하고 위로 떠오르며 페이드아웃되다가 스스로 풀로 반납되는 순수 시각 컴포넌트.
    /// 데미지 계산/치명타 판정은 전부 Health/Attacker가 소유하며, 이 컴포넌트는 전달받은 값을
    /// 그리기만 한다(Combat.WeaponSwing, War.Boss.WarBossTelegraphIndicator와 동일한 분리 철학).
    /// Projectile과 마찬가지로 스스로 타이머를 관리하다 스스로 풀에 반납되는 자기완결형 컴포넌트다.
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class DamageNumber : MonoBehaviour, ITickable
    {
        [SerializeField]
        private DamageNumberConfigSO config;

        private TextMesh _textMesh;
        private Vector3 _startPosition;
        private float _elapsed;

        private void Awake()
        {
            _textMesh = GetComponent<TextMesh>();
        }

        private void OnEnable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        /// <summary>
        /// 지정된 위치에서 데미지 숫자 표시를 시작한다. 풀에서 꺼낸 직후 호출되어야 한다.
        /// </summary>
        public void Show(Vector3 worldPosition, float amount, bool isCritical)
        {
            _startPosition = worldPosition + Vector3.up * config.SpawnHeightOffset;
            transform.position = _startPosition;
            _elapsed = 0f;

            _textMesh.text = Mathf.RoundToInt(amount).ToString();
            _textMesh.fontSize = config.FontSize;
            _textMesh.color = isCritical ? config.CriticalColor : config.NormalColor;
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            float progress01 = config.Lifetime <= 0f ? 1f : Mathf.Clamp01(_elapsed / config.Lifetime);

            transform.position = _startPosition + Vector3.up * (config.RiseDistance * progress01);

            Color color = _textMesh.color;
            color.a = 1f - progress01;
            _textMesh.color = color;

            if (_elapsed >= config.Lifetime)
            {
                ReleaseSelf();
            }
        }

        private void ReleaseSelf()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(gameObject);
            }
        }
    }
}
