using System;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// 누르고 있는 도중 longPressThreshold(기본 0.4초, Character.PlayerManualMover의 부대 집결
    /// 누르고-있기 판정과 같은 임계값 관례)를 넘기는 순간 즉시 OnLongPress를 발행한다(손을 뗄 때가
    /// 아니라 누르고 있는 중에 바로 반응해야 한다는 피드백으로 OnPointerUp 사후 판정에서 전환).
    /// 그 전에 손을 떼면 OnShortPress를 발행한다. 길게 누르기가 이미 발동된 뒤에 손을 떼도 추가로는
    /// 아무 이벤트도 나가지 않는다 - 한 번의 누름에 둘 다 발행되는 일은 없다.
    /// </summary>
    public sealed class LongPressPointerHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, ITickable
    {
        [SerializeField]
        private float longPressThreshold = 0.4f;

        public event Action OnShortPress;
        public event Action OnLongPress;

        private bool _isPressed;
        private bool _hasFiredLongPress;
        private float _pressStartTime;

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

            _isPressed = false;
            _hasFiredLongPress = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            _hasFiredLongPress = false;
            _pressStartTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isPressed)
            {
                return;
            }

            _isPressed = false;

            // 이미 누르고 있는 도중에 OnLongPress가 발동됐다면(Tick 참고) 손을 떼는 시점엔
            // 아무 것도 추가로 발행하지 않는다 - 짧은 탭/긴 누르기는 배타적이어야 한다.
            if (!_hasFiredLongPress)
            {
                OnShortPress?.Invoke();
            }
        }

        /// <summary>
        /// 누른 채로 버튼 영역을 벗어나면 취소한다(짧은 탭/길게 누르기 둘 다 발행하지 않음) -
        /// UnityEngine.UI.Button.onClick도 같은 규칙(눌린 채 밖으로 나가면 클릭 취소)이라 기존 UX와 일관된다.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            _isPressed = false;
            _hasFiredLongPress = false;
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isPressed || _hasFiredLongPress)
            {
                return;
            }

            if (Time.unscaledTime - _pressStartTime < longPressThreshold)
            {
                return;
            }

            _hasFiredLongPress = true;
            OnLongPress?.Invoke();
        }
    }
}
