using Core;
using Rank.Events;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// DungeonPopup의 BossDungeonRow — RankChangedEvent를 구독해 session.HasAnyBossAvailable로
    /// 입장 버튼의 활성/라벨을 전환하고("병사 랭크 필요" ↔ "입장"), 클릭 시
    /// BossDungeonSelectPopupUI를 연다. 별도로 "병사 랭크"를 하드코딩하지 않는다 — 실제로 승급전
    /// 보스를 가진 랭크에 도달했는지(Dungeon.BossDungeonSessionController.HasAnyBossAvailable)만 본다.
    /// </summary>
    public sealed class BossDungeonRowUI : MonoBehaviour
    {
        [SerializeField]
        private Button enterButton;

        [SerializeField]
        private Text enterButtonLabel;

        [SerializeField]
        private Dungeon.BossDungeonSessionController session;

        [SerializeField]
        private BossDungeonSelectPopupUI selectPopup;

        private void Awake()
        {
            enterButton.onClick.AddListener(OnEnterClicked);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool unlocked = session != null && session.HasAnyBossAvailable;
            enterButton.interactable = unlocked;
            enterButtonLabel.text = unlocked ? "입장" : "병사 랭크 필요";
        }

        private void OnEnterClicked()
        {
            selectPopup.Open();
        }
    }
}
