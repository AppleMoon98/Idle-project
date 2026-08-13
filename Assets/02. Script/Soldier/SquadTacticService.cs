using System.Collections.Generic;
using Core;
using Soldier.Events;

namespace Soldier
{
    /// <summary>
    /// 부대(0 ~ SquadCount-1)마다 어떤 전술이 배정돼 있는지만 저장한다. 기본값은 전 부대
    /// None(전술 없음). 실제 전술 효과(대형 재배치 등)는 이 서비스가 아니라 그 전술을 구독하는
    /// 별도 조율자(예: SquadShieldWallCoordinator)가 담당한다 — Enhancement.EnhancementService가
    /// 레벨/비용만 갖고 실제 스탯 적용은 StatEnhancementReceiver에게 맡기는 것과 같은 "서비스는
    /// 상태만, 적용은 구독자가" 분리.
    ///
    /// 세이브하지 않는다 — Stage.StageModeService(돌파/반복 모드)와 같은 선택으로, 매 실행마다
    /// 기본값(전술 없음)으로 시작해도 괜찮은 세션 단위 설정으로 취급한다.
    /// </summary>
    public sealed class SquadTacticService : IManager, IService
    {
        private readonly EventBus _events;
        private readonly Dictionary<int, SquadTacticType> _tactics = new();

        public SquadTacticService(EventBus events)
        {
            _events = events;
        }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            _tactics.Clear();
        }

        /// <summary>
        /// squadIndex에 배정된 전술. 아직 아무것도 고르지 않았으면 None.
        /// </summary>
        public SquadTacticType GetTactic(int squadIndex)
        {
            return _tactics.TryGetValue(squadIndex, out SquadTacticType tactic) ? tactic : SquadTacticType.None;
        }

        /// <summary>
        /// squadIndex의 전술을 바꾼다. 실제로 값이 바뀔 때만 SquadTacticChangedEvent를 발행한다.
        /// </summary>
        public void SetTactic(int squadIndex, SquadTacticType tactic)
        {
            if (GetTactic(squadIndex) == tactic)
            {
                return;
            }

            _tactics[squadIndex] = tactic;
            _events?.Publish(new SquadTacticChangedEvent(squadIndex, tactic));
        }
    }
}
