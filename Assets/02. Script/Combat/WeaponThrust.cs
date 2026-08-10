using Core;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 무기 소켓을 정면(로컬 +X, 무기 소켓이 캐릭터 중심에서 바깥쪽을 향하는 방향)으로 내질렀다가
    /// 되돌리는 찌르기 모션. 창처럼 긴 리치를 정면으로 뻗어 찌르는 병과(창병)용으로,
    /// 회전 대신 위치를 움직인다는 점만 <see cref="WeaponSwing"/>과 다르다. 타격 판정에는 관여하지 않는다.
    /// </summary>
    public sealed class WeaponThrust : WeaponMotion, ITickable
    {
        [SerializeField]
        private float thrustDistance = 0.35f;

        [SerializeField]
        private float thrustDuration = 0.18f;

        private Vector3 _restLocalPosition;
        private float _elapsed;
        private bool _isThrusting;

        private void Awake()
        {
            _restLocalPosition = transform.localPosition;
        }

        private void OnDisable()
        {
            if (_isThrusting)
            {
                FinishThrust();
            }
        }

        public override void Play()
        {
            _elapsed = 0f;

            if (!_isThrusting)
            {
                _isThrusting = true;

                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
                {
                    ticker.Register(this);
                }
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            // WeaponSwing과 같은 이유(섹션 AP 참고)로, OnDisable→FinishThrust가 이미 위치를
            // 원위치로 되돌린 뒤에도 같은 프레임 안에서 한 번 더 Tick이 들어올 수 있어 즉시 빠진다.
            if (!_isThrusting)
            {
                return;
            }

            _elapsed += deltaTime;

            if (thrustDuration <= 0f || _elapsed >= thrustDuration)
            {
                FinishThrust();
                return;
            }

            float t = _elapsed / thrustDuration;
            float currentOffset = Mathf.Sin(t * Mathf.PI) * thrustDistance;
            transform.localPosition = _restLocalPosition + new Vector3(currentOffset, 0f, 0f);
        }

        private void FinishThrust()
        {
            transform.localPosition = _restLocalPosition;
            _isThrusting = false;

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }
    }
}
