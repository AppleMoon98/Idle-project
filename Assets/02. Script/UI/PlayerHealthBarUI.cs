using Character;
using Character.Events;
using Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 화면 좌측 상단, 랭크 텍스트 바로 아래에 상시 표시하는 플레이어 HP바. 채움 이미지 위에
    /// "현재/최대" 텍스트를 겹쳐 보여준다. GoldDisplayUI와 동일한 이유로 최초 진입 시 한 번
    /// Health.Current/CharacterStatsProvider.Stats.MaxHealth를 직접 읽고, 이후로는
    /// CharacterHealthChangedEvent만 구독한다(이벤트는 변화가 있을 때만 발행되므로).
    /// </summary>
    public sealed class PlayerHealthBarUI : MonoBehaviour
    {
        [SerializeField]
        private Health playerHealth;

        [SerializeField]
        private Image fillImage;

        [SerializeField]
        private Text hpText;

        private CharacterStatsProvider _playerStats;

        private void Awake()
        {
            if (playerHealth != null)
            {
                _playerStats = playerHealth.GetComponent<CharacterStatsProvider>();
            }
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<CharacterHealthChangedEvent>(OnHealthChanged);

            if (playerHealth != null && _playerStats != null)
            {
                Refresh(playerHealth.Current, _playerStats.Stats.MaxHealth);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<CharacterHealthChangedEvent>(OnHealthChanged);
        }

        private void OnHealthChanged(CharacterHealthChangedEvent evt)
        {
            if (playerHealth == null || evt.Character != playerHealth.gameObject)
            {
                return;
            }

            Refresh(evt.Current, evt.Max);
        }

        private void Refresh(float current, float max)
        {
            fillImage.fillAmount = max > 0f ? current / max : 0f;
            hpText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
        }
    }
}
