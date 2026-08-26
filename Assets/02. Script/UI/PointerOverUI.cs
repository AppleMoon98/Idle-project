using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UI
{
    /// <summary>
    /// 화면 좌표가 UI(EventSystem 레이캐스트 대상) 위에 있는지 판정하는 공용 헬퍼. 원래
    /// Character.PlayerManualMover 하나만 갖고 있던 로직을 UI.CameraPinchZoomUI(GitHub 이슈
    /// #11 - 팝업 스크롤 중 마우스 휠/핀치가 배경 카메라 줌까지 함께 바꾸던 문제)도 필요로 하게
    /// 되면서 공유 정적 클래스로 뽑았다.
    ///
    /// EventSystem.current.IsPointerOverGameObject()는 터치 입력의 첫 프레임(방금 누른 그 프레임)
    /// 에는 아직 EventSystem이 그 터치를 UI 레이캐스트로 처리하기 전이라 신뢰할 수 없다(항상
    /// false를 반환) - 대신 지금 이 프레임의 스크린 좌표로 직접 UI 레이캐스트를 쏴서 즉시
    /// 판정하면 이 한 프레임 지연 문제가 없다.
    ///
    /// 레이캐스트 결과 중 순수 UnityEngine.UI.Text(클릭 핸들러 없는 정보성 라벨 - 던전 HUD의
    /// "제한시간" 텍스트 등)만 걸리는 경우는 UI 위로 치지 않는다 - 새 Text를 만들면
    /// raycastTarget이 기본값 true라 클릭 핸들러가 전혀 없는데도 그 밑의 조작(탭 이동, 카메라
    /// 줌)이 막히는 게 실사용 중 발견됐었다. 버튼의 라벨 Text를 눌러도 같은 버튼의 배경 Image가
    /// 레이캐스트 결과에 함께 잡히므로 정상적으로 계속 막히고, 팝업 배경도 보통 Image라 그대로
    /// 막힌다 - Text 하나만 단독으로 잡힐 때만 예외적으로 통과시킨다.
    /// </summary>
    internal static class PointerOverUI
    {
        private static readonly List<RaycastResult> ScratchResults = new();

        internal static bool IsOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            PointerEventData eventData = new(EventSystem.current) { position = screenPosition };
            ScratchResults.Clear();
            EventSystem.current.RaycastAll(eventData, ScratchResults);

            foreach (RaycastResult result in ScratchResults)
            {
                if (!result.gameObject.TryGetComponent(out UnityEngine.UI.Text _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
