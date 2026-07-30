using Character;

namespace Combat
{
    /// <summary>
    /// EnemyTracker가 추적 대상을 고를 때 후보를 선별하는 선택적 훅.
    /// 컴포넌트에 부착되어 있지 않으면 EnemyTracker는 단순 최근접 우선으로 동작한다.
    /// </summary>
    public interface ITargetFilter
    {
        /// <summary>
        /// candidate가 우선적으로 선택되어야 하는 후보인지 여부를 반환한다.
        /// 선호 후보가 하나도 없으면 EnemyTracker는 이 값과 무관하게 최근접 후보로 폴백한다.
        /// </summary>
        bool IsPreferred(Health candidate);

        /// <summary>
        /// EnemyTracker가 이번 틱에 최종 선택한 대상을 알려준다. 선택된 대상이 없으면 null.
        /// </summary>
        void OnTargetAcquired(Health target);
    }
}
