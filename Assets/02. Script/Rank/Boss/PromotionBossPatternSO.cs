using Combat.BossPattern;
using UnityEngine;

namespace Rank.Boss
{
    /// <summary>
    /// 승급전 보스의 평시 순환 패턴 하나를 정의하는 데이터 자산. War.Boss.WarBossPatternSO는
    /// 판정 한 건(원형 하나)만 표현하지만, 이 자산은 BossPatternHit 여러 개를 순서대로 담을 수
    /// 있어(찌르기=1개, 앞뒤 가르기=2개) 다단계 판정도 코드 분기 없이 데이터만으로 표현한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PromotionBossPattern", menuName = "Idle Project/Rank/Promotion Boss Pattern")]
    public sealed class PromotionBossPatternSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private BossPatternHit[] hits;

        /// <summary>
        /// 패턴 이름(디버그/에디터 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 이 패턴을 이루는 판정들, 표시 순서(Delay 오름차순)로 저장돼 있어야 한다.
        /// </summary>
        public BossPatternHit[] Hits => hits;
    }
}
