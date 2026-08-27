using System;
using Core;
using Gacha.Events;
using UnityEngine;

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
        /// 음수면(과거 AddScrolls 오버플로가 저장된 뒤 이번 수정 전에 로드된 경우, GitHub 이슈 #45)
        /// 0으로 복원하되 조용히 넘어가지 않고 경고를 남긴다.
        /// </summary>
        public SkillScrollService(EventBus events, int initialScrolls = 0)
        {
            _events = events;

            if (initialScrolls < 0)
            {
                Debug.LogWarning($"[SkillScrollService] 저장된 스킬 주문서가 음수({initialScrolls})로 손상되어 있어 0으로 복원합니다 - 과거 오버플로로 인한 소실 가능성(GitHub 이슈 #45).");
            }

            _currentScrolls = initialScrolls > 0 ? initialScrolls : 0;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 주문서를 더하고 변경 이벤트를 발행한다. amount가 0 이하면(음수 지급 방지, GitHub 이슈 #8)
        /// 아무 것도 하지 않는다. 덧셈은 long으로 계산한 뒤 int.MaxValue로 saturate한다(GitHub 이슈
        /// #45) - 순수 int 덧셈은 상한 근처에서 오버플로해 음수로 반전되고, 그러면 TrySpendScrolls가
        /// 영원히 실패하며 다음 로드 때는 위 음수 복원 클램프에 걸려 전액 소실로 이어졌다.
        /// </summary>
        public void AddScrolls(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            long sum = (long)_currentScrolls + amount;
            _currentScrolls = (int)Math.Min(sum, int.MaxValue);
            _events.Publish(new SkillScrollChangedEvent(_currentScrolls));
        }

        /// <summary>
        /// 주문서 소비를 시도한다. amount가 0 이하이거나(GitHub 이슈 #8 - 음수를 빼면 잔액이
        /// 늘어나는 버그를 막는다) 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendScrolls(int amount)
        {
            if (amount <= 0 || amount > _currentScrolls)
            {
                return false;
            }

            _currentScrolls -= amount;
            _events.Publish(new SkillScrollChangedEvent(_currentScrolls));
            return true;
        }
    }
}
