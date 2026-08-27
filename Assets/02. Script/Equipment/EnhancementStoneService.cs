using System;
using Core;
using Equipment.Events;
using UnityEngine;

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
        /// 음수면(과거 AddStones 오버플로가 저장된 뒤 이번 수정 전에 로드된 경우, GitHub 이슈 #45)
        /// 0으로 복원하되 조용히 넘어가지 않고 경고를 남긴다 - 재화가 소실됐다는 사실 자체는
        /// 사라지지 않지만, 적어도 원인 파악은 가능하게 한다.
        /// </summary>
        public EnhancementStoneService(EventBus events, int initialStones = 0)
        {
            _events = events;

            if (initialStones < 0)
            {
                Debug.LogWarning($"[EnhancementStoneService] 저장된 강화석이 음수({initialStones})로 손상되어 있어 0으로 복원합니다 - 과거 오버플로로 인한 소실 가능성(GitHub 이슈 #45).");
            }

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
        /// 아무 것도 하지 않는다. 덧셈은 long으로 계산한 뒤 int.MaxValue로 saturate한다(GitHub 이슈
        /// #45) - 순수 int 덧셈은 상한 근처에서 오버플로해 음수로 반전되고, 그러면 TrySpendStones가
        /// 영원히 실패하며 다음 로드 때는 위 음수 복원 클램프에 걸려 전액 소실로 이어졌다.
        /// </summary>
        public void AddStones(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            long sum = (long)_currentStones + amount;
            _currentStones = (int)Math.Min(sum, int.MaxValue);
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
