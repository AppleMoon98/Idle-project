using Core;
using Equipment.Events;

namespace Equipment
{
    /// <summary>
    /// 보유 강화석을 관리하는 서비스. CurrencyService(골드)와 동일한 형태지만,
    /// 지금은 이 재화를 얻을 방법이 없다 — 추후 "강화석 던전" 시스템이 AddStones를 호출해 채워줄
    /// 것을 염두에 두고 소비 쪽(장비 강화)만 먼저 만들어둔다.
    /// </summary>
    public sealed class EnhancementStoneService : IManager, IService
    {
        private readonly EventBus _events;
        private int _currentStones;

        /// <summary>
        /// 현재 보유 강화석.
        /// </summary>
        public int CurrentStones => _currentStones;

        /// <summary>
        /// initialStones: 저장된 강화석으로 시작하기 위한 초기값(SaveService.Load() 결과). 기본 0.
        /// </summary>
        public EnhancementStoneService(EventBus events, int initialStones = 0)
        {
            _events = events;
            _currentStones = initialStones > 0 ? initialStones : 0;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 강화석을 더하고 변경 이벤트를 발행한다. amount가 0 이하면(음수 지급 방지, GitHub 이슈 #8)
        /// 아무 것도 하지 않는다.
        /// </summary>
        public void AddStones(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _currentStones += amount;
            _events.Publish(new EnhancementStoneChangedEvent(_currentStones));
        }

        /// <summary>
        /// 강화석 소비를 시도한다. amount가 0 이하이거나(GitHub 이슈 #8 - 음수를 빼면 잔액이
        /// 늘어나는 버그를 막는다) 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendStones(int amount)
        {
            if (amount <= 0 || amount > _currentStones)
            {
                return false;
            }

            _currentStones -= amount;
            _events.Publish(new EnhancementStoneChangedEvent(_currentStones));
            return true;
        }
    }
}
