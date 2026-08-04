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
            // GameTicker는 순회 도중의 Unregister를 그 프레임이 끝난 뒤에야 실제로 반영한다(안전한
            // 도중 등록/해제를 위해서). 그래서 죽음 등으로 OnDisable→FinishSwing이 이미 회전을
            // 원위치로 되돌리고 _isSwinging을 false로 내려도, 같은 프레임 안에서 아직 리스트에
            // 남아있던 이 인스턴스가 한 번 더 Tick을 받을 수 있다 — 그 경우 여기서 즉시 빠져야
            // FinishSwing이 되돌려놓은 회전을 남은 _elapsed로 다시 덮어써버리는 걸 막을 수 있다.
            if (!_isSwinging)
            {
                return;
            }

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
