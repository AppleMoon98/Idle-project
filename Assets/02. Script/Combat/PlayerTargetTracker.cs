using Character;
using Core;

namespace Combat
{
    /// <summary>
    /// 플레이어가 현재 어떤 대상을 타겟팅 중인지 기록하는 단일 슬롯 트래커.
    /// 플레이어는 하나뿐이므로 SoldierTargetRegistry처럼 여러 클레임을 관리할 필요가 없다.
    /// </summary>
    public sealed class PlayerTargetTracker : IManager, IService
    {
        /// <summary>
        /// 플레이어가 현재 타겟팅 중인 대상. 없으면 null.
        /// </summary>
        public Health CurrentTarget { get; private set; }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            CurrentTarget = null;
        }

        /// <summary>
        /// 플레이어의 EnemyTracker가 이번 틱에 선택한 최종 타겟을 기록한다.
        /// </summary>
        public void SetCurrentTarget(Health target)
        {
            CurrentTarget = target;
        }
    }
}
