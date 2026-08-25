using System;
using System.Collections.Generic;
using System.Reflection;
using Character;
using Enhancement;
using Save;
using Skill;
using UI;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// GitHub 이슈 #3("짧은 재접속에도 오프라인 보상이 '0시간'으로 표시되어 팝업이 반복 노출됨")과
    /// 이슈 #4("오프라인 보상 DPS가 장비·강화·실제 배치 병사를 반영하지 않음")의 완료 기준(경계값/
    /// 회귀 검증)을 만족하기 위한 수동 실행형 검증 모음. 이 프로젝트는 게임 코드 전용 asmdef가
    /// 없어(전부 암묵적 Assembly-CSharp) 별도 EditMode 테스트 어셈블리를 새로 만들어 참조하려면
    /// 프로젝트 전체 어셈블리 경계를 재구성해야 하는 부담이 있어, 대신 이 폴더(Assembly-CSharp-Editor로
    /// 컴파일)에서 직접 실행 가능한 검증 모음으로 구현했다 - Unity Test Runner 창에는 뜨지 않지만,
    /// 메뉴 한 번으로 전부 재실행할 수 있는 회귀 방지 도구로 동일하게 기능한다. Character.
    /// RuntimeStatApplier/PossessionStatApplier(internal)는 AssemblyInfo.cs의
    /// InternalsVisibleTo("Assembly-CSharp-Editor")로 접근을 허용해뒀다.
    /// </summary>
    internal static class RegressionChecks
    {
        [MenuItem("Idle Project/Run Regression Checks (Offline Reward)")]
        private static void RunAll()
        {
            var failures = new List<string>();
            int total = 0;

            void Check(string name, Action check)
            {
                total++;
                try
                {
                    check();
                }
                catch (Exception e)
                {
                    failures.Add($"{name}: {e.Message}");
                }
            }

            // --- 이슈 #3: 오프라인 보상 팝업 경계값 ---
            Check("FormatElapsedDuration_UnderOneMinute", () => AssertFormatElapsedDuration(9f, "0분 9초"));
            Check("FormatElapsedDuration_TypicalShortReconnect", () => AssertFormatElapsedDuration(609f, "10분 9초"));
            Check("FormatElapsedDuration_JustUnderOneHour", () => AssertFormatElapsedDuration(3599f, "59분 59초"));
            Check("FormatElapsedDuration_ExactlyOneHour", () => AssertFormatElapsedDuration(3600f, "1시간"));
            Check("FormatElapsedDuration_MultipleHours", () => AssertFormatElapsedDuration(9000f, "2.5시간"));
            Check("MinElapsedSecondsToShowPopup_DefaultsToFiveMinutes", CheckMinElapsedSecondsDefault);

            // --- 스킬 버프 곱연산 회귀 방지(플레이어가 실제로 확인 요청했던 시나리오) ---
            Check("SkillBuff_ApplyThenRevert_RestoresBaseline", CheckSkillBuffApplyRevert);
            Check("SkillBuff_TwoDifferentBuffs_StackMultiplicatively", CheckSkillBuffMultiplicativeStack);
            Check("SkillBuff_RevertOne_WhileOtherActive_RemovesOnlyOwnShare", CheckSkillBuffPartialRevert);

            // --- 이슈 #4: 강화/장비/랭크가 실제로 반영되는 핵심 메커니즘 ---
            Check("RuntimeStatApplier_AttackPower_FlatAdditive", CheckRuntimeStatApplierFlatAdditive);
            Check("RuntimeStatApplier_AttackSpeed_BaseRelativePercent", CheckRuntimeStatApplierAttackSpeed);
            Check("RuntimeStatApplier_NoDuplicateAcrossSeparateSoldiers", CheckRuntimeStatApplierNoDuplication);
            Check("PossessionStatApplier_AttackPower_PercentOfBase", CheckPossessionStatApplier);

            // --- 이슈 #7: 저장 데이터 일부 손상 시 안전한 기본값 복구(부트스트랩 크래시 방지) ---
            Check("SaveService_ParseLastActiveUnixTime_MalformedFallsBackToZero", CheckParseLastActiveUnixTimeOrZero);
            Check("SaveService_ParseBlobOrNull_MalformedJsonReturnsNullWithoutThrowing", CheckParseBlobOrNullSurvivesMalformedJson);

            if (failures.Count == 0)
            {
                Debug.Log($"[RegressionChecks] 전부 통과 ({total}/{total}).");
            }
            else
            {
                Debug.LogError($"[RegressionChecks] {failures.Count}/{total}개 실패:\n" + string.Join("\n", failures));
            }
        }

        private static void AssertFormatElapsedDuration(float seconds, string expected)
        {
            MethodInfo method = typeof(OfflineProgressPopupUI).GetMethod(
                "FormatElapsedDuration", BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new Exception("FormatElapsedDuration 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            string actual = (string)method.Invoke(null, new object[] { seconds });

            if (actual != expected)
            {
                throw new Exception($"seconds={seconds} 기대='{expected}' 실제='{actual}'");
            }
        }

        private static void CheckMinElapsedSecondsDefault()
        {
            var go = new GameObject("RegressionCheck_OfflinePopup");
            // 비활성 상태에서 컴포넌트를 추가해 Awake()가 즉시 실행되지 않게 한다 - Awake는
            // popupRoot(인스펙터로만 배선되는 필드)를 바로 SetActive(false)하려 들어 여기서는
            // NullReferenceException이 난다. 필드 기본값은 Awake 여부와 무관하게 이미 설정돼
            // 있으므로 리플렉션으로 읽는 데는 문제가 없다.
            go.SetActive(false);

            try
            {
                var popup = go.AddComponent<OfflineProgressPopupUI>();
                FieldInfo field = typeof(OfflineProgressPopupUI).GetField(
                    "minElapsedSecondsToShowPopup", BindingFlags.NonPublic | BindingFlags.Instance);

                float value = (float)field.GetValue(popup);

                if (!Mathf.Approximately(value, 300f))
                {
                    throw new Exception($"기대=300, 실제={value}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static RuntimeStats CreateStats(float attackPower = 10f, float attackInterval = 1f)
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();

            return new RuntimeStats(baseStats)
            {
                AttackPower = attackPower,
                AttackInterval = attackInterval
            };
        }

        private static void CheckSkillBuffApplyRevert()
        {
            RuntimeStats stats = CreateStats(attackInterval: 2f);

            float percent = SkillBuffStatApplier.ApplyPercent(stats, EnhancementStatType.AttackSpeed, 0.1f);
            AssertApprox(1.8f, stats.AttackInterval, "적용 직후");

            SkillBuffStatApplier.Revert(stats, EnhancementStatType.AttackSpeed, percent);
            AssertApprox(2f, stats.AttackInterval, "복원 후");
        }

        private static void CheckSkillBuffMultiplicativeStack()
        {
            RuntimeStats stats = CreateStats(attackInterval: 2f);

            SkillBuffStatApplier.ApplyPercent(stats, EnhancementStatType.AttackSpeed, 0.1f);
            SkillBuffStatApplier.ApplyPercent(stats, EnhancementStatType.AttackSpeed, 0.15f);

            AssertApprox(2f * 0.9f * 0.85f, stats.AttackInterval, "두 버프 동시 적용(곱연산이어야 함)");
        }

        private static void CheckSkillBuffPartialRevert()
        {
            RuntimeStats stats = CreateStats(attackInterval: 2f);

            float percentA = SkillBuffStatApplier.ApplyPercent(stats, EnhancementStatType.AttackSpeed, 0.1f);
            float percentB = SkillBuffStatApplier.ApplyPercent(stats, EnhancementStatType.AttackSpeed, 0.15f);

            SkillBuffStatApplier.Revert(stats, EnhancementStatType.AttackSpeed, percentA);
            AssertApprox(2f * 0.85f, stats.AttackInterval, "A만 되돌린 후");

            SkillBuffStatApplier.Revert(stats, EnhancementStatType.AttackSpeed, percentB);
            AssertApprox(2f, stats.AttackInterval, "B도 되돌린 후");
        }

        private static void CheckRuntimeStatApplierFlatAdditive()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
            SetPrivateFloat(baseStats, "attackPower", 100f);
            var stats = new RuntimeStats(baseStats);

            RuntimeStatApplier.Apply(stats, baseStats, EnhancementStatType.AttackPower, 25f);

            AssertApprox(125f, stats.AttackPower, "AttackPower 고정 가산");
        }

        private static void CheckRuntimeStatApplierAttackSpeed()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
            SetPrivateFloat(baseStats, "attackInterval", 2f);
            var stats = new RuntimeStats(baseStats);

            RuntimeStatApplier.Apply(stats, baseStats, EnhancementStatType.AttackSpeed, 0.18f);

            AssertApprox(2f * (1f - 0.18f), stats.AttackInterval, "AttackSpeed 기본값 대비 %");
        }

        private static void CheckRuntimeStatApplierNoDuplication()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
            SetPrivateFloat(baseStats, "attackPower", 70f);
            const float delta = 5f;

            var soldierA = new RuntimeStats(baseStats);
            RuntimeStatApplier.Apply(soldierA, baseStats, EnhancementStatType.AttackPower, delta);

            var soldierB = new RuntimeStats(baseStats);
            RuntimeStatApplier.Apply(soldierB, baseStats, EnhancementStatType.AttackPower, delta);

            AssertApprox(75f, soldierA.AttackPower, "병사 A");
            AssertApprox(75f, soldierB.AttackPower, "병사 B(중복 누적 없이 A와 동일해야 함)");
        }

        private static void CheckPossessionStatApplier()
        {
            var baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
            SetPrivateFloat(baseStats, "attackPower", 200f);
            var stats = new RuntimeStats(baseStats);

            PossessionStatApplier.Apply(stats, baseStats, EnhancementStatType.AttackPower, 0.1f);

            AssertApprox(220f, stats.AttackPower, "보유/랭크 보너스는 원본 대비 %");
        }

        private static void CheckParseLastActiveUnixTimeOrZero()
        {
            MethodInfo method = typeof(SaveService).GetMethod(
                "ParseLastActiveUnixTimeOrZero", BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new Exception("ParseLastActiveUnixTimeOrZero 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            AssertParsedTimestamp(method, "not-a-number", 0);
            AssertParsedTimestamp(method, "-100", 0);
            AssertParsedTimestamp(method, "99999999999999999999", 0); // long 범위 초과(overflow)
            AssertParsedTimestamp(method, "", 0);
            AssertParsedTimestamp(method, "1700000000", 1700000000);
        }

        private static void AssertParsedTimestamp(MethodInfo method, string raw, long expected)
        {
            long actual = (long)method.Invoke(null, new object[] { raw });

            if (actual != expected)
            {
                throw new Exception($"raw='{raw}' 기대={expected} 실제={actual}");
            }
        }

        private static void CheckParseBlobOrNullSurvivesMalformedJson()
        {
            Type nestedType = typeof(SaveService).GetNestedType("InventorySaveBlob", BindingFlags.NonPublic);

            if (nestedType == null)
            {
                throw new Exception("InventorySaveBlob 중첩 타입을 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            MethodInfo openGeneric = typeof(SaveService).GetMethod(
                "ParseBlobOrNull", BindingFlags.NonPublic | BindingFlags.Static);

            if (openGeneric == null)
            {
                throw new Exception("ParseBlobOrNull 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            MethodInfo closedGeneric = openGeneric.MakeGenericMethod(nestedType);

            object malformedResult = closedGeneric.Invoke(null, new object[] { "{ definitely-not-json" });

            if (malformedResult != null)
            {
                throw new Exception("깨진 JSON을 넘겼는데 예외 없이 null이 아닌 값을 반환함(예외가 새 나가지 않았는지도 함께 확인된 것)");
            }

            object emptyResult = closedGeneric.Invoke(null, new object[] { "" });

            if (emptyResult != null)
            {
                throw new Exception("빈 문자열을 넘겼는데 null이 아닌 값을 반환함");
            }
        }

        private static void SetPrivateFloat(UnityEngine.Object target, string fieldName, float value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new Exception($"필드 '{fieldName}'을 찾지 못함");
            }

            field.SetValue(target, value);
        }

        private static void AssertApprox(float expected, float actual, string context)
        {
            if (Mathf.Abs(expected - actual) > 0.0005f)
            {
                throw new Exception($"{context}: 기대={expected}, 실제={actual}");
            }
        }
    }
}
