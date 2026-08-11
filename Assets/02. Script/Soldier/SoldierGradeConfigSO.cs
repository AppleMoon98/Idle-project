using Equipment;
using UnityEngine;

namespace Soldier
{
    /// <summary>
    /// 병사 원형(SoldierSO)이 참조하는 메인 등급(커먼~레전더리)이 플레이어 체력/공격력 대비
    /// 몇 %의 지분으로 반영되는지 정의하는 데이터 에셋. 등급 자체(EquipmentGradeSO)를 카탈로그
    /// 인덱스로 대분류(5단계씩 묶음)로 환산하는 방식은 Equipment.EquipmentPossessionConfigSO와
    /// 동일하지만, 그쪽은 "장비 보유 효과 누적 배율"이고 이쪽은 "플레이어 스탯 대비 지분율"이라
    /// 용도가 달라 별도 에셋으로 분리했다(SoldierEnhancementService가 EnhancementService와
    /// 구조만 같고 별도 서비스인 것과 같은 "도메인별 병렬 구현" 관례).
    /// </summary>
    [CreateAssetMenu(fileName = "SoldierGradeConfig", menuName = "Idle Project/Soldier/Soldier Grade Config")]
    public sealed class SoldierGradeConfigSO : ScriptableObject
    {
        [SerializeField]
        private EquipmentGradeCatalogSO gradeCatalog;

        /// <summary>
        /// 대분류 하나가 세부 등급(EquipmentGradeCatalogSO 인덱스) 몇 개로 이루어지는지.
        /// 현재 등급 체계는 6개 대분류 × 5단계이므로 기본값 5.
        /// </summary>
        [SerializeField]
        private int subGradesPerMainGrade = 5;

        /// <summary>
        /// 대분류 등급(커먼=0, 언커먼=1, 레어=2, 슈퍼레어=3, 에픽=4, 레전더리=5) 순서의
        /// 플레이어 스탯 대비 지분율(0.005 = 0.5%) 목록.
        /// </summary>
        [SerializeField]
        private float[] mainGradePercents = { 0.005f, 0.01f, 0.03f, 0.05f, 0.075f, 0.1f };

        /// <summary>
        /// grade가 속한 대분류의 지분율을 반환한다. 그레이드 카탈로그를 못 찾거나 grade가 목록에
        /// 없으면 0(지분 없음)을 반환한다.
        /// </summary>
        public float GetPercent(EquipmentGradeSO grade)
        {
            if (gradeCatalog == null || mainGradePercents == null || mainGradePercents.Length == 0)
            {
                return 0f;
            }

            int gradeIndex = gradeCatalog.IndexOf(grade);

            if (gradeIndex < 0)
            {
                return 0f;
            }

            int mainTier = Mathf.Clamp(gradeIndex / Mathf.Max(subGradesPerMainGrade, 1), 0, mainGradePercents.Length - 1);
            return mainGradePercents[mainTier];
        }
    }
}
