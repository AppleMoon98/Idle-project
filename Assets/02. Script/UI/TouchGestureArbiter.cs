using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UI
{
    /// <summary>
    /// 여러 컴포넌트(UI.CameraPinchZoomUI의 핀치 줌, Character.PlayerManualMover의 탭 이동/부대
    /// 집결)가 같은 터치 스트림을 각자 독립적으로 읽어, 두 손가락 핀치의 첫 손가락이 동시에 탭
    /// 이동으로도 해석되던 문제(GitHub 이슈 #55)를 막는 단일 중재 지점.
    ///
    /// 매 호출마다 Touchscreen.current.touches를 직접 다시 읽어 판정하는 순수 정적 쿼리라 - 별도
    /// Tick 등록/호출 순서에 의존하지 않는다. 같은 프레임 안에서 CameraPinchZoomUI와
    /// PlayerManualMover 둘 다(또는 어느 한쪽만) 이 메서드를 몇 번을 불러도 답이 달라지지 않는다
    /// (그 프레임의 터치 개수라는, 프레임 내내 변하지 않는 값만으로 계산하는 순수 함수이기 때문).
    /// CameraPinchZoomUI는 이 클래스를 소비하지 않는다 - 자기 자신은 "손가락 2개가 실제로 잡힐
    /// 때만" 동작하므로 다른 제스처에 의해 오발동할 위험이 없고, 오직 PlayerManualMover의 단일
    /// 터치 판정만 멀티터치에 의해 오염될 수 있는 비대칭적인 문제라 이쪽만 이 중재를 소비한다.
    /// </summary>
    internal static class TouchGestureArbiter
    {
        private static bool _sawMultiTouchThisGesture;
        private static bool _wasApplicationFocused = true;

        /// <summary>
        /// 지금 단일 터치 기반 제스처(탭 이동, 부대 집결 홀드)를 진행해도 되는지: 이번 제스처에서
        /// 손가락이 2개 이상(3개 이상 포함) 동시에 닿은 적이 있다면, 지금은 1개로 줄었어도 모든
        /// 손가락이 완전히 떨어질 때까지 계속 억제한다 - 핀치 종료 후 남은 손가락 하나가 새 탭으로
        /// 오인되는 것을 막는다(개선 제안 3번).
        /// </summary>
        public static bool ShouldSuppressSingleTouchGestures()
        {
            return Evaluate(Application.isFocused, CountActiveTouches());
        }

        private static int CountActiveTouches()
        {
            Touchscreen touchscreen = Touchscreen.current;

            if (touchscreen == null)
            {
                return 0;
            }

            int activeTouchCount = 0;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.press.isPressed)
                {
                    activeTouchCount++;
                }
            }

            return activeTouchCount;
        }

        /// <summary>
        /// 실제 판정 로직 - 순수 함수라 RegressionChecks가 Touchscreen/InputSystem 디바이스
        /// 시뮬레이션 없이(이 개발 환경에서 신뢰할 수 없다고 이미 확인된 경로 - GitHub 이슈 #11/
        /// #55 조사 과정에서 반복 확인됨) activeTouchCount/isFocused를 직접 넣어 검증할 수 있다.
        /// 앱 포커스를 잃었다가 되찾으면(OS가 터치 해제 이벤트를 놓쳐 손가락 개수가 실제와 다르게
        /// 보일 수 있음 - Character.PlayerManualMover의 포인터 눌림 고착 문제와 같은 계열) 억제
        /// 상태를 무조건 리셋해, 남아있을 수 있는 낡은 판정을 신뢰하지 않는다.
        /// </summary>
        internal static bool Evaluate(bool isFocused, int activeTouchCount)
        {
            if (isFocused && !_wasApplicationFocused)
            {
                _sawMultiTouchThisGesture = false;
            }

            _wasApplicationFocused = isFocused;

            if (!isFocused)
            {
                return _sawMultiTouchThisGesture;
            }

            if (activeTouchCount >= 2)
            {
                _sawMultiTouchThisGesture = true;
            }
            else if (activeTouchCount == 0)
            {
                _sawMultiTouchThisGesture = false;
            }

            return _sawMultiTouchThisGesture;
        }

        /// <summary>
        /// RegressionChecks가 검사 사이에 정적 상태를 격리하기 위한 테스트 전용 리셋.
        /// </summary>
        internal static void ResetForTesting()
        {
            _sawMultiTouchThisGesture = false;
            _wasApplicationFocused = true;
        }
    }
}
