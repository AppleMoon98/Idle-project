using UnityEngine;

namespace War.Boss
{
    /// <summary>
    /// War 보스의 광역 공격 패턴 하나를 정의하는 데이터 에셋. 예고(텔레그래프) 표시 후
    /// 일정 시간 뒤 그 자리에 남아있는 아군에게 데미지를 준다. 패턴 종류는 코드 분기가 아니라
    /// 이 데이터의 조합(반경/시간/데미지)만으로 구분한다.
    /// </summary>
    [CreateAssetMenu(fileName = "WarBossPattern", menuName = "Idle Project/War/War Boss Pattern")]
    public sealed class WarBossPatternSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private float telegraphDuration = 1.5f;

        [SerializeField]
        private float radius = 3f;

        [SerializeField]
        private float damage = 20f;

        /// <summary>
        /// 패턴 이름(디버그/에디터 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 예고 표시부터 실제 판정까지 걸리는 시간(초).
        /// </summary>
        public float TelegraphDuration => telegraphDuration;

        /// <summary>
        /// 판정 반경.
        /// </summary>
        public float Radius => radius;

        /// <summary>
        /// 판정 시 반경 안 대상에게 주는 데미지.
        /// </summary>
        public float Damage => damage;
    }
}
