using System.Collections.Generic;
using System.Reflection;
using Enhancement;
using Equipment;
using Gacha;
using Skill;
using Soldier;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 재화를 소비하는 SO 콘텐츠(강화/장비강화/가챠/스킬/병사 배치 코스트)의 비용 필드가
    /// 음수이거나 비정상(NaN/Infinity)이 아닌지 프로젝트 전체 자산을 대상으로 검증한다
    /// (GitHub 이슈 #8 완료 조건: "잘못된 SO 비용이 발견되면 콘텐츠 검증 단계에서 구체적인
    /// 오류를 제공함"). Loot.CurrencyService 등 재화 서비스 자체는 런타임에 음수 소비/지급
    /// 요청을 거부하지만(section GN), 그 요청의 근원이 되는 SO 비용 값 자체가 애초에 잘못됐는지는
    /// 별개로 확인해야 한다 - [Min(0)] 인스펙터 속성(이 검증과 함께 추가됨)은 사람이 인스펙터에서
    /// 직접 값을 끌 때만 막아주고, 스크립트로 생성/수정되거나 .asset 파일이 직접 편집된 콘텐츠는
    /// 막지 못하므로 이 도구가 실제 저장된 값을 다시 한번 확인한다.
    ///
    /// StableIdBackfill.cs와 같은 "AssetDatabase.FindAssets로 프로젝트 자산 전체를 스캔하는"
    /// 1회성/반복 실행 가능한 Editor 검증 도구다. 비용 필드는 대부분 public 프로퍼티로 이미
    /// 노출돼 있어 그대로 읽고, Skill.SkillSO의 골드 비용만 public 프로퍼티가 없어(계산된
    /// GetGoldCost(level)만 공개) RegressionChecks.cs가 이미 쓰는 것과 같은
    /// 리플렉션으로 원본 필드를 직접 읽는다.
    /// </summary>
    internal static class ContentCostValidation
    {
        [MenuItem("Idle Project/Validate Content Costs")]
        private static void RunAll()
        {
            List<string> errors = ValidateAll(out int assetsChecked);

            if (errors.Count == 0)
            {
                Debug.Log($"[ContentCostValidation] 통과 - {assetsChecked}개 자산의 비용 필드 모두 정상.");
                return;
            }

            foreach (string error in errors)
            {
                Debug.LogError($"[ContentCostValidation] {error}");
            }

            Debug.LogError($"[ContentCostValidation] 실패 - {assetsChecked}개 자산 중 {errors.Count}건의 비용 오류 발견.");
        }

        /// <summary>
        /// 프로젝트의 비용 SO 자산 전체를 검증해 오류 목록을 그대로 돌려준다(로그 없이). 메뉴
        /// 항목(RunAll)과 RegressionChecks.cs의 회귀 검사가 같은 로직을 공유하는 진입점 —
        /// 같은 Assembly-CSharp-Editor 어셈블리 안이라 리플렉션 없이 직접 호출 가능하다.
        /// </summary>
        internal static List<string> ValidateAll(out int assetsChecked)
        {
            var errors = new List<string>();
            assetsChecked = 0;

            assetsChecked += ValidateEnhancementConfigs(errors);
            assetsChecked += ValidateEquipmentEnhancementConfigs(errors);
            assetsChecked += ValidateGachaTables(errors);
            assetsChecked += ValidateSkillGachaTables(errors);
            assetsChecked += ValidateEquipmentGachaTables(errors);
            assetsChecked += ValidateSkills(errors);
            assetsChecked += ValidateSoldiers(errors);

            return errors;
        }

        private static int ValidateEnhancementConfigs(List<string> errors)
        {
            int count = 0;

            foreach (var (path, config) in FindAssets<EnhancementConfigSO>())
            {
                count++;
                CheckNonNegative(errors, path, "BaseCost", config.BaseCost);
                CheckNonNegativeFinite(errors, path, "CostMultiplier", config.CostMultiplier, allowZero: false);
                CheckNonNegative(errors, path, "MaxLevel", config.MaxLevel);
                ValidateCostIncrementTiers(errors, path, config.CostIncrementTiers, config.MaxLevel);
            }

            return count;
        }

        private static int ValidateEquipmentEnhancementConfigs(List<string> errors)
        {
            int count = 0;

            foreach (var (path, config) in FindAssets<EquipmentEnhancementConfigSO>())
            {
                count++;
                CheckNonNegative(errors, path, "DuplicatesRequiredPerLevel", config.DuplicatesRequiredPerLevel);
                CheckNonNegative(errors, path, "StoneCostBase", config.StoneCostBase);
                CheckNonNegative(errors, path, "StoneCostIncreasePerLevel", config.StoneCostIncreasePerLevel);
                CheckNonNegative(errors, path, "MaxLevel", config.MaxLevel);
            }

            return count;
        }

        private static int ValidateGachaTables(List<string> errors)
        {
            int count = 0;

            // GachaTableSO.CostIncrementTiers는 public 프로퍼티가 없어(ValidateSkills의
            // goldCostBase 필드와 같은 이유로) 리플렉션으로 읽는다 - GitHub 이슈 #67 스캔 중
            // 이 필드가 지금까지 전혀 검증되지 않고 있었다는 것을 함께 발견했다.
            FieldInfo tiersField = typeof(GachaTableSO).GetField("costIncrementTiers", BindingFlags.NonPublic | BindingFlags.Instance);

            if (tiersField == null)
            {
                errors.Add("GachaTableSO의 costIncrementTiers 필드를 찾지 못함 - 필드 이름이 바뀌었는지 확인 필요");
            }

            foreach (var (path, table) in FindAssets<GachaTableSO>())
            {
                count++;
                CheckNonNegative(errors, path, "TicketCostPerPull", table.TicketCostPerPull);
                CheckNonNegative(errors, path, "GoldCostPerPull", table.GoldCostPerPull);

                if (tiersField != null)
                {
                    var tiers = (IReadOnlyList<CostIncrementTier>)tiersField.GetValue(table);
                    ValidateCostIncrementTiers(errors, path, tiers);
                }
            }

            return count;
        }

        private static int ValidateSkillGachaTables(List<string> errors)
        {
            int count = 0;

            FieldInfo tiersField = typeof(SkillGachaTableSO).GetField("costIncrementTiers", BindingFlags.NonPublic | BindingFlags.Instance);

            if (tiersField == null)
            {
                errors.Add("SkillGachaTableSO의 costIncrementTiers 필드를 찾지 못함 - 필드 이름이 바뀌었는지 확인 필요");
            }

            foreach (var (path, table) in FindAssets<SkillGachaTableSO>())
            {
                count++;
                CheckNonNegative(errors, path, "TicketCostPerPull", table.TicketCostPerPull);
                CheckNonNegative(errors, path, "GoldCostPerPull", table.GoldCostPerPull);

                if (tiersField != null)
                {
                    var tiers = (IReadOnlyList<CostIncrementTier>)tiersField.GetValue(table);
                    ValidateCostIncrementTiers(errors, path, tiers);
                }
            }

            return count;
        }

        private static int ValidateEquipmentGachaTables(List<string> errors)
        {
            int count = 0;

            foreach (var (path, table) in FindAssets<EquipmentGachaTableSO>())
            {
                count++;
                CheckNonNegative(errors, path, "GoldCostPerPull", table.GoldCostPerPull);
            }

            return count;
        }

        private static int ValidateSkills(List<string> errors)
        {
            int count = 0;

            FieldInfo goldCostBaseField = typeof(SkillSO).GetField("goldCostBase", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo goldCostIncreaseField = typeof(SkillSO).GetField("goldCostIncreasePerLevel", BindingFlags.NonPublic | BindingFlags.Instance);

            if (goldCostBaseField == null || goldCostIncreaseField == null)
            {
                errors.Add("SkillSO의 비용 필드를 찾지 못함 - 필드 이름이 바뀌었는지 확인 필요");
                return count;
            }

            foreach (var (path, skill) in FindAssets<SkillSO>())
            {
                count++;
                CheckNonNegative(errors, path, "goldCostBase", (int)goldCostBaseField.GetValue(skill));
                CheckNonNegative(errors, path, "goldCostIncreasePerLevel", (int)goldCostIncreaseField.GetValue(skill));
            }

            return count;
        }

        private static int ValidateSoldiers(List<string> errors)
        {
            int count = 0;

            foreach (var (path, soldier) in FindAssets<SoldierSO>())
            {
                count++;
                CheckNonNegative(errors, path, "Cost", soldier.Cost);
            }

            return count;
        }

        /// <summary>
        /// GitHub 이슈 #67 - 원소별 음수 검사만으로는 역순·중복 임계값, MaxLevel을 벗어나
        /// 영원히 적용되지 않는 죽은 구간, 최대 레벨까지 누산 시 long 경계 오버플로를 전혀
        /// 잡지 못했다. <paramref name="maxLevel"/>은 그 개념이 있는 콘텐츠(EnhancementConfigSO)만
        /// 넘긴다 - 가챠 테이블처럼 누적 뽑기 횟수에 자연스러운 상한이 없는 콘텐츠는 null로
        /// 넘겨 "죽은 구간"/"최대치 오버플로" 두 검사를 건너뛴다(런타임 CostIncrementTier.
        /// CalculateTotal 자신의 포화 처리가 그 경우의 최종 안전망).
        /// </summary>
        private static void ValidateCostIncrementTiers(
            List<string> errors, string assetPath, IReadOnlyList<CostIncrementTier> tiers, int? maxLevel = null)
        {
            if (tiers == null || tiers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < tiers.Count; i++)
            {
                CheckNonNegative(errors, assetPath, $"CostIncrementTiers[{i}].LevelThreshold", tiers[i].LevelThreshold);
                CheckNonNegative(errors, assetPath, $"CostIncrementTiers[{i}].Increment", tiers[i].Increment);

                if (i > 0 && tiers[i].LevelThreshold <= tiers[i - 1].LevelThreshold)
                {
                    errors.Add(
                        $"{assetPath} - CostIncrementTiers[{i}].LevelThreshold = {tiers[i].LevelThreshold} " +
                        $"(바로 이전 구간 CostIncrementTiers[{i - 1}].LevelThreshold = {tiers[i - 1].LevelThreshold} " +
                        "보다 커야 함 - 역순 또는 중복된 임계값이라 구간이 항상 0 길이가 되거나 뒤 구간이 앞 구간을 덮어씀)");
                }

                if (maxLevel.HasValue && tiers[i].LevelThreshold >= maxLevel.Value)
                {
                    errors.Add(
                        $"{assetPath} - CostIncrementTiers[{i}].LevelThreshold = {tiers[i].LevelThreshold} " +
                        $"(MaxLevel = {maxLevel.Value} 이상이라 이 구간은 실제 레벨이 절대 도달할 수 없는 죽은 구간)");
                }
            }

            if (maxLevel.HasValue)
            {
                long totalAtMaxLevel = CostIncrementTier.CalculateTotal(0, tiers, maxLevel.Value);

                if (totalAtMaxLevel == long.MaxValue || totalAtMaxLevel == long.MinValue)
                {
                    errors.Add(
                        $"{assetPath} - CostIncrementTiers가 MaxLevel({maxLevel.Value})까지 누산될 때 long 범위를 넘어 " +
                        "포화됨(정상적인 강화 곡선이라면 나올 수 없는 규모) - Increment 값을 재검토할 것");
                }
            }
        }

        private static IEnumerable<(string path, T asset)> FindAssets<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset != null)
                {
                    yield return (path, asset);
                }
            }
        }

        private static void CheckNonNegative(List<string> errors, string assetPath, string fieldName, int value)
        {
            if (value < 0)
            {
                errors.Add($"{assetPath} - {fieldName} = {value} (0 이상이어야 함)");
            }
        }

        private static void CheckNonNegativeFinite(List<string> errors, string assetPath, string fieldName, float value, bool allowZero)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                errors.Add($"{assetPath} - {fieldName} = {value} (NaN/Infinity 불가)");
                return;
            }

            bool invalid = allowZero ? value < 0f : value <= 0f;

            if (invalid)
            {
                string requirement = allowZero ? "0 이상이어야 함" : "0보다 커야 함";
                errors.Add($"{assetPath} - {fieldName} = {value} ({requirement})");
            }
        }
    }
}
