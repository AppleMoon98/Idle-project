using System.Collections.Generic;
using Core;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 현재 열려있는 IDismissible 화면을 최근에 연 순서(LIFO)로 추적한다(GitHub 이슈 #25). 각 팝업이
    /// 자신의 Open()/Close()에서 Register/Unregister를 직접 호출해 스택을 쌓고 내린다 - 이 프로젝트의
    /// popupRoot 팝업 대다수가 popupRoot를 소유한 스크립트 자신은 항상 활성 상태로 유지한 채(section
    /// AY의 "component must not live on the SAME GameObject it calls SetActive(false) on to hide
    /// itself" 관례) 자식 GameObject만 토글하므로, OnEnable/OnDisable로는 열고 닫는 시점을 감지할 수
    /// 없다 - Open()/Close() 메서드 안에서 명시적으로 호출하는 것이 유일한 신뢰 가능한 지점이다.
    ///
    /// TryDismissTop()이 스택 맨 위부터 훑으면서 Unity가 파괴한 항목(가짜 null)은 그 자리에서 조용히
    /// 제거하고 계속 내려간다 - Close() 호출 없이 GameObject가 통째로 파괴되는 경우(이 프로젝트는 씬이
    /// 하나뿐이라 실제로는 거의 없지만) 스택에 파괴된 참조가 영원히 남는 것을 막는 안전망이다.
    /// </summary>
    public sealed class BackNavigationService : IManager, IService
    {
        private readonly List<IDismissible> _stack = new();

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _stack.Clear();
        }

        /// <summary>
        /// 이미 등록돼 있으면 중복 추가하지 않는다 - Open()이 이미 열린 상태에서 실수로 다시
        /// 호출돼도 스택이 어긋나지 않는다.
        /// </summary>
        public void Register(IDismissible dismissible)
        {
            if (dismissible == null || _stack.Contains(dismissible))
            {
                return;
            }

            _stack.Add(dismissible);
        }

        public void Unregister(IDismissible dismissible)
        {
            if (dismissible == null)
            {
                return;
            }

            _stack.Remove(dismissible);
        }

        /// <summary>
        /// 스택이 비어있으면(닫을 팝업이 없으면) false를 반환한다 - 호출부(BackInputRouter)는 그
        /// 다음 우선순위(던전/승급전 이탈, 최종적으로 종료 확인)로 넘어간다.
        /// </summary>
        public bool TryDismissTop()
        {
            while (_stack.Count > 0)
            {
                int topIndex = _stack.Count - 1;
                IDismissible top = _stack[topIndex];

                if (top is Object unityObject && unityObject == null)
                {
                    _stack.RemoveAt(topIndex);
                    continue;
                }

                if (top.TryDismiss())
                {
                    _stack.Remove(top);
                    return true;
                }

                // 최상위 항목이 스스로 닫을 게 없다고 응답했다 - 그 아래는 어차피 화면상 가려져
                // 있으므로 더 내려가지 않고 여기서 멈춘다.
                return false;
            }

            return false;
        }
    }
}
