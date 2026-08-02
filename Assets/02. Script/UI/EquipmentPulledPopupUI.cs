using Core;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// EquipmentPulledEvent를 구독해 무기 가챠 결과를 알림 팝업으로 보여준다.
    /// SoldierPulledPopupUI와 동일한 패턴.
    /// </summary>
    public sealed class EquipmentPulledPopupUI : MonoBehaviour
    {
        [SerializeField]
        private GameObject popupRoot;

        [SerializeField]
        private Text messageText;

        [SerializeField]
        private Button confirmButton;

        private void Awake()
        {
            popupRoot.SetActive(false);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<EquipmentPulledEvent>(OnEquipmentPulled);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<EquipmentPulledEvent>(OnEquipmentPulled);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnEquipmentPulled(EquipmentPulledEvent evt)
        {
            messageText.text = $"뽑기 성공!\n{evt.Pulled.ItemName}";
            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
