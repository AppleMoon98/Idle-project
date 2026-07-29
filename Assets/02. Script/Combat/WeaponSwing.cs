using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 무기 소켓을 부채꼴로 휘두르는 시각 효과. 타격 판정에는 관여하지 않는다.
    /// </summary>
    public sealed class WeaponSwing : MonoBehaviour, ITickable
    {
        [SerializeField]
        private float swingAngle = 90f;

        [SerializeField]
        private float swingDuration = 0.2f;

        private Quaternion _restRotation;
        private float _elapsed;
        private bool _isSwinging;

        private void Awake()
        {
            _restRotation = transform.localRotation;
        }

        private void OnDisable()
        {
            if (_isSwinging)
            {
                FinishSwing();
            }
        }

        /// <summary>
        /// 스윙 모션을 처음부터 재생한다.
        /// </summary>
        public void Play()
        {
            _elapsed = 0f;

            if (!_isSwinging)
            {
                _isSwinging = true;

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
                {
                    ticker.Register(this);
                }
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (swingDuration <= 0f || _elapsed >= swingDuration)
            {
                FinishSwing();
                return;
            }

            float t = _elapsed / swingDuration;
            float currentAngle = Mathf.Sin(t * Mathf.PI) * swingAngle;
            transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, currentAngle);
        }

        private void FinishSwing()
        {
            transform.localRotation = _restRotation;
            _isSwinging = false;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }
    }
}
