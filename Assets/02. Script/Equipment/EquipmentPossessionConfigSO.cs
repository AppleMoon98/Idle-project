using System;
using System.Collections.Generic;
using UnityEngine;

namespace Equipment
{
    /// <summary>
    /// 장비 슬롯을 "보유"만 해도(장착 여부 무관) 얼마나 능력치를 주는지 정의하는 데이터 에셋.
    /// EquipmentStatConfigSO(장착 시 효과)와 슬롯→능력치 계수 형태가 완전히 같아
    /// EquipmentStatConfigSO.SlotStatEntry를 그대로 재사용한다 — 다만 소비하는 쪽(
    /// EquipmentPossessionService)의 계산 방식이 다르다(장착 1개가 아니라 보유 중인 모든 라인을 합산).
    /// 등급에 따른 증가는 SlotStatEntry.PerGradeIndex(30단계 세부 등급에 대한 선형 증가)가 아니라
    /// mainGradeMultipliers(커먼/언커먼/레어/... 6개 대분류 단위의 배율)로 별도 관리한다 — 사용자가
    /// "커먼 1 → 언커먼 5 → 레어 10 → ..."처럼 대분류 단위로 배율을 지정했기 때문에,
    /// 30단계 각각이 아니라 5단계씩 묶인 대분류 경계에서만 값이 바뀌는 계단식이어야 한다.
    /// SlotStatEntry.PerGradeIndex는 이 설정에서는 사용하지 않는다(공유 구조체라 필드 자체는 남아있음).
    /// </summary>
    [CreateAssetMenu(fileName = "EquipmentPossessionConfig", menuName = "Idle Project/Equipment/Equipment Possession Config")]
    public sealed class EquipmentPossessionConfigSO : ScriptableObject
    {
        /// <summary>
        /// 대분류 등급 구간별로 강화 레벨이 보유 효과에 반영되는 방식을 정의한다. 예를 들어
        /// "슈퍼레어까지는 강화 1레벨당 1%, 100강까지 / 에픽부터는 강화 1레벨당 10%, 20강까지"처럼
        /// 대분류에 따라 강화 성장률·상한이 완전히 달라질 수 있도록, EquipmentEnhancementConfigSO의
        /// 강화 배율(장착 효과용)과는 별도로 관리한다.
        /// </summary>
        [Serializable]
        public struct PossessionEnhancementTier
        {
            /// <summary>이 구간이 적용되는 최소 대분류 등급 인덱스(커먼=0, 언커먼=1, ...).</summary>
            public int MainGradeTierThreshold;

            /// <summary>강화 1레벨당 증가하는 비율(예: 0.01 = 1%).</summary>
            public float PercentPerLevel;

            /// <summary>보유 효과 계산에 반영되는 강화 레벨 상한. 이보다 높은 레벨은 상한값으로 고정된다.</summary>
            public int MaxLevel;
        }

        [SerializeField]
        private EquipmentStatConfigSO.SlotStatEntry[] entries;

        /// <summary>
        /// 대분류 등급(커먼=0, 언커먼=1, 레어=2, 슈퍼레어=3, 에픽=4, 레전더리=5) 순서의 배율 목록.
        /// </summary>
        [SerializeField]
        private float[] mainGradeMultipliers = { 1f, 5f, 10f, 20f, 50f, 100f };

        /// <summary>
        /// 대분류 하나가 세부 등급(EquipmentGradeCatalogSO 인덱스) 몇 개로 이루어지는지.
        /// 현재 등급 체계는 6개 대분류 × 5단계(커먼1~5 등)이므로 기본값 5.
        /// </summary>
        [SerializeField]
        private int subGradesPerMainGrade = 5;

        /// <summary>
        /// MainGradeTierThreshold 순서와 무관하게 등록 가능한 강화 반영 구간 목록. GetEnhancementTier가
        /// "mainGradeTier 이하인 threshold 중 가장 큰 것"을 골라 적용한다.
        /// </summary>
        [SerializeField]
        private PossessionEnhancementTier[] enhancementTiers =
        {
            new PossessionEnhancementTier { MainGradeTierThreshold = 0, PercentPerLevel = 0.01f, MaxLevel = 100 },
            new PossessionEnhancementTier { MainGradeTierThreshold = 4, PercentPerLevel = 0.10f, MaxLevel = 20 },
        };

        /// <summary>
        /// slot에 대응하는 능력치 계수를 모두 찾는다. EquipmentStatConfigSO.GetEntries와 동일.
        /// </summary>
        public IEnumerable<EquipmentStatConfigSO.SlotStatEntry> GetEntries(EquipmentType slot)
        {
            if (entries == null)
            {
                yield break;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Slot == slot)
                {
                    yield return entries[i];
                }
            }
        }

        /// <summary>
        /// gradeIndex(EquipmentGradeCatalogSO 기준 0~29)가 속한 대분류 등급 인덱스(커먼=0, ...)를 반환한다.
        /// </summary>
        public int GetMainGradeTier(int gradeIndex)
        {
            if (subGradesPerMainGrade <= 0)
            {
                return 0;
            }

            return Mathf.Max(gradeIndex, 0) / subGradesPerMainGrade;
        }

        /// <summary>
        /// gradeIndex가 속한 대분류의 배율을 반환한다. 배율 목록이 비어 있으면 1(배율 없음)을 반환한다.
        /// </summary>
        public float GetMainGradeMultiplier(int gradeIndex)
        {
            if (mainGradeMultipliers == null || mainGradeMultipliers.Length == 0)
            {
                return 1f;
            }

            int mainGradeTier = Mathf.Clamp(GetMainGradeTier(gradeIndex), 0, mainGradeMultipliers.Length - 1);
            return mainGradeMultipliers[mainGradeTier];
        }

        /// <summary>
        /// mainGradeTier에 적용되는 강화 반영 구간을 찾는다(threshold가 mainGradeTier 이하인 것 중 최댓값).
        /// 등록된 구간이 없으면 강화 반영 없음(PercentPerLevel=0, MaxLevel=0)으로 취급한다.
        /// </summary>
        public PossessionEnhancementTier GetEnhancementTier(int mainGradeTier)
        {
            PossessionEnhancementTier result = default;
            bool found = false;

            if (enhancementTiers != null)
            {
                for (int i = 0; i < enhancementTiers.Length; i++)
                {
                    PossessionEnhancementTier candidate = enhancementTiers[i];

                    if (candidate.MainGradeTierThreshold > mainGradeTier)
                    {
                        continue;
                    }

                    if (!found || candidate.MainGradeTierThreshold > result.MainGradeTierThreshold)
                    {
                        result = candidate;
                        found = true;
                    }
                }
            }

            return result;
        }
    }
}
