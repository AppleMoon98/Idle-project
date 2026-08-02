using System;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 로스터 패널의 병사 한 줄(행)을 표시/제어한다. SoldierRosterPanelUI가 보유 병사 수만큼
    /// 이 컴포넌트가 붙은 프리팹을 Instantiate하고 Initialize로 데이터를 채운다.
    /// </summary>
    public sealed class SoldierRosterRowUI : MonoBehaviour
    {
        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Text label;

        [SerializeField]
        private Button equipmentButton;

        [SerializeField]
        private Button behaviorButton;

        /// <summary>
        /// 행 데이터를 채운다. onEquipmentRequested/onBehaviorRequested는 각각 "장비"/"행동" 버튼을
        /// 눌렀을 때 이 유닛의 InstanceId로 해당 팝업을 열어달라는 요청 콜백이다.
        /// </summary>
        public void Initialize(OwnedSoldier owned, Action<int> onEquipmentRequested, Action<int> onBehaviorRequested)
        {
            string profileName = owned.BehaviorProfile != null ? owned.BehaviorProfile.DisplayName : "미배정(교전)";
            label.text = $"{owned.Definition.DisplayName} (#{owned.InstanceId}) - {profileName}";

            if (iconImage != null)
            {
                iconImage.sprite = owned.Definition.Icon;
                iconImage.enabled = owned.Definition.Icon != null;
            }

            equipmentButton.onClick.AddListener(() => onEquipmentRequested?.Invoke(owned.InstanceId));
            behaviorButton.onClick.AddListener(() => onBehaviorRequested?.Invoke(owned.InstanceId));
        }
    }
}
