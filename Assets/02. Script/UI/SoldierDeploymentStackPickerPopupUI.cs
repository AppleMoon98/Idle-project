using System;
using System.Collections.Generic;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SoldierDeploymentPickerPopupUI의 스택 슬롯(2개 이상)을 탭했을 때, 그 안의 개별 유닛을
    /// 고르는 하위 팝업. SoldierRosterStackPopupUI와 같은 형태지만, 고른 뒤 여는 다음 동작이
    /// 문맥마다 달라서(로스터는 액션 팝업, 여기는 배치 확정) onPicked 콜백을 호출자가
    /// 자유롭게 지정하도록 범용으로 만들었다.
    /// </summary>
    public sealed class SoldierDeploymentStackPickerPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Transform rowContainer;

        [SerializeField]
        private SoldierPickerRowUI rowPrefab;

        [SerializeField]
        private Button closeButton;

        private readonly List<SoldierPickerRowUI> _spawnedRows = new();
        private Action<OwnedSoldier> _onPicked;

        private void Awake()
        {
            popupRoot.SetActive(false);
            closeButton.onClick.AddListener(Close);
        }

        /// <summary>
        /// stack 안의 유닛들을 목록으로 채워 팝업을 연다. onPicked는 하나를 골랐을 때 호출된다
        /// (호출 직후 이 팝업은 스스로 닫힌다 — 호출자가 부모 팝업까지 닫을지는 onPicked 안에서 결정).
        /// </summary>
        public void Open(IReadOnlyList<OwnedSoldier> stack, Action<OwnedSoldier> onPicked)
        {
            _onPicked = onPicked;

            foreach (SoldierPickerRowUI row in _spawnedRows)
            {
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();

            foreach (OwnedSoldier owned in stack)
            {
                SoldierPickerRowUI row = Instantiate(rowPrefab, rowContainer);
                row.Initialize($"{owned.Definition.DisplayName} (#{owned.InstanceId})", () => OnRowPicked(owned), owned.Definition.Icon);

                _spawnedRows.Add(row);
            }

            popupRoot.SetActive(true);
        }

        public void Close()
        {
            popupRoot.SetActive(false);
        }

        private void OnRowPicked(OwnedSoldier owned)
        {
            _onPicked?.Invoke(owned);
            Close();
        }
    }
}
