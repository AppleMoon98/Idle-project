using Equipment;
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

        [SerializeField]
        private EquipmentGradeSO grade;

        [SerializeField]
        private float iconScale = 1f;

        [SerializeField]
        private bool iconIgnoreGradeTint = false;

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

        /// <summary>
        /// 병종(Prefab)과 별개의 병사 등급 — Equipment.EquipmentGradeSO를 그대로 재사용해(6개
        /// 메인 등급의 대표 1티어만 참조), 등급 색조 틴트/이름을 장비와 동일한 데이터로 표시할 수
        /// 있게 한다. Soldier.SoldierGradeConfigSO가 이 값을 스탯 배율로 변환한다. null이면
        /// 배율 없음(등급 개념이 없는 구형 항목).
        /// </summary>
        public EquipmentGradeSO Grade => grade;

        /// <summary>
        /// 아이콘 렌더링 배율(기본 1). 실사진풍 아바타처럼 원본 실루엣 플레이스홀더보다 여백이
        /// 많은 아이콘을 교체했을 때, 이 항목만 개별적으로 키워서 다른 병종 아이콘과 시각적
        /// 크기를 맞추기 위한 것 — 그 외 값이 1인 대다수 항목은 전혀 영향받지 않는다.
        /// </summary>
        public float IconScale => iconScale;

        /// <summary>
        /// true면 아이콘에 등급 틴트(Grade.TintColor)를 입히지 않고 항상 흰색(원본 색 그대로)으로
        /// 표시한다. 실루엣 플레이스홀더 아이콘은 등급 틴트가 있어야 구분되지만(section DR), 이미
        /// 자체 색을 가진 실사진풍 아바타는 틴트를 덧입히면 오히려 색이 탁해지므로 개별 예외로 둔다.
        /// </summary>
        public bool IconIgnoreGradeTint => iconIgnoreGradeTint;
    }
}
