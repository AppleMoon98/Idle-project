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
    /// stack이 비어있으면(0개 보유) 회색으로 비활성 표시만 하고 탭을 막는다 — SoldierRosterPanelUI가
    /// 카탈로그 전체를 순회하며 아직 안 뽑은 병사도 "미보유" 슬롯으로 함께 보여줄 때 쓴다.
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
        private GameObject selectedHighlight;

        [SerializeField]
        private Color baseBackgroundColor = Color.white;

        [SerializeField]
        private float gradeTintBlend = 0.35f;

        [SerializeField]
        private Color ownedLabelColor = new(0.95f, 0.9f, 0.78f, 1f);

        [SerializeField]
        private Color unownedLabelColor = new(0.55f, 0.55f, 0.55f, 0.7f);

        [SerializeField]
        private Color unownedIconTint = new(0.5f, 0.5f, 0.5f, 0.5f);

        [SerializeField]
        private Color unownedBackgroundTint = new(0.15f, 0.15f, 0.15f, 1f);

        [SerializeField]
        private float unownedBackgroundBlend = 0.6f;

        /// <summary>
        /// 슬롯 데이터를 채운다. definition은 이 슬롯이 나타낼 병사 원형, stack은 그중 실제로
        /// 보유 중인 유닛들(0개 이상)이다. onSlotTapped는 슬롯을 탭했을 때 이 스택을 그대로
        /// 넘겨달라는 요청 콜백이며, stack이 비어있으면 슬롯 자체가 비활성화돼 호출되지 않는다.
        /// </summary>
        public void Initialize(SoldierSO definition, IReadOnlyList<OwnedSoldier> stack, Action<IReadOnlyList<OwnedSoldier>> onSlotTapped)
        {
            bool isOwned = stack.Count > 0;

            EquipmentGradeSO grade = definition.Grade;

            label.text = definition.DisplayName;
            label.color = isOwned ? ownedLabelColor : unownedLabelColor;

            if (iconImage != null)
            {
                iconImage.sprite = definition.Icon;
                iconImage.enabled = definition.Icon != null;
                Color gradeIconTint = grade != null ? grade.TintColor : Color.white;
                iconImage.color = isOwned ? gradeIconTint : unownedIconTint;
            }

            if (background != null)
            {
                Color gradeBackground = EquipmentRowUI.ComputeGradeBackground(baseBackgroundColor, grade, gradeTintBlend);
                background.color = isOwned ? gradeBackground : Color.Lerp(gradeBackground, unownedBackgroundTint, unownedBackgroundBlend);
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

            slotButton.interactable = isOwned;
            slotButton.onClick.AddListener(() => onSlotTapped?.Invoke(stack));

            SetSelected(false);
        }

        /// <summary>
        /// 선택 테두리를 켜고 끈다 — 이 슬롯 자체는 "선택" 개념을 모르는 채 요청받은 대로 표시만
        /// 한다(SoldierDeploymentPanelUI가 "부대 편성" 팝업의 배치 대상 선택 흐름에서 호출한다).
        /// 기본 로스터 패널(SoldierRosterPanelUI)은 이 메서드를 호출하지 않으므로 항상 꺼져 있다.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (selectedHighlight != null)
            {
                selectedHighlight.SetActive(selected);
            }
        }
    }
}
