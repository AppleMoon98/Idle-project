using Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 하단 탭 버튼과 그에 대응하는 패널을 토글/전환하는 범용 네비게이션 셸.
    /// 각 패널이 무엇을 표시하는지는 전혀 모른다.
    /// </summary>
    public sealed class BottomMenuUI : MonoBehaviour, ITickable
    {
        [SerializeField]
        private Button[] tabButtons;

        [SerializeField]
        private GameObject[] panels;

        [SerializeField]
        private bool selectFirstTabByDefault;

        [SerializeField]
        private bool allowToggleClose = true;

        [SerializeField]
        private bool highlightSelectedTab;

        [SerializeField]
        private Color selectedTabColor = new(1f, 0.84f, 0f, 1f);

        /// <summary>
        /// 켜두면, 패널이 열려있는 탭 버튼은 Button.spriteState.pressedSprite를 계속 표시한다(눌린
        /// 채로 고정) - Button.Transition이 SpriteSwap이고 pressedSprite가 지정돼 있어야 의미가
        /// 있다. highlightSelectedTab(색 강조)과 독립적으로 동작하며 함께 켜도 된다. 기본값
        /// false라 기존 BottomMenuUI 사용처는 전혀 영향받지 않는다.
        /// </summary>
        [SerializeField]
        private bool keepPressedSpriteWhileOpen;

        /// <summary>
        /// keepPressedSpriteWhileOpen과 함께 켜진 탭의 자식 "Label"도 눌린 것처럼 아래로 붙여
        /// 보이도록, 그 순간만 Top/Bottom 인셋을 이 값으로 바꾼다(Top=labelPressedTopOffset,
        /// Bottom=0 고정) - 평상시 인셋은 씬에 저장된 값을 Awake에서 그대로 캐싱해뒀다가 되돌린다.
        /// </summary>
        [SerializeField]
        private float labelPressedTopOffset = 16f;

        private Color[] _normalTabColors;
        private RectTransform[] _tabLabels;
        private Vector2[] _tabLabelNormalOffsetMin;
        private Vector2[] _tabLabelNormalOffsetMax;

        private void Awake()
        {
            if (highlightSelectedTab)
            {
                _normalTabColors = new Color[tabButtons.Length];
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    if (tabButtons[i].targetGraphic != null)
                    {
                        _normalTabColors[i] = tabButtons[i].targetGraphic.color;
                    }
                }
            }

            if (keepPressedSpriteWhileOpen)
            {
                _tabLabels = new RectTransform[tabButtons.Length];
                _tabLabelNormalOffsetMin = new Vector2[tabButtons.Length];
                _tabLabelNormalOffsetMax = new Vector2[tabButtons.Length];

                for (int i = 0; i < tabButtons.Length; i++)
                {
                    RectTransform label = tabButtons[i].transform.Find("Label") as RectTransform;
                    _tabLabels[i] = label;

                    if (label != null)
                    {
                        _tabLabelNormalOffsetMin[i] = label.offsetMin;
                        _tabLabelNormalOffsetMax[i] = label.offsetMax;
                    }

                    // 닫혀있는 탭을 누르고 있는 도중(아직 클릭 미완료)에는 Tick이 손대지 않으므로
                    // (Tick의 doc 참고), Unity 자체 SpriteSwap이 Pressed 스프라이트를 보여주는
                    // 바로 그 순간(PointerDown)에 맞춰 Label도 함께 눌린 위치로 옮겨야 스프라이트
                    // 모션과 어긋나지 않는다(실사용 중 "Label이 버튼 모션을 못 따라온다"는 제보의
                    // 원인) - PointerUp/Exit(Unity가 Pressed에서 벗어나는 시점)에는 되돌린다.
                    // 클릭이 실제로 열림으로 이어지면 그 직후 RefreshTabHighlights가 다시 눌림으로
                    // 덮어써서 최종 상태는 항상 올바르게 정리된다(스프라이트가 이미 겪는 것과 동일한
                    // "일시적으로 풀렸다 즉시 재적용"의 무해한 흐름).
                    var relay = tabButtons[i].gameObject.AddComponent<TabPressRelay>();
                    relay.Owner = this;
                    relay.Index = i;
                }
            }

            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
            }

            foreach (GameObject panel in panels)
            {
                panel.SetActive(false);
            }

            if (selectFirstTabByDefault && panels.Length > 0)
            {
                panels[0].SetActive(true);
            }

            RefreshTabHighlights();
        }

        private void OnEnable()
        {
            // keepPressedSpriteWhileOpen이 꺼진(기본값) 대다수 BottomMenuUI 인스턴스는 등록조차
            // 하지 않아 매 프레임 비용이 전혀 없다.
            if (keepPressedSpriteWhileOpen)
            {
                TickerRegistration.Register(this);
            }
        }

        private void OnDisable()
        {
            if (keepPressedSpriteWhileOpen)
            {
                TickerRegistration.Unregister(this);
            }
        }

        /// <summary>
        /// Button.Transition=SpriteSwap은 Unity 자체가 OnPointerEnter/Exit마다 독립적으로
        /// overrideSprite를 되돌린다(Highlighted/Normal 상태 전이) - 열려있는 탭 버튼 위로 포인터가
        /// 나갔다 들어왔다 하는 것만으로 "눌린 채 고정" 표시가 매번 풀려버린다(실사용 중 발견).
        /// RefreshTabHighlights의 클릭 시점 반영만으로는 이 되돌림을 못 막으므로, 매 틱 다시
        /// 덮어써서 항상 "패널이 열려있으면 눌림"을 유지한다.
        ///
        /// 단, 여기서는 "열려있는" 탭만 건드린다 - 닫혀있는 탭까지 매 프레임 overrideSprite=null로
        /// 강제하면, 사용자가 그 탭을 막 누르고 있는 도중(아직 클릭이 완료돼 패널이 열리기 전)에
        /// Unity 자체가 보여주는 Pressed 표시를 다음 프레임에 곧바로 지워버려 "눌러도 안 눌리는
        /// 것처럼" 보이는 문제가 있었다(실사용 중 발견 - "처음 클릭했을 때 눌린 상태로 전환되지
        /// 않는다"는 제보의 원인). 닫힌 탭의 순간적인 눌림/호버 표시는 Unity 자체 전이에 그대로
        /// 맡기고, 실제로 열림/닫힘이 바뀌는 순간의 전체 재조정은 RefreshTabHighlights(클릭 등
        /// 상태 변화 시점에만 호출)가 담당한다.
        /// </summary>
        void ITickable.Tick(float deltaTime)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (panels[i].activeSelf)
                {
                    ApplyPressedVisualForTab(i, true);
                }
            }
        }

        // 어느 탭이 열려있는지를 별도 인덱스로 캐싱하지 않고 panels[].activeSelf를 그때그때
        // 직접 읽는다 - 패널이 BottomMenuUI를 거치지 않고 외부에서 직접 SetActive(false)되는
        // 경우(예: EquipmentSlotPopupUI.Close()가 팝업 상단 닫기 버튼에서 equippedSlotBar를
        // 직접 끄는 것)가 있어, 캐싱된 인덱스만 믿으면 실제 상태와 어긋나 다음 탭 클릭이
        // "이미 열려있다고 착각하고 닫기"로 처리돼 한 번 더 눌러야 열리는 문제가 생긴다.
        private void OnTabClicked(int index)
        {
            if (panels[index].activeSelf)
            {
                if (!allowToggleClose)
                {
                    return;
                }

                panels[index].SetActive(false);
                RefreshTabHighlights();
                return;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                if (i != index)
                {
                    panels[i].SetActive(false);
                }
            }

            panels[index].SetActive(true);
            RefreshTabHighlights();
        }

        /// <summary>
        /// 현재 열려있는 패널에 대응하는 탭 버튼만 selectedTabColor로(highlightSelectedTab) 또는
        /// pressedSprite 고정 표시로(keepPressedSpriteWhileOpen), 나머지는 각자의 원래 상태로
        /// 되돌린다. 둘 다 꺼져 있으면(기본값) 아무 것도 하지 않아 기존 BottomMenuUI 사용처(하단
        /// 메인 탭 등)는 전혀 영향받지 않는다.
        /// </summary>
        private void RefreshTabHighlights()
        {
            if (!highlightSelectedTab && !keepPressedSpriteWhileOpen)
            {
                return;
            }

            if (highlightSelectedTab)
            {
                for (int i = 0; i < tabButtons.Length; i++)
                {
                    if (tabButtons[i].targetGraphic != null)
                    {
                        tabButtons[i].targetGraphic.color = panels[i].activeSelf ? selectedTabColor : _normalTabColors[i];
                    }
                }
            }

            if (keepPressedSpriteWhileOpen)
            {
                ApplyPressedVisuals();
            }
        }

        /// <summary>
        /// keepPressedSpriteWhileOpen 탭 전체(열림/닫힘 무관)의 눌린 표시(스프라이트 + Label
        /// 인셋)를 패널 상태에 맞게 전부 다시 맞춘다 - 클릭 등 실제 상태 변화가 일어난 시점에만
        /// 호출된다(RefreshTabHighlights). 방금 닫힌 탭을 원래대로 되돌리는 것도 이 전체 재조정이
        /// 담당한다. 매 프레임 도는 Tick은 이와 달리 "열려있는" 탭만 건드린다(Tick의 doc 참고).
        /// </summary>
        private void ApplyPressedVisuals()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                ApplyPressedVisualForTab(i, panels[i].activeSelf);
            }
        }

        private void ApplyPressedVisualForTab(int index, bool isOpen)
        {
            if (tabButtons[index].targetGraphic is Image image)
            {
                image.overrideSprite = isOpen ? tabButtons[index].spriteState.pressedSprite : null;
            }

            RectTransform label = _tabLabels[index];

            if (label == null)
            {
                return;
            }

            SetLabelOffset(index, isOpen);
        }

        private void SetLabelOffset(int index, bool pressed)
        {
            RectTransform label = _tabLabels[index];

            if (label == null)
            {
                return;
            }

            if (pressed)
            {
                label.offsetMin = new Vector2(_tabLabelNormalOffsetMin[index].x, 0f);
                label.offsetMax = new Vector2(_tabLabelNormalOffsetMax[index].x, -labelPressedTopOffset);
            }
            else
            {
                label.offsetMin = _tabLabelNormalOffsetMin[index];
                label.offsetMax = _tabLabelNormalOffsetMax[index];
            }
        }

        /// <summary>
        /// 닫혀있는 탭이 눌리고 있는 동안(PointerDown~PointerUp/Exit), Unity 자체 SpriteSwap이
        /// 보여주는 Pressed 스프라이트와 같은 타이밍으로 Label도 함께 눌린 위치로 옮긴다 -
        /// keepPressedSpriteWhileOpen이 켜졌을 때만 각 탭 버튼에 부착된다(Awake 참고).
        /// </summary>
        private sealed class TabPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public BottomMenuUI Owner;
            public int Index;

            public void OnPointerDown(PointerEventData eventData)
            {
                Owner.SetLabelOffset(Index, true);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                Owner.SetLabelOffset(Index, false);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                Owner.SetLabelOffset(Index, false);
            }
        }
    }
}
