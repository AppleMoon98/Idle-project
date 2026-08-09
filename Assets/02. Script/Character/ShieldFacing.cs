using Core;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 방패 비주얼을 방패병의 현재 이동 방향(CharacterMover.Target 기준 위/아래)에 맞춰
    /// 위치시킨다 - 아래로 이동 중(목표가 자신보다 아래)이면 방패는 아래 중앙, 위로 이동
    /// 중이면 위 중앙에 온다. 이 프로젝트의 몬스터는 화면 상/하단에서 스폰돼 세로로 접근하므로
    /// 위/아래 두 상태만 있으면 충분하고, 스프라이트 자체를 회전시키는 게 아니라 방패
    /// 오브젝트의 로컬 위치만 토글하는 가벼운 방식이다.
    /// </summary>
    [RequireComponent(typeof(CharacterMover))]
    public sealed class ShieldFacing : MonoBehaviour, ITickable
    {
        [SerializeField]
        private Transform shieldVisual;

        [SerializeField]
        private float verticalOffset = 0.4f;

        [SerializeField]
        private float retargetInterval = 0.2f;

        private CharacterMover _mover;
        private float _elapsed;

        private void Awake()
        {
            _mover = GetComponent<CharacterMover>();
        }

        private void OnEnable()
        {
            TickerRegistration.Register(this);
        }

        private void OnDisable()
        {
            TickerRegistration.Unregister(this);
        }

        void ITickable.Tick(float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed < retargetInterval)
            {
                return;
            }

            _elapsed = 0f;
            UpdateFacing();
        }

        private void UpdateFacing()
        {
            if (shieldVisual == null || _mover.Target == null)
            {
                return;
            }

            bool movingDown = _mover.Target.position.y < transform.position.y;
            float y = movingDown ? -verticalOffset : verticalOffset;
            Vector3 local = shieldVisual.localPosition;
            shieldVisual.localPosition = new Vector3(0f, y, local.z);
        }
    }
}
