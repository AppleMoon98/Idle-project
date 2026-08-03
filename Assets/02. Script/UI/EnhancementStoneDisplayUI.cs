using Core;
using Equipment;
using Equipment.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 보유 강화석 개수를 텍스트로 표시한다. GoldDisplayUI와 동일한 패턴 — 최초 진입 시
    /// EnhancementStoneService에서 초기값을 읽고, 이후에는 EnhancementStoneChangedEvent 구독만으로 갱신한다.
    /// </summary>
    public sealed class EnhancementStoneDisplayUI : MonoBehaviour
    {
        [SerializeField]
        private Text stoneCountText;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<EnhancementStoneChangedEvent>(OnStoneChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out EnhancementStoneService stoneService))
            {
                SetStoneText(stoneService.CurrentStones);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<EnhancementStoneChangedEvent>(OnStoneChanged);
        }

        private void OnStoneChanged(EnhancementStoneChangedEvent evt)
        {
            SetStoneText(evt.CurrentStones);
        }

        private void SetStoneText(int amount)
        {
            stoneCountText.text = amount.ToString();
        }
    }
}
