using System;
using Core;
using Dungeon.Events;
using UnityEngine;

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
        /// 음수면(과거 AddTokens 오버플로가 저장된 뒤 이번 수정 전에 로드된 경우, GitHub 이슈 #45)
        /// 0으로 복원하되 조용히 넘어가지 않고 경고를 남긴다.
        /// </summary>
        public BossTokenService(EventBus events, int initialTokens = 0)
        {
            _events = events;

            if (initialTokens < 0)
            {
                Debug.LogWarning($"[BossTokenService] 저장된 보스 토벌 증표가 음수({initialTokens})로 손상되어 있어 0으로 복원합니다 - 과거 오버플로로 인한 소실 가능성(GitHub 이슈 #45).");
            }

            _currentTokens = initialTokens > 0 ? initialTokens : 0;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        /// <summary>
        /// 증표를 더하고 변경 이벤트를 발행한다. amount가 0 이하면(음수 지급 방지, GitHub 이슈 #8)
        /// 아무 것도 하지 않는다. 덧셈은 long으로 계산한 뒤 int.MaxValue로 saturate한다(GitHub 이슈
        /// #45) - 순수 int 덧셈은 상한 근처에서 오버플로해 음수로 반전되고, 그러면 TrySpendTokens가
        /// 영원히 실패하며 다음 로드 때는 위 음수 복원 클램프에 걸려 전액 소실로 이어졌다.
        /// </summary>
        public void AddTokens(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            long sum = (long)_currentTokens + amount;
            _currentTokens = (int)Math.Min(sum, int.MaxValue);
            _events.Publish(new BossTokenChangedEvent(_currentTokens));
        }

        /// <summary>
        /// 증표 소비를 시도한다. amount가 0 이하이거나(GitHub 이슈 #8 - 음수를 빼면 잔액이
        /// 늘어나는 버그를 막는다) 잔액이 부족하면 아무 변화 없이 false를 반환한다.
        /// </summary>
        public bool TrySpendTokens(int amount)
        {
            if (amount <= 0 || amount > _currentTokens)
            {
                return false;
            }

            _currentTokens -= amount;
            _events.Publish(new BossTokenChangedEvent(_currentTokens));
            return true;
        }
    }
}
