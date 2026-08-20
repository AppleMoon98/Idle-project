using Core;
using Dungeon.Events;

namespace Dungeon
{
    /// <summary>
    /// 보유 보스 토벌 증표를 관리하는 서비스. Gacha.SoldierTicketService/SkillScrollService와 동일한
    /// 형태. 보스 던전 클리어 보상으로 지급되며, 이 증표를 소비하는 시스템은 아직 없다(콘텐츠
    /// 갭 — 강화석 던전이 처음 만들어졌을 때 소비 쪽만 먼저 있었던 것과 반대 방향).
    /// </summary>
    public sealed class BossTokenService : IManager, IService
    {
        private readonly EventBus _events;
        private int _currentTokens;

        /// <summary>
        /// 현재 보유 보스 토벌 증표.
        /// </summary>
        public int CurrentTokens => _currentTokens;

        /// <summary>
        /// initialTokens: 저장된 증표로 시작하기 위한 초기값(SaveService.Load() 결과). 기본 0.
        /// </summary>
        public BossTokenService(EventBus events, int initialTokens = 0)
        {
            _events = events;
            _currentTokens = initialTokens;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 증표를 더하고 변경 이벤트를 발행한다.
        /// </summary>
        public void AddTokens(int amount)
        {
            _currentTokens += amount;
            _events.Publish(new BossTokenChangedEvent(_currentTokens));
        }

        /// <summary>
        /// 증표 소비를 시도한다. 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendTokens(int amount)
        {
            if (amount > _currentTokens)
            {
                return false;
            }

            _currentTokens -= amount;
            _events.Publish(new BossTokenChangedEvent(_currentTokens));
            return true;
        }
    }
}
