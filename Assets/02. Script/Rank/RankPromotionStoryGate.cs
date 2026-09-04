using Core;
using Rank.Events;
using UI;

namespace Rank
{
    /// <summary>
    /// RankChangedEvent(실제 승급, IsRestore == false)를 가로채 승급 스토리(RankSO.PromotionStory)를
    /// 먼저 재생하고, 끝나면(또는 그 랭크에 스토리가 없으면 즉시) RankPromotionAnnouncementReadyEvent를
    /// 발행한다. UI.RankUpPopupUI는 RankChangedEvent가 아니라 이 이벤트를 구독하므로 "스토리 먼저,
    /// 알림 팝업 나중"이라는 순서가 두 UI 컴포넌트가 서로 직접 참조하지 않고도 EventBus만으로
    /// 보장된다. Loot.LootDropper/Combat.DamageNumberSpawner와 같은 "EventBus + 필요한 의존성만
    /// 받는 plain C# 클래스, GameBootstrapper가 직접 생성/Dispose" 관례를 그대로 따른다.
    /// </summary>
    public sealed class RankPromotionStoryGate
    {
        private readonly EventBus _events;
        private readonly StoryPopupUI _storyPopupUI;

        public RankPromotionStoryGate(EventBus events, StoryPopupUI storyPopupUI)
        {
            _events = events;
            _storyPopupUI = storyPopupUI;

            _events.Subscribe<RankChangedEvent>(OnRankChanged);
        }

        /// <summary>
        /// 이벤트 구독을 해제한다. 게임 종료 시 반드시 호출해야 한다.
        /// </summary>
        public void Dispose()
        {
            _events.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            if (evt.IsRestore)
            {
                return;
            }

            if (_storyPopupUI != null && evt.NewRank.PromotionStory != null)
            {
                RankSO newRank = evt.NewRank;
                int newRankIndex = evt.NewRankIndex;

                _storyPopupUI.Play(newRank.PromotionStory, () =>
                    _events.Publish(new RankPromotionAnnouncementReadyEvent(newRank, newRankIndex)));
                return;
            }

            _events.Publish(new RankPromotionAnnouncementReadyEvent(evt.NewRank, evt.NewRankIndex));
        }
    }
}
