using Core;
using Gacha.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// SoldierPulledEvent를 구독해 가챠 뽑기 결과를 알림 팝업으로 보여준다. RankUpPopupUI와 동일한 패턴.
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
            messageText.text = $"뽑기 성공!\n{evt.Pulled.Definition.DisplayName} (#{evt.Pulled.InstanceId})";
            popupRoot.SetActive(true);
        }

        private void Close()
        {
            popupRoot.SetActive(false);
        }
    }
}
