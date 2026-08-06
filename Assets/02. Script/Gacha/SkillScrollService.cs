using Core;
using Gacha.Events;

namespace Gacha
{
    /// <summary>
    /// 보유 스킬 주문서를 관리하는 서비스. CurrencyService(골드)/SoldierTicketService(병사 소환권)와
    /// 동일한 형태로, 스킬 던전 클리어 보상으로 지급되고 스킬 뽑기에서 소비된다.
    /// </summary>
    public sealed class SkillScrollService : IManager, IService
    {
        private readonly EventBus _events;
        private int _currentScrolls;

        /// <summary>
        /// 현재 보유 스킬 주문서.
        /// </summary>
        public int CurrentScrolls => _currentScrolls;

        /// <summary>
        /// initialScrolls: 저장된 주문서로 시작하기 위한 초기값(SaveService.Load() 결과). 기본 0.
        /// </summary>
        public SkillScrollService(EventBus events, int initialScrolls = 0)
        {
            _events = events;
            _currentScrolls = initialScrolls;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 주문서를 더하고 변경 이벤트를 발행한다.
        /// </summary>
        public void AddScrolls(int amount)
        {
            _currentScrolls += amount;
            _events.Publish(new SkillScrollChangedEvent(_currentScrolls));
        }

        /// <summary>
        /// 주문서 소비를 시도한다. 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendScrolls(int amount)
        {
            if (amount > _currentScrolls)
            {
                return false;
            }

            _currentScrolls -= amount;
            _events.Publish(new SkillScrollChangedEvent(_currentScrolls));
            return true;
        }
    }
}
