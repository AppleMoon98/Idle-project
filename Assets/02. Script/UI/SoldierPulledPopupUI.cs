using System.Collections.Generic;
using System.Text;
using Core;
using Gacha.Events;
using Soldier;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SoldierPulledEvent를 구독해 가챠 뽑기 결과를 알림 팝업으로 보여준다. RankUpPopupUI와 동일한 패턴.
    /// 다다뽑기(10/30개 등)도 같은 이벤트로 오므로, 종류별 개수를 묶어 요약해서 보여준다.
    /// </summary>
    public sealed class SoldierPulledPopupUI : MonoBehaviour
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
            GameBootstrapper.Events?.Subscribe<SoldierPulledEvent>(OnSoldierPulled);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SoldierPulledEvent>(OnSoldierPulled);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnSoldierPulled(SoldierPulledEvent evt)
        {
            var counts = new Dictionary<string, int>();

            foreach (OwnedSoldier owned in evt.Pulled)
            {
                string name = owned.Definition.DisplayName;
                counts.TryGetValue(name, out int current);
                counts[name] = current + 1;
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
