using System.Collections.Generic;
using System.Text;
using Core;
using Equipment;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// EquipmentPulledEvent를 구독해 무기 가챠 결과를 알림 팝업으로 보여준다.
    /// SoldierPulledPopupUI와 동일한 패턴 — 다다뽑기 결과는 종류별 개수로 묶어 요약한다.
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
            var counts = new Dictionary<string, int>();

            foreach (EquipmentSO item in evt.Pulled)
            {
                counts.TryGetValue(item.ItemName, out int current);
                counts[item.ItemName] = current + 1;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"뽑기 성공! ({evt.Pulled.Count}개)");

            foreach (KeyValuePair<string, int> pair in counts)
            {
                sb.AppendLine($"{pair.Key} x{pair.Value}");
            }

            messageText.text = sb.ToString();
            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
