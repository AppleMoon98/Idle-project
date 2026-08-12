using System;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 부대 편성 팝업 상단 4x5 배치 그리드의 칸 하나. 배정된 유닛이 있으면 아이콘을 보여주고,
    /// 없으면 빈 칸으로, 현재 랭크로 아직 열리지 않았으면 잠금 상태로 표시한다. 탭하면 자기
    /// slotIndex를 그대로 콜백에 넘긴다 — 그 탭이 "배치를 확정할지"는 이 컴포넌트가 모르는
    /// 일이고 SquadDeploymentSlotGridUI가 결정한다(선택된 유닛이 있는지는 이 슬롯이 알 수 없다).
    /// </summary>
    public sealed class SquadDeploymentSlotUI : MonoBehaviour
    {
        [SerializeField]
        private Image background;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Button slotButton;

        [SerializeField]
        private Color emptyColor = new(0.35f, 0.35f, 0.4f, 0.6f);

        [SerializeField]
        private Color filledColor = new(0.55f, 0.5f, 0.3f, 0.85f);

        [SerializeField]
        private Color lockedColor = new(0.15f, 0.15f, 0.15f, 0.6f);

        private int _slotIndex;

        /// <summary>
        /// 슬롯 데이터를 채운다. occupant가 null이면 빈 칸으로 표시한다. onTapped는 잠기지
        /// 않은 슬롯을 탭했을 때 이 슬롯의 slotIndex를 그대로 넘겨달라는 요청 콜백이다.
        /// </summary>
        public void Initialize(int slotIndex, bool isLocked, OwnedSoldier occupant, Action<int> onTapped)
        {
            _slotIndex = slotIndex;

            if (background != null)
            {
                background.color = isLocked ? lockedColor : occupant != null ? filledColor : emptyColor;
            }

            if (iconImage != null)
            {
                Sprite icon = occupant?.Definition.Icon;
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            slotButton.interactable = !isLocked;
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => onTapped?.Invoke(_slotIndex));
        }
    }
}
