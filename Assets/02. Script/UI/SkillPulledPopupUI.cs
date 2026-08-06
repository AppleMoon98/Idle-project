using System.Collections.Generic;
using System.Text;
using Core;
using Skill;
using Skill.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SkillPulledEvent를 구독해 스킬 가챠 결과를 알림 팝업으로 보여준다. SoldierPulledPopupUI와
    /// 동일한 패턴 — 다다뽑기 결과는 스킬별로 이번에 오른 레벨 수(x N)로 묶어 요약한다.
    /// </summary>
    public sealed class SkillPulledPopupUI : MonoBehaviour
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
            GameBootstrapper.Events?.Subscribe<SkillPulledEvent>(OnSkillPulled);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<SkillPulledEvent>(OnSkillPulled);
            confirmButton.onClick.RemoveListener(Close);
        }

        private void OnSkillPulled(SkillPulledEvent evt)
        {
            var counts = new Dictionary<string, int>();

            foreach (SkillSO skill in evt.Pulled)
            {
                string name = skill.DisplayName;
                counts.TryGetValue(name, out int current);
                counts[name] = current + 1;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"뽑기 성공! ({evt.Pulled.Count}개)");

            foreach (KeyValuePair<string, int> pair in counts)
            {
                sb.AppendLine($"{pair.Key} Lv+{pair.Value}");
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
