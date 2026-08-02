using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 가챠로 뽑을 수 있는 병사 한 종류의 데이터 에셋. 전투 중 실제로 배치되는 GameObject는
    /// Prefab이 가리키는 프리팹(Soldier.prefab/Soldier_Ranged.prefab 등)이 그대로 담당하고,
    /// 이 에셋은 로스터/세이브/가챠 테이블이 참조할 안정적인 식별자 역할만 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Soldier", menuName = "Idle Project/Soldier/Soldier")]
    public sealed class SoldierSO : ScriptableObject
    {
        [SerializeField]
        private string displayName;

        [SerializeField]
        private GameObject prefab;

        [SerializeField]
        private Sprite icon;

        /// <summary>
        /// 병사 이름(로스터/가챠 결과 UI 표시용).
        /// </summary>
        public string DisplayName => displayName;

        /// <summary>
        /// 배치 시 스폰할 프리팹.
        /// </summary>
        public GameObject Prefab => prefab;

        /// <summary>
        /// 목록 UI(로스터/배치/피커)에 표시할 아이콘. 아직 지정되지 않았으면 null.
        /// </summary>
        public Sprite Icon => icon;
    }
}
