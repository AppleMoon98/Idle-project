using UnityEngine;

namespace Behavior
{
    /// <summary>
    /// 병사에게 배정하는 행동 프로필 데이터 에셋. Rules는 우선순위 순서대로 평가되며,
    /// 처음으로 조건이 만족되는 규칙의 모드가 그 판정 주기의 행동으로 채택된다
    /// (보통 마지막 규칙은 AlwaysConditionSO로 채워 기본 행동을 보장한다).
    /// </summary>
    [CreateAssetMenu(fileName = "BehaviorProfile", menuName = "Idle Project/Behavior/Behavior Profile")]
    public sealed class BehaviorProfileSO : ScriptableObject
    {
        [SerializeField]
        private string stableId;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private BehaviorRuleEntry[] rules;

        /// <summary>
        /// 카탈로그 배열 순서와 무관하게 이 항목을 영구적으로 식별하는 GUID
        /// (EquipmentSO.StableId와 동일한 이유·동일한 발급 도구, GitHub 이슈 #19).
        /// </summary>
        public string StableId => stableId;

        /// <summary>
        /// 프로필 이름(선택 UI 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 우선순위 순서의 규칙 목록.
        /// </summary>
        public BehaviorRuleEntry[] Rules => rules;
    }
}
