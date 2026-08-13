using System.Collections.Generic;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 같은 SoldierSO(등급+병종)로 묶인 로스터 슬롯(2개 이상)을 탭했을 때, 그 안의 개별 유닛을
    /// 고르는 팝업. SoldierDeploymentPickerPopupUI와 같은 "이름 목록 → 하나 선택" 형태로,
    /// SoldierPickerRowUI를 그대로 재사용한다. 하나를 고르면 SoldierRosterSlotActionPopupUI를
    /// 그 유닛으로 열고 자신은 닫는다.
    /// </summary>
    public sealed class SoldierRosterStackPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierPickerRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        [SerializeField]
        private SoldierRosterSlotActionPopupUI actionPopup;

        private readonly List<SoldierPickerRowUI> _spawnedRows = new();

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// stack 안의 유닛들을 목록으로 채워 팝업을 연다.
        /// </summary>
        public void Open(IReadOnlyList<OwnedSoldier> stack)
        {
            foreach (SoldierPickerRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            foreach (OwnedSoldier owned in stack)
            {
                SoldierPickerRowUI row = Instantiate(rowPrefab, rowContainer);
                Color? iconColor = owned.Definition.Grade != null ? owned.Definition.Grade.TintColor : null;
                row.Initialize($"{owned.Definition.DisplayName} (#{owned.InstanceId})", () => OnPicked(owned), owned.Definition.Icon, iconColor);

                _spawnedRows.Add(row);
            }

            popupRoot.SetActive(true);
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void OnPicked(OwnedSoldier owned)
        {
            actionPopup.Open(owned);
            Close();
        }
    }
}
