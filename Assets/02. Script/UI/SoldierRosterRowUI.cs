using System;
using System.Collections.Generic;
using Equipment;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 로스터 패널의 병사 한 슬롯(그리드 칸 하나)을 표시/제어한다. SoldierRosterPanelUI가 같은
    /// SoldierSO(등급+병종)를 가진 유닛들을 하나의 스택으로 묶어 이 프리팹 하나로 표시한다 —
    /// 장비 인벤토리의 "같은 라인은 개수로 쌓인다" 관례를 로스터에도 적용한 것. 스택 개수가
    /// 2개 이상이면 countBadge에 "×N"을 표시한다. 슬롯 전체가 하나의 버튼이라 탭하면
    /// onSlotTapped로 이 스택 전체를 그대로 넘긴다 — 스택이 1개면 SoldierRosterPanelUI가 바로
    /// 액션 팝업을 열고, 여러 개면 먼저 개별 유닛을 고르는 SoldierRosterStackPopupUI를 연다.
    /// </summary>
    public sealed class SoldierRosterRowUI : MonoBehaviour
    {
        [SerializeField]
        private Image background;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Text countBadge;

        [SerializeField]
        private Button slotButton;

        [SerializeField]
        private Color baseBackgroundColor = Color.white;

        [SerializeField]
        private float gradeTintBlend = 0.35f;

        /// <summary>
        /// 슬롯 데이터를 채운다. stack은 같은 SoldierSO를 가진 유닛 전체(1개 이상)이고,
        /// onSlotTapped는 슬롯을 탭했을 때 이 스택을 그대로 넘겨달라는 요청 콜백이다.
        /// </summary>
        public void Initialize(IReadOnlyList<OwnedSoldier> stack, Action<IReadOnlyList<OwnedSoldier>> onSlotTapped)
        {
            SoldierSO definition = stack[0].Definition;

            label.text = definition.DisplayName;

            if (iconImage != null)
            {
                iconImage.sprite = definition.Icon;
                iconImage.enabled = definition.Icon != null;
            }

            if (background != null)
            {
                EquipmentGradeSO grade = definition.Grade;
                background.color = EquipmentRowUI.ComputeGradeBackground(baseBackgroundColor, grade, gradeTintBlend);
            }

            if (countBadge != null)
            {
                bool showBadge = stack.Count > 1;
                countBadge.gameObject.SetActive(showBadge);

                if (showBadge)
                {
                    countBadge.text = $"×{stack.Count}";
                }
            }

            slotButton.onClick.AddListener(() => onSlotTapped?.Invoke(stack));
        }
    }
}
