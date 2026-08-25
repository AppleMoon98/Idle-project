using System;
using System.Collections.Generic;
using System.Reflection;
using Behavior;
using Character;
using Core;
using Dungeon;
using Enhancement;
using Equipment;
using Gacha;
using Inventory;
using Loot;
using Rank;
using Save;
using Skill;
using Soldier;
using UI;
using UI.Events;
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

            // --- 이슈 #20: PoolManager 미등록 시 던전/승급전 컨트롤러가 상태를 커밋하지 않음
            // (준비→생성→커밋 순서로 뒤집은 뒤에도 스폰 실패 경로가 여전히 안전한지) ---
            Check("RankPromotionBattleController_TrySpawnBoss_NoPoolManager_ReturnsFalse",
                () => AssertTrySpawnBossReturnsFalse<RankPromotionBattleController>());
            Check("StoneDungeonSessionController_TrySpawnBoss_NoPoolManager_ReturnsFalse",
                () => AssertTrySpawnBossReturnsFalse<StoneDungeonSessionController>());
            Check("SkillDungeonSessionController_TrySpawnBoss_NoPoolManager_ReturnsFalse",
                () => AssertTrySpawnBossReturnsFalse<SkillDungeonSessionController>());
            Check("BossDungeonSessionController_TrySpawnBoss_NoPoolManager_ReturnsFalse",
                () => AssertTrySpawnBossReturnsFalse<BossDungeonSessionController>());
            Check("GoldDungeonSessionController_TrySpawnMonsters_NoPoolManager_ReturnsFalse",
                () => AssertTrySpawnCollectionReturnsFalse<GoldDungeonSessionController>("TrySpawnMonsters", "_aliveMonsters"));
            Check("SoldierRescueDungeonSessionController_TrySpawnZones_NoPoolManager_ReturnsFalse",
                () => AssertTrySpawnCollectionReturnsFalse<SoldierRescueDungeonSessionController>("TrySpawnZones", "_activeZones"));

            // --- 이슈 #8: 재화 서비스가 음수/0 소비·지급 요청을 거부(TrySpend*(-5)가 잔액을
            // 늘리는 버그 방지), 저장 복원 시 음수 초기값을 0으로 클램프 ---
            Check("CurrencyService_RejectsNonPositiveAmounts", CheckCurrencyServiceRejectsNonPositiveAmounts);
            Check("EnhancementStoneService_RejectsNonPositiveAmounts", CheckEnhancementStoneServiceRejectsNonPositiveAmounts);
            Check("SoldierTicketService_RejectsNonPositiveAmounts", CheckSoldierTicketServiceRejectsNonPositiveAmounts);
            Check("SkillScrollService_RejectsNonPositiveAmounts", CheckSkillScrollServiceRejectsNonPositiveAmounts);
            Check("EquipmentGachaTicketService_RejectsNonPositiveAmounts", CheckEquipmentGachaTicketServiceRejectsNonPositiveAmounts);
            Check("BossTokenService_RejectsNonPositiveAmounts", CheckBossTokenServiceRejectsNonPositiveAmounts);

            // --- 이슈 #19: 카탈로그가 배열 인덱스 대신 StableId로 항목을 식별(재정렬/삭제에도
            // 안전) + 실제 프로젝트 콘텐츠에 중복/빈 StableId가 없는지 ---
            Check("EquipmentCatalog_FindByStableId_RoundTripsAndRejectsUnknown", CheckEquipmentCatalogFindByStableId);
            Check("EquipmentCatalog_RealContent_NoDuplicateOrEmptyStableIds",
                () => AssertNoDuplicateOrEmptyStableIds<EquipmentCatalogSO, EquipmentSO>(
                    c => c.Items, so => so.StableId));
            Check("SoldierCatalog_RealContent_NoDuplicateOrEmptyStableIds",
                () => AssertNoDuplicateOrEmptyStableIds<SoldierCatalogSO, SoldierSO>(
                    c => c.Soldiers, so => so.StableId));
            Check("SkillCatalog_RealContent_NoDuplicateOrEmptyStableIds",
                () => AssertNoDuplicateOrEmptyStableIds<SkillCatalogSO, SkillSO>(
                    c => c.Skills, so => so.StableId));
            Check("BehaviorProfileCatalog_RealContent_NoDuplicateOrEmptyStableIds",
                () => AssertNoDuplicateOrEmptyStableIds<BehaviorProfileCatalogSO, BehaviorProfileSO>(
                    c => c.Profiles, so => so.StableId));

            // --- 이슈 #10: 가챠 결과 슬롯이 패널 비활성화 시 풀로 반납되어, 여러 컨트롤러가
            // 같은 풀을 공유해도 전체 인스턴스 수가 늘어나지 않고 재사용됨 ---
            Check("GachaResultRevealController_OnDisable_ReleasesSpawnedSlotsForReuse",
                CheckGachaResultRevealControllerReleasesOnDisable);

            // --- 이슈 #21: 300연 가챠/대량 이벤트로 인한 O(n^2) 성능 저하 3곳 ---
            Check("SkillGachaTableSO_Entries_ReturnsCachedArrayOnRepeatedAccess",
                CheckSkillGachaTableEntriesCached);
            Check("SkillGachaTableSO_Entries_DoesNotCacheEmptyResult_RecoversOnceCatalogFilled",
                CheckSkillGachaTableEntriesDoesNotCacheEmptyResult);
            Check("SkillGachaService_Pull_LevelableCandidatesComputedOncePerBatch",
                CheckSkillGachaServiceCandidatesBuiltOncePerBatch);
            Check("SaveService_InventoryHandlers_DeferRebuildToTick_NotEagerly",
                CheckSaveServiceInventoryHandlersDeferRebuildToTick);
            Check("SaveService_OtherSnapshotHandlers_SetDirtyFlagWithoutEagerRebuild",
                CheckSaveServiceOtherHandlersDeferRebuildToTick);

            // --- 이슈 #22: 300회 요청의 부분 성공/전체 실패가 이유·실행 수 안내 없이 조용히 끝남 ---
            Check("GachaPullToast_FullSuccess_NoToast", CheckGachaPullToastFullSuccessNoToast);
            Check("GachaPullToast_ZeroSuccess_InsufficientCurrency_GenericMessage",
                CheckGachaPullToastZeroSuccessInsufficientCurrency);
            Check("GachaPullToast_PartialSuccess_InsufficientCurrency_IncludesCounts",
                CheckGachaPullToastPartialSuccessInsufficientCurrency);
            Check("GachaPullToast_NoCandidates_ZeroSuccess_UsesProvidedMessage",
                CheckGachaPullToastNoCandidatesZeroSuccess);
            Check("GachaAffordabilityCalculator_FixedCost_ExactDivision",
                CheckGachaAffordabilityCalculatorFixedCost);
            Check("GachaAffordabilityCalculator_EscalatingCost_SimulatesCumulativeSpend",
                CheckGachaAffordabilityCalculatorEscalatingCost);
            Check("GachaAffordabilityCalculator_FixedCost_CapsAtMaxSimulatedPulls",
                CheckGachaAffordabilityCalculatorFixedCostCapsAtMaxSimulatedPulls);
            Check("GachaAffordabilityCalculator_EscalatingCost_CapsAtMaxSimulatedPulls",
                CheckGachaAffordabilityCalculatorEscalatingCostCapsAtMaxSimulatedPulls);
            Check("SkillGachaService_Pull_InsufficientScrolls_PartialSuccessPublishesCountedToast",
                CheckSkillGachaServiceInsufficientCurrencyPublishesPartialToast);
            Check("SkillGachaService_Pull_AllSkillsMaxLevel_ZeroResultsPublishesReasonToast",
                CheckSkillGachaServiceAllMaxLevelPublishesNoCandidatesToast);
            Check("SkillGachaService_HasAnyLevelableCandidate_ReflectsPerTierMaxLevelState",
                CheckSkillGachaServiceHasAnyLevelableCandidate);

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

        /// <summary>
        /// action을 실행하는 동안 Core.GameBootstrapper.Services를 null로 바꿔둔다(실행 전/후
        /// 무관하게 항상 원래 값으로 복원, PoolManager를 구할 수 없는 상황을 재현하기 위함) -
        /// 이슈 #20의 재현 시나리오("PoolManager 없이 임시 ServiceLocator 구성")를, 새 ServiceLocator를
        /// 따로 만드는 대신 전역 참조를 잠깐 비우는 방식으로 재현한다. Services는
        /// { get; private set; } 자동 프로퍼티라 세터를 리플렉션으로 직접 호출한다.
        /// </summary>
        private static void WithNullServices(Action action)
        {
            PropertyInfo property = typeof(Core.GameBootstrapper).GetProperty(
                "Services", BindingFlags.Public | BindingFlags.Static);

            if (property == null)
            {
                throw new Exception("GameBootstrapper.Services 프로퍼티를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            object original = property.GetValue(null);

            try
            {
                property.SetValue(null, null);
                action();
            }
            finally
            {
                property.SetValue(null, original);
            }
        }

        /// <summary>
        /// T.TrySpawnBoss(out GameObject)가 PoolManager 없이 호출되면 false를 반환하고 out
        /// 인자도 null로 남아있는지 확인한다(GitHub 이슈 #20 - 호출부가 이 반환값을 확인하기
        /// 전까지는 _isActive/_isFighting 등 세션 상태를 커밋하지 않아야 한다).
        /// </summary>
        private static void AssertTrySpawnBossReturnsFalse<T>() where T : Component
        {
            var go = new GameObject($"RegressionCheck_{typeof(T).Name}");
            go.SetActive(false); // Awake()가 즉시 실행되지 않게(CheckMinElapsedSecondsDefault와 동일한 이유)

            try
            {
                var controller = go.AddComponent<T>();

                MethodInfo method = typeof(T).GetMethod("TrySpawnBoss", BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    throw new Exception($"{typeof(T).Name}.TrySpawnBoss 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
                }

                WithNullServices(() =>
                {
                    object[] args = { null };
                    bool success = (bool)method.Invoke(controller, args);
                    var instance = (GameObject)args[0];

                    if (success || instance != null)
                    {
                        throw new Exception($"PoolManager 없이도 성공을 반환함(success={success}, instance={(instance != null ? instance.name : "null")})");
                    }
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// T.{methodName}()(bool 반환, 인자 없음)이 PoolManager 없이 호출되면 false를 반환하고
        /// collectionFieldName 필드(예: _aliveMonsters/_activeZones)가 비어있는지 확인한다.
        /// AssertTrySpawnBossReturnsFalse의 "단일 대상 out 인자" 대신 "다중 대상 컬렉션 필드"
        /// 버전 - Gold 던전(_aliveMonsters)/병사 구출 던전(_activeZones)이 이 모양이다.
        /// </summary>
        private static void AssertTrySpawnCollectionReturnsFalse<T>(string methodName, string collectionFieldName) where T : Component
        {
            var go = new GameObject($"RegressionCheck_{typeof(T).Name}");
            go.SetActive(false);

            try
            {
                var controller = go.AddComponent<T>();

                MethodInfo method = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    throw new Exception($"{typeof(T).Name}.{methodName} 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
                }

                FieldInfo field = typeof(T).GetField(collectionFieldName, BindingFlags.NonPublic | BindingFlags.Instance);

                if (field == null)
                {
                    throw new Exception($"{typeof(T).Name}.{collectionFieldName} 필드를 찾지 못함 - 이름이 바뀌었는지 확인");
                }

                WithNullServices(() =>
                {
                    bool success = (bool)method.Invoke(controller, null);
                    object collection = field.GetValue(controller);

                    // HashSet<T>는 비제네릭 System.Collections.ICollection을 구현하지 않아
                    // (List<T>와 달리) 직접 캐스팅이 안 된다 - Count 프로퍼티를 리플렉션으로
                    // 읽어 컬렉션 구체 타입과 무관하게(_aliveMonsters/HashSet, _activeZones/List
                    // 둘 다) 동일하게 처리한다.
                    int count = (int)collection.GetType().GetProperty("Count").GetValue(collection);

                    if (success || count > 0)
                    {
                        throw new Exception($"PoolManager 없이도 성공을 반환함(success={success}, count={count})");
                    }
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// CurrencyService(BigNumber 기반)가 이슈 #8의 재현 절차 그대로 음수/0 요청을 거부하고,
        /// 음수 초기값을 0으로 클램프하는지 확인한다.
        /// </summary>
        private static void CheckCurrencyServiceRejectsNonPositiveAmounts()
        {
            var events = new Core.EventBus();
            var service = new CurrencyService(events, 10);

            if (service.TrySpendGold(-5))
            {
                throw new Exception("TrySpendGold(-5)가 성공함(재화 증가 버그)");
            }

            if (service.CurrentGold != (Core.BigNumber)10)
            {
                throw new Exception($"음수 소비 시도 후 잔액이 바뀜: {service.CurrentGold}");
            }

            if (service.TrySpendGold(Core.BigNumber.Zero))
            {
                throw new Exception("TrySpendGold(0)이 성공함");
            }

            if (service.CanAfford(-5))
            {
                throw new Exception("CanAfford(-5)가 true를 반환함");
            }

            service.AddGold(-3);

            if (service.CurrentGold != (Core.BigNumber)10)
            {
                throw new Exception($"AddGold(-3) 이후 잔액이 바뀜: {service.CurrentGold}");
            }

            var restored = new CurrencyService(events, -100);

            if (restored.CurrentGold != Core.BigNumber.Zero)
            {
                throw new Exception($"음수 초기값이 0으로 클램프되지 않음: {restored.CurrentGold}");
            }
        }

        private static void CheckEnhancementStoneServiceRejectsNonPositiveAmounts()
        {
            var events = new Core.EventBus();
            var service = new EnhancementStoneService(events, 10);

            if (service.TrySpendStones(-5))
            {
                throw new Exception("TrySpendStones(-5)가 성공함(재화 증가 버그)");
            }

            if (service.CurrentStones != 10)
            {
                throw new Exception($"음수 소비 시도 후 잔액이 바뀜: {service.CurrentStones}");
            }

            if (service.TrySpendStones(0))
            {
                throw new Exception("TrySpendStones(0)이 성공함");
            }

            service.AddStones(-3);

            if (service.CurrentStones != 10)
            {
                throw new Exception($"AddStones(-3) 이후 잔액이 바뀜: {service.CurrentStones}");
            }

            var restored = new EnhancementStoneService(events, -100);

            if (restored.CurrentStones != 0)
            {
                throw new Exception($"음수 초기값이 0으로 클램프되지 않음: {restored.CurrentStones}");
            }
        }

        private static void CheckSoldierTicketServiceRejectsNonPositiveAmounts()
        {
            var events = new Core.EventBus();
            var service = new SoldierTicketService(events, 10);

            if (service.TrySpendTickets(-5))
            {
                throw new Exception("TrySpendTickets(-5)가 성공함(재화 증가 버그)");
            }

            if (service.CurrentTickets != 10)
            {
                throw new Exception($"음수 소비 시도 후 잔액이 바뀜: {service.CurrentTickets}");
            }

            if (service.TrySpendTickets(0))
            {
                throw new Exception("TrySpendTickets(0)이 성공함");
            }

            service.AddTickets(-3);

            if (service.CurrentTickets != 10)
            {
                throw new Exception($"AddTickets(-3) 이후 잔액이 바뀜: {service.CurrentTickets}");
            }

            var restored = new SoldierTicketService(events, -100);

            if (restored.CurrentTickets != 0)
            {
                throw new Exception($"음수 초기값이 0으로 클램프되지 않음: {restored.CurrentTickets}");
            }
        }

        private static void CheckSkillScrollServiceRejectsNonPositiveAmounts()
        {
            var events = new Core.EventBus();
            var service = new SkillScrollService(events, 10);

            if (service.TrySpendScrolls(-5))
            {
                throw new Exception("TrySpendScrolls(-5)가 성공함(재화 증가 버그)");
            }

            if (service.CurrentScrolls != 10)
            {
                throw new Exception($"음수 소비 시도 후 잔액이 바뀜: {service.CurrentScrolls}");
            }

            if (service.TrySpendScrolls(0))
            {
                throw new Exception("TrySpendScrolls(0)이 성공함");
            }

            service.AddScrolls(-3);

            if (service.CurrentScrolls != 10)
            {
                throw new Exception($"AddScrolls(-3) 이후 잔액이 바뀜: {service.CurrentScrolls}");
            }

            var restored = new SkillScrollService(events, -100);

            if (restored.CurrentScrolls != 0)
            {
                throw new Exception($"음수 초기값이 0으로 클램프되지 않음: {restored.CurrentScrolls}");
            }
        }

        private static void CheckEquipmentGachaTicketServiceRejectsNonPositiveAmounts()
        {
            var events = new Core.EventBus();
            var service = new EquipmentGachaTicketService(events, 10);

            if (service.TrySpendTickets(-5))
            {
                throw new Exception("TrySpendTickets(-5)가 성공함(재화 증가 버그)");
            }

            if (service.CurrentTickets != 10)
            {
                throw new Exception($"음수 소비 시도 후 잔액이 바뀜: {service.CurrentTickets}");
            }

            if (service.TrySpendTickets(0))
            {
                throw new Exception("TrySpendTickets(0)이 성공함");
            }

            service.AddTickets(-3);

            if (service.CurrentTickets != 10)
            {
                throw new Exception($"AddTickets(-3) 이후 잔액이 바뀜: {service.CurrentTickets}");
            }

            var restored = new EquipmentGachaTicketService(events, -100);

            if (restored.CurrentTickets != 0)
            {
                throw new Exception($"음수 초기값이 0으로 클램프되지 않음: {restored.CurrentTickets}");
            }
        }

        private static void CheckBossTokenServiceRejectsNonPositiveAmounts()
        {
            var events = new Core.EventBus();
            var service = new BossTokenService(events, 10);

            if (service.TrySpendTokens(-5))
            {
                throw new Exception("TrySpendTokens(-5)가 성공함(재화 증가 버그)");
            }

            if (service.CurrentTokens != 10)
            {
                throw new Exception($"음수 소비 시도 후 잔액이 바뀜: {service.CurrentTokens}");
            }

            if (service.TrySpendTokens(0))
            {
                throw new Exception("TrySpendTokens(0)이 성공함");
            }

            service.AddTokens(-3);

            if (service.CurrentTokens != 10)
            {
                throw new Exception($"AddTokens(-3) 이후 잔액이 바뀜: {service.CurrentTokens}");
            }

            var restored = new BossTokenService(events, -100);

            if (restored.CurrentTokens != 0)
            {
                throw new Exception($"음수 초기값이 0으로 클램프되지 않음: {restored.CurrentTokens}");
            }
        }

        /// <summary>
        /// EquipmentCatalogSO.FindByStableId가 실제로 StableId 기준으로 정확히 항목을 찾고,
        /// 빈 문자열/알 수 없는 값은 null을 반환하는지 확인한다. 순수 메모리상 인스턴스로만
        /// 검증하므로 실제 프로젝트 콘텐츠(에셋)와는 무관하다 - 그건 AssertNoDuplicateOrEmptyStableIds가
        /// 별도로 확인한다.
        /// </summary>
        private static void CheckEquipmentCatalogFindByStableId()
        {
            var itemA = ScriptableObject.CreateInstance<EquipmentSO>();
            SetPrivateString(itemA, "stableId", "guid-a");

            var itemB = ScriptableObject.CreateInstance<EquipmentSO>();
            SetPrivateString(itemB, "stableId", "guid-b");

            var catalog = ScriptableObject.CreateInstance<EquipmentCatalogSO>();
            SetPrivateField(catalog, "items", new[] { itemA, itemB });

            if (catalog.FindByStableId("guid-b") != itemB)
            {
                throw new Exception("일치하는 StableId로 항목을 찾지 못함");
            }

            if (catalog.FindByStableId("guid-does-not-exist") != null)
            {
                throw new Exception("존재하지 않는 StableId가 항목을 반환함");
            }

            if (catalog.FindByStableId("") != null || catalog.FindByStableId(null) != null)
            {
                throw new Exception("빈/null StableId가 항목을 반환함");
            }
        }

        /// <summary>
        /// T 타입 카탈로그 에셋을 프로젝트에서 실제로 찾아(정확히 하나여야 함) 그 안의 모든 항목의
        /// StableId가 비어있지 않고 서로 중복되지 않는지 확인한다(GitHub 이슈 #19 완료 조건
        /// "중복/빈/삭제된 ID는 빌드 전 실패 처리"). StableIdBackfill을 실행하지 않았거나, 새
        /// 항목을 손으로 추가하면서 StableId를 안 채운 경우를 잡아낸다.
        /// </summary>
        private static void AssertNoDuplicateOrEmptyStableIds<TCatalog, TItem>(
            Func<TCatalog, TItem[]> getItems, Func<TItem, string> getStableId)
            where TCatalog : UnityEngine.Object
            where TItem : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(TCatalog).Name}");

            if (guids.Length == 0)
            {
                throw new Exception($"{typeof(TCatalog).Name} 에셋을 프로젝트에서 찾지 못함");
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<TCatalog>(path);
                TItem[] items = getItems(catalog);

                if (items == null)
                {
                    continue;
                }

                var seen = new HashSet<string>();

                foreach (TItem item in items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    string stableId = getStableId(item);

                    if (string.IsNullOrEmpty(stableId))
                    {
                        throw new Exception($"{path}의 '{item.name}' 항목에 StableId가 비어있음 - StableIdBackfill을 다시 실행할 것");
                    }

                    if (!seen.Add(stableId))
                    {
                        throw new Exception($"{path}에 StableId '{stableId}'가 중복됨");
                    }
                }
            }
        }

        /// <summary>
        /// 두 UI.GachaResultRevealController가 같은 슬롯 프리팹 풀을 공유할 때, 하나가
        /// 비활성화되며 스폰해둔 슬롯을 반납하면 다른 하나가 새로 Instantiate하지 않고 그
        /// 인스턴스를 재사용하는지 확인한다(GitHub 이슈 #10 - 반납이 없으면 씬 전체 인스턴스
        /// 수가 컨트롤러 전환마다 계속 늘어난다). 실제 Play Mode에서 300개 규모로 이미 검증했고
        /// (씬 전체 인스턴스 수가 300으로 고정됨을 확인), 여기서는 같은 로직을 더 작은 규모(5개)로
        /// Edit Mode에서도 회귀 검사로 남긴다.
        /// </summary>
        private static void CheckGachaResultRevealControllerReleasesOnDisable()
        {
            const int slotCount = 5;

            string prefabPath = "Assets/04. Prefab/UI/GachaResultSlot.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null || !prefab.TryGetComponent(out UI.GachaResultSlotUI slotPrefab))
            {
                throw new Exception($"{prefabPath}에서 UI.GachaResultSlotUI 프리팹을 찾지 못함 - 경로/컴포넌트가 바뀌었는지 확인");
            }

            var pool = new Managers.PoolManager();
            pool.Initialize();

            // 씬에 이미 다른(무관한) GachaResultSlotUI가 떠 있을 수 있으므로(예: Play Mode 중
            // 실제 가챠 UI가 열려 있는 상태에서 이 검사를 돌리는 경우) 절대 개수 대신 이 검사가
            // 시작하기 전 대비 증가분(델타)만 확인한다 - 앰비언트 상태와 무관하게 안전하다.
            // EnsurePool 자체가 defaultCapacity만큼 미리 생성(prewarm)하므로, 반드시 그 전에 측정한다.
            int baseline = CountSceneSlots();
            pool.EnsurePool(prefab, slotCount, slotCount);

            GameObject controllerAGo = null;
            GameObject controllerBGo = null;

            try
            {
                var visuals = new UI.GachaResultVisual[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    visuals[i] = new UI.GachaResultVisual(null, Color.white);
                }

                controllerAGo = CreateRevealController(slotPrefab, out UI.GachaResultRevealController controllerA);
                SetPrivateField(controllerA, "_pool", pool);
                SpawnAllSlots(controllerA, visuals);

                int deltaAfterA = CountSceneSlots() - baseline;

                if (deltaAfterA != slotCount)
                {
                    throw new Exception($"A가 {slotCount}개 스폰한 직후 씬 전체 슬롯 증가분이 {deltaAfterA}(기대={slotCount})");
                }

                InvokeOnDisable(controllerA);

                controllerBGo = CreateRevealController(slotPrefab, out UI.GachaResultRevealController controllerB);
                SetPrivateField(controllerB, "_pool", pool);
                SpawnAllSlots(controllerB, visuals);

                int deltaAfterB = CountSceneSlots() - baseline;

                if (deltaAfterB != slotCount)
                {
                    throw new Exception($"A가 반납한 뒤 B가 {slotCount}개를 다시 스폰했는데 씬 전체 슬롯 증가분이 {deltaAfterB}(기대={slotCount}, 반납이 안 돼 새로 Instantiate된 것으로 보임)");
                }

                InvokeOnDisable(controllerB);
            }
            finally
            {
                if (controllerAGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(controllerAGo);
                }

                if (controllerBGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(controllerBGo);
                }

                // PoolManager.Shutdown()은 UnityEngine.Object.Destroy를 쓰는데, 이는 Play Mode
                // 전용이라 이 검사가 Edit Mode에서 돌 때 콘솔에 에러를 남긴다(정상 동작 - 실제
                // 게임에서 Shutdown은 항상 Play Mode 종료 시에만 불린다) - 대신 풀 루트를
                // DestroyImmediate로 직접 정리한다.
                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        private static GameObject CreateRevealController(UI.GachaResultSlotUI slotPrefab, out UI.GachaResultRevealController controller)
        {
            var go = new GameObject("RegressionCheck_GachaResultRevealController");
            go.SetActive(false); // OnEnable이 즉시 실행되지 않게(_pool을 직접 주입할 것이므로 불필요)
            go.AddComponent<RectTransform>();
            go.AddComponent<UnityEngine.UI.ScrollRect>();
            controller = go.AddComponent<UI.GachaResultRevealController>();

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(go.transform);

            SetPrivateField(controller, "content", contentGo.transform);
            SetPrivateField(controller, "slotPrefab", slotPrefab);

            return go;
        }

        private static void SpawnAllSlots(UI.GachaResultRevealController controller, UI.GachaResultVisual[] visuals)
        {
            SetPrivateField(controller, "_pending", visuals);
            SetPrivateField(controller, "_nextIndex", 0);

            MethodInfo spawnNext = typeof(UI.GachaResultRevealController).GetMethod("SpawnNext", BindingFlags.NonPublic | BindingFlags.Instance);

            for (int i = 0; i < visuals.Length; i++)
            {
                spawnNext.Invoke(controller, null);
            }

            SetPrivateField(controller, "_nextIndex", visuals.Length);
        }

        private static void InvokeOnDisable(UI.GachaResultRevealController controller)
        {
            MethodInfo onDisable = typeof(UI.GachaResultRevealController).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
            onDisable.Invoke(controller, null);
        }

        private static int CountSceneSlots()
        {
            return UnityEngine.Object.FindObjectsByType<UI.GachaResultSlotUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        private static void CheckSkillGachaTableEntriesCached()
        {
            SkillCatalogSO catalog = null;
            SkillSO skill = null;
            SkillGachaTableSO table = null;

            try
            {
                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);

                SkillGachaPoolEntry[] first = table.Entries;
                SkillGachaPoolEntry[] second = table.Entries;

                if (!ReferenceEquals(first, second))
                {
                    throw new Exception("Entries가 접근할 때마다 새 배열을 만듦 - 캐싱되지 않음(300연 뽑기 시 시도마다 카탈로그 전체를 다시 순회하는 회귀, GitHub 이슈 #21)");
                }
            }
            finally
            {
                if (table != null) UnityEngine.Object.DestroyImmediate(table);
                if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        /// <summary>
        /// 실사용 중 실제 프로젝트 에셋(SkillGachaTable_Normal.asset)에서 발견된 회귀 - catalog가
        /// 최초 접근 시점에 비어있으면(에디터 세션 안에서 일시적으로 그런 순간이 생길 수 있음,
        /// 예: Domain Reload를 건너뛰는 Play Mode 설정) 빈 배열이 캐싱돼버려, 이후 catalog에
        /// 실제 스킬이 채워져도 Entries가 영원히 빈 채로 남았다(뽑기 후보가 0개로 보임). 빈
        /// 결과는 캐싱하지 않아야 다음 접근에서 다시 계산해 회복된다.
        /// </summary>
        private static void CheckSkillGachaTableEntriesDoesNotCacheEmptyResult()
        {
            SkillCatalogSO catalog = null;
            SkillSO skill = null;
            SkillGachaTableSO table = null;

            try
            {
                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", System.Array.Empty<SkillSO>());

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);

                SkillGachaPoolEntry[] whileEmpty = table.Entries;

                if (whileEmpty.Length != 0)
                {
                    throw new Exception("빈 카탈로그인데 Entries가 비어있지 않음");
                }

                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                SkillGachaPoolEntry[] afterFilled = table.Entries;

                if (afterFilled.Length != 1)
                {
                    throw new Exception($"카탈로그가 뒤늦게 채워졌는데도 Entries가 여전히 비어있음(길이={afterFilled.Length}) - 빈 결과가 잘못 캐싱된 회귀");
                }
            }
            finally
            {
                if (table != null) UnityEngine.Object.DestroyImmediate(table);
                if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        /// <summary>
        /// GitHub 이슈 #21: SkillGachaService.TryPullOne이 후보 목록(List&lt;SkillGachaPoolEntry&gt;)을
        /// 매개변수로 받는 시그니처인지(= Pull() 배치 시작 시점에 한 번만 계산해 넘겨받는 구조인지)를
        /// 구조적으로 확인한다. 순수 동작 결과만으로는 "배치당 1회 계산"과 "시도마다 재계산"을 구별할
        /// 수 없다 - 가챠는 AddCopy만 호출해 레벨이 바뀌지 않으므로(IsMaxLevel 판정 불변) 두 방식의
        /// 최종 결과가 항상 동일하기 때문이다(SkillGachaService.BuildLevelableCandidates doc 참고).
        /// 이어서 실제 Pull() 호출도 함께 수행해, 미리 계산된 후보 목록을 넘겨받는 구조로 바뀐 뒤에도
        /// 정상적으로 뽑기가 동작하는지 확인한다.
        /// </summary>
        private static void CheckSkillGachaServiceCandidatesBuiltOncePerBatch()
        {
            MethodInfo tryPullOne = typeof(SkillGachaService).GetMethod("TryPullOne", BindingFlags.NonPublic | BindingFlags.Instance);

            if (tryPullOne == null)
            {
                throw new Exception("TryPullOne 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            ParameterInfo[] parameters = tryPullOne.GetParameters();

            if (parameters.Length < 2 || parameters[1].ParameterType != typeof(List<SkillGachaPoolEntry>))
            {
                throw new Exception("TryPullOne이 List<SkillGachaPoolEntry> 후보 목록을 매개변수로 받지 않음 - 배치당 1회 계산 구조가 되돌려졌을 수 있음");
            }

            SkillCatalogSO catalog = null;
            SkillSO skill = null;
            SkillGachaTableSO table = null;

            try
            {
                var events = new EventBus();

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);
                SetPrivateField(table, "ticketCostPerPull", 1);
                SetPrivateField(table, "currencyType", GachaCurrencyType.Ticket);

                var skillService = new SkillService(events);
                var scrolls = new SkillScrollService(events, initialScrolls: 100);
                var currency = new CurrencyService(events);
                var service = new SkillGachaService(events, scrolls, currency, skillService, new[] { table });

                IReadOnlyList<SkillSO> results = service.Pull(0, 5);

                if (results.Count != 5)
                {
                    throw new Exception($"주문서 100개(1회당 1개)로 5회 뽑기를 시도했는데 성공 횟수가 {results.Count}(기대=5)");
                }

                if (skillService.GetCount(skill) != 5)
                {
                    throw new Exception($"5회 성공했는데 보유 개수가 {skillService.GetCount(skill)}(기대=5) - AddCopy 누락 의심");
                }
            }
            finally
            {
                if (table != null) UnityEngine.Object.DestroyImmediate(table);
                if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        /// <summary>
        /// GitHub 이슈 #21: OnInventoryChanged/OnEquipmentEquipped 핸들러가 이벤트를 받는 즉시
        /// RebuildInventorySnapshot()을 동기 호출하지 않고 더티 플래그만 세우는지(회귀 시 아래 null
        /// 서비스 참조에서 NullReferenceException으로 즉시 드러난다), 그리고 Tick()이 그 플래그를
        /// 소비해 실제로 한 번 재직렬화한 뒤 플래그를 되돌리는지를 확인한다. _isDirty는 핸들러 호출
        /// 직후 리플렉션으로 강제로 false를 되돌려, Tick()이 실제 PlayerPrefs.Save()(디스크 flush)를
        /// 타지 않도록 막는다 - SaveService를 Initialize() 없이(로드된 실제 세이브 값이 아닌 C# 기본값
        /// 상태로) 직접 구성했으므로, Save()가 그대로 불리면 진짜 유저 세이브 데이터를 기본값으로
        /// 덮어써버릴 위험이 있다.
        /// </summary>
        private static void CheckSaveServiceInventoryHandlersDeferRebuildToTick()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            var equippedGear = new EquippedGearService(events);
            var saveService = new SaveService(events, inventory, equippedGear, null, null, null, null, null, null, null, null, null);

            const string sentinel = "REGRESSION_CHECK_SENTINEL";
            SetPrivateFieldOnPlainObject(saveService, "_inventoryJson", sentinel);

            InvokeVoidHandler(saveService, "OnInventoryChanged");

            if (!(bool)GetPrivateFieldOnPlainObject(saveService, "_isInventorySnapshotDirty"))
            {
                throw new Exception("OnInventoryChanged 호출 후 _isInventorySnapshotDirty가 true가 아님");
            }

            if (!(bool)GetPrivateFieldOnPlainObject(saveService, "_isDirty"))
            {
                throw new Exception("OnInventoryChanged 호출 후 _isDirty가 true가 아님(MarkDirty 누락 의심)");
            }

            string jsonAfterHandler = (string)GetPrivateFieldOnPlainObject(saveService, "_inventoryJson");

            if (jsonAfterHandler != sentinel)
            {
                throw new Exception("OnInventoryChanged 핸들러가 이벤트를 받는 즉시 RebuildInventorySnapshot()을 동기 호출함 - 300연 뽑기 시 시도마다 전체 인벤토리를 재직렬화하는 회귀(GitHub 이슈 #21)");
            }

            InvokeVoidHandler(saveService, "OnEquipmentEquipped");

            // Tick()이 진짜 PlayerPrefs.Save()(디스크 flush)까지 타지 않도록, 여기서만 강제로
            // _isDirty를 꺼둔다 - 클래스 doc에 남긴 대로 실제 게임에서는 이 필드가 항상 로드된
            // 세이브 값으로 시작하지만, 이 검사는 그 초기화(Initialize/Load)를 건너뛰었기 때문이다.
            SetPrivateFieldOnPlainObject(saveService, "_isDirty", false);

            ((ITickable)saveService).Tick(0f);

            if ((bool)GetPrivateFieldOnPlainObject(saveService, "_isInventorySnapshotDirty"))
            {
                throw new Exception("Tick() 이후에도 _isInventorySnapshotDirty가 true로 남아있음 - 플래그가 소비되지 않음");
            }

            string jsonAfterTick = (string)GetPrivateFieldOnPlainObject(saveService, "_inventoryJson");

            if (jsonAfterTick == sentinel)
            {
                throw new Exception("Tick() 호출 후에도 _inventoryJson이 그대로 - RebuildInventorySnapshot()이 실행되지 않음");
            }
        }

        /// <summary>
        /// GitHub 이슈 #21: 병사 로스터(3개 이벤트 소스)/스킬 레벨/스킬 보유 개수 핸들러도 인벤토리와
        /// 동일하게 즉시 재직렬화하지 않고 더티 플래그만 세우는지 확인한다. 각 Rebuild*Snapshot이
        /// null 서비스 참조를 요구하므로, 핸들러가 실수로 그 자리에서 직접 호출하면 이 검사 자체가
        /// NullReferenceException으로 즉시 실패한다 - 별도의 스파이/카운터 없이도 "핸들러가 재직렬화를
        /// Tick으로 미뤘는지"를 검증할 수 있는 이유.
        /// </summary>
        private static void CheckSaveServiceOtherHandlersDeferRebuildToTick()
        {
            var events = new EventBus();
            var saveService = new SaveService(events, null, null, null, null, null, null, null, null, null, null, null);

            AssertHandlerSetsDirtyFlagWithoutRebuilding(saveService, "OnSoldierRosterChanged", "_isSoldierRosterSnapshotDirty");
            AssertHandlerSetsDirtyFlagWithoutRebuilding(saveService, "OnSoldierDeploymentChanged", "_isSoldierRosterSnapshotDirty");
            AssertHandlerSetsDirtyFlagWithoutRebuilding(saveService, "OnSoldierBehaviorProfileChanged", "_isSoldierRosterSnapshotDirty");
            AssertHandlerSetsDirtyFlagWithoutRebuilding(saveService, "OnSkillLeveledUp", "_isSkillLevelsSnapshotDirty");
            AssertHandlerSetsDirtyFlagWithoutRebuilding(saveService, "OnSkillCountChanged", "_isSkillCountsSnapshotDirty");
        }

        private static void AssertHandlerSetsDirtyFlagWithoutRebuilding(SaveService saveService, string handlerName, string dirtyFieldName)
        {
            SetPrivateFieldOnPlainObject(saveService, dirtyFieldName, false);

            InvokeVoidHandler(saveService, handlerName);

            if (!(bool)GetPrivateFieldOnPlainObject(saveService, dirtyFieldName))
            {
                throw new Exception($"{handlerName} 호출 후 {dirtyFieldName}이 true가 아님");
            }
        }

        private static void CheckGachaPullToastFullSuccessNoToast()
        {
            var events = new EventBus();
            var toasts = new List<string>();
            events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

            GachaPullToast.PublishIfIncomplete(events, 5, 5, GachaPullStopReason.InsufficientCurrency, "후보 없음");

            if (toasts.Count != 0)
            {
                throw new Exception($"요청한 횟수를 전부 성공했는데 토스트가 발행됨: [{string.Join(", ", toasts)}]");
            }
        }

        private static void CheckGachaPullToastZeroSuccessInsufficientCurrency()
        {
            var events = new EventBus();
            var toasts = new List<string>();
            events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

            GachaPullToast.PublishIfIncomplete(events, 0, 300, GachaPullStopReason.InsufficientCurrency, "후보 없음");

            if (toasts.Count != 1 || toasts[0] != "재화가 모자랍니다.")
            {
                throw new Exception($"0회 성공 + 재화부족 토스트가 기대와 다름: [{string.Join(", ", toasts)}]");
            }
        }

        /// <summary>
        /// GitHub 이슈 #22 재현 사례 A(주문서 4개로 300회 요청 → 4회만 실행, 296회 미실행 이유
        /// 안내 없음)를 GachaPullToast 단위로 재현한다.
        /// </summary>
        private static void CheckGachaPullToastPartialSuccessInsufficientCurrency()
        {
            var events = new EventBus();
            var toasts = new List<string>();
            events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

            GachaPullToast.PublishIfIncomplete(events, 4, 300, GachaPullStopReason.InsufficientCurrency, "후보 없음");

            if (toasts.Count != 1 || toasts[0] != "재화가 모자라 4/300회만 뽑았습니다.")
            {
                throw new Exception($"부분 성공(4/300) 토스트가 기대와 다름: [{string.Join(", ", toasts)}]");
            }
        }

        /// <summary>
        /// GitHub 이슈 #22 재현 사례 B(주문서 1000개로 300회 요청 → 0건 반환, 아무 설명 없이 종료)를
        /// GachaPullToast 단위로 재현한다.
        /// </summary>
        private static void CheckGachaPullToastNoCandidatesZeroSuccess()
        {
            var events = new EventBus();
            var toasts = new List<string>();
            events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

            GachaPullToast.PublishIfIncomplete(events, 0, 300, GachaPullStopReason.NoCandidates, "모든 스킬이 최대 레벨입니다.");

            if (toasts.Count != 1 || toasts[0] != "모든 스킬이 최대 레벨입니다.")
            {
                throw new Exception($"후보 없음(0/300) 토스트가 기대와 다름: [{string.Join(", ", toasts)}]");
            }
        }

        private static void CheckGachaAffordabilityCalculatorFixedCost()
        {
            int affordable = GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(305, 100);

            if (affordable != 3)
            {
                throw new Exception($"305 / 100 = 3이어야 하는데 {affordable}");
            }

            if (GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(50, 100) != 0)
            {
                throw new Exception("1회분도 안 되는 잔액인데 0이 아님");
            }
        }

        private static void CheckGachaAffordabilityCalculatorEscalatingCost()
        {
            // 0회차 100골드, 이후 회차마다 +10(0,100,110,120,130,...) - 500골드로는
            // 100(누적100,1회)+110(210,2회)+120(330,3회)+130(460,4회)+140(600, 잔액 부족, 멈춤)
            // → 정확히 4회.
            int affordable = GachaAffordabilityCalculator.CalculateMaxAffordableGoldPulls(
                (BigNumber)500, 0, pulls => 100 + pulls * 10);

            if (affordable != 4)
            {
                throw new Exception($"누적 비용 시뮬레이션 결과가 4가 아니라 {affordable}");
            }
        }

        private static void CheckGachaAffordabilityCalculatorFixedCostCapsAtMaxSimulatedPulls()
        {
            // 고정 비용 1짜리를 아주 큰 잔액(1e30)으로 조회하면(고정 비용 나눗셈 빠른 경로) 상한에서 멈춰야 한다.
            int affordable = GachaAffordabilityCalculator.CalculateMaxAffordableGoldPulls(
                new BigNumber(1, 30), 0, _ => 1);

            if (affordable != GachaAffordabilityCalculator.MaxSimulatedPulls)
            {
                throw new Exception($"고정 비용 빠른 경로가 상한에서 안 멈춤: {affordable}(기대={GachaAffordabilityCalculator.MaxSimulatedPulls})");
            }
        }

        private static void CheckGachaAffordabilityCalculatorEscalatingCostCapsAtMaxSimulatedPulls()
        {
            // 회차마다 비용이 달라(1,2,3,...) 고정 비용 빠른 경로를 안 타고 실제로 시뮬레이션
            // 루프를 돈다 - 누적 비용(1e4회 기준 약 5천만)보다 훨씬 큰 잔액(1e20)이라 상한
            // (MaxSimulatedPulls)에서 멈춰야 한다.
            int affordable = GachaAffordabilityCalculator.CalculateMaxAffordableGoldPulls(
                new BigNumber(1, 20), 0, pulls => pulls + 1);

            if (affordable != GachaAffordabilityCalculator.MaxSimulatedPulls)
            {
                throw new Exception($"회차별 비용 시뮬레이션 루프가 상한에서 안 멈춤: {affordable}(기대={GachaAffordabilityCalculator.MaxSimulatedPulls})");
            }
        }

        /// <summary>
        /// GitHub 이슈 #22 재현 사례 A를 SkillGachaService.Pull() 전체 경로로 재현한다 - 주문서
        /// 4개만 보유한 채 300회를 요청하면 정확히 4회만 성공하고, "재화가 모자라 4/300회만
        /// 뽑았습니다." 토스트가 함께 발행돼야 한다.
        /// </summary>
        private static void CheckSkillGachaServiceInsufficientCurrencyPublishesPartialToast()
        {
            SkillCatalogSO catalog = null;
            SkillSO skill = null;
            SkillGachaTableSO table = null;

            try
            {
                var events = new EventBus();
                var toasts = new List<string>();
                events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);
                SetPrivateField(table, "ticketCostPerPull", 1);
                SetPrivateField(table, "currencyType", GachaCurrencyType.Ticket);

                var skillService = new SkillService(events);
                var scrolls = new SkillScrollService(events, initialScrolls: 4);
                var currency = new CurrencyService(events);
                var service = new SkillGachaService(events, scrolls, currency, skillService, new[] { table });

                IReadOnlyList<SkillSO> results = service.Pull(0, 300);

                if (results.Count != 4)
                {
                    throw new Exception($"주문서 4개로 300회 요청했는데 성공 횟수가 {results.Count}(기대=4)");
                }

                if (!toasts.Contains("재화가 모자라 4/300회만 뽑았습니다."))
                {
                    throw new Exception($"부분 성공 안내 토스트가 발행되지 않음: [{string.Join(", ", toasts)}]");
                }
            }
            finally
            {
                if (table != null) UnityEngine.Object.DestroyImmediate(table);
                if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        /// <summary>
        /// GitHub 이슈 #22 재현 사례 B를 SkillGachaService.Pull() 전체 경로로 재현한다 - 유일한
        /// 스킬이 항상 만렙(maxLevel=0)인 상태에서 주문서가 충분해도 300회 요청이 0건으로 끝나야
        /// 하고, "모든 스킬이 최대 레벨입니다." 토스트가 함께 발행돼야 한다.
        /// </summary>
        private static void CheckSkillGachaServiceAllMaxLevelPublishesNoCandidatesToast()
        {
            SkillCatalogSO catalog = null;
            SkillSO skill = null;
            SkillGachaTableSO table = null;

            try
            {
                var events = new EventBus();
                var toasts = new List<string>();
                events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateField(skill, "maxLevel", 0); // 레벨 0 >= 최대레벨 0 - 항상 만렙 취급
                SetPrivateField(catalog, "skills", new[] { skill });

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);
                SetPrivateField(table, "ticketCostPerPull", 1);
                SetPrivateField(table, "currencyType", GachaCurrencyType.Ticket);

                var skillService = new SkillService(events);
                var scrolls = new SkillScrollService(events, initialScrolls: 1000);
                var currency = new CurrencyService(events);
                var service = new SkillGachaService(events, scrolls, currency, skillService, new[] { table });

                if (service.HasAnyLevelableCandidate(0))
                {
                    throw new Exception("maxLevel=0인 스킬 하나뿐인데 HasAnyLevelableCandidate가 true를 반환함");
                }

                IReadOnlyList<SkillSO> results = service.Pull(0, 300);

                if (results.Count != 0)
                {
                    throw new Exception($"모든 스킬이 만렙인데 {results.Count}건이 성공함(기대=0)");
                }

                if (!toasts.Contains("모든 스킬이 최대 레벨입니다."))
                {
                    throw new Exception($"만렙 안내 토스트가 발행되지 않음: [{string.Join(", ", toasts)}]");
                }
            }
            finally
            {
                if (table != null) UnityEngine.Object.DestroyImmediate(table);
                if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        /// <summary>
        /// SkillGachaService.HasAnyLevelableCandidate가 티어별로 독립적으로 판정되는지 확인한다.
        /// 두 테이블(만렙 스킬만 있는 테이블 / 레벨업 가능한 스킬이 섞인 테이블)을 따로 만드는 이유:
        /// SkillGachaTableSO.Entries는 최초 접근 시 캐싱되므로(GitHub 이슈 #21), 같은 테이블의
        /// catalog 내용을 검사 도중 바꿔치기하면 첫 접근 때 캐싱된 옛 목록만 계속 보게 된다.
        /// </summary>
        private static void CheckSkillGachaServiceHasAnyLevelableCandidate()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var scrolls = new SkillScrollService(events);
            var currency = new CurrencyService(events);

            SkillSO maxedSkill = null;
            SkillSO leveledSkill = null;
            SkillCatalogSO allMaxedCatalog = null;
            SkillCatalogSO mixedCatalog = null;
            SkillGachaTableSO allMaxedTable = null;
            SkillGachaTableSO mixedTable = null;

            try
            {
                maxedSkill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateField(maxedSkill, "maxLevel", 0);
                leveledSkill = ScriptableObject.CreateInstance<SkillSO>();

                allMaxedCatalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(allMaxedCatalog, "skills", new[] { maxedSkill });
                allMaxedTable = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(allMaxedTable, "catalog", allMaxedCatalog);
                SetPrivateField(allMaxedTable, "weightPerSkill", 1);

                mixedCatalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(mixedCatalog, "skills", new[] { maxedSkill, leveledSkill });
                mixedTable = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(mixedTable, "catalog", mixedCatalog);
                SetPrivateField(mixedTable, "weightPerSkill", 1);

                var service = new SkillGachaService(events, scrolls, currency, skillService, new[] { allMaxedTable, mixedTable });

                if (service.HasAnyLevelableCandidate(0))
                {
                    throw new Exception("만렙 스킬만 있는 테이블(티어 0)에서 HasAnyLevelableCandidate가 true");
                }

                if (!service.HasAnyLevelableCandidate(1))
                {
                    throw new Exception("레벨업 가능한 스킬이 섞인 테이블(티어 1)에서 HasAnyLevelableCandidate가 false");
                }
            }
            finally
            {
                if (allMaxedTable != null) UnityEngine.Object.DestroyImmediate(allMaxedTable);
                if (mixedTable != null) UnityEngine.Object.DestroyImmediate(mixedTable);
                if (allMaxedCatalog != null) UnityEngine.Object.DestroyImmediate(allMaxedCatalog);
                if (mixedCatalog != null) UnityEngine.Object.DestroyImmediate(mixedCatalog);
                if (maxedSkill != null) UnityEngine.Object.DestroyImmediate(maxedSkill);
                if (leveledSkill != null) UnityEngine.Object.DestroyImmediate(leveledSkill);
            }
        }

        /// <summary>
        /// name 매개변수 하나짜리(또는 매개변수 없는) private 인스턴스 메서드를 리플렉션으로 호출한다.
        /// 매개변수가 있으면 그 타입의 기본값(구조체는 전부 0/null인 zero 값)으로 채운다 - 이 검사가
        /// 다루는 핸들러들은 전부 evt 매개변수 자체를 쓰지 않고 플래그만 세우므로 기본값으로 충분하다.
        /// </summary>
        private static void InvokeVoidHandler(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new Exception($"{target.GetType().Name}.{methodName} 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            ParameterInfo[] parameters = method.GetParameters();
            object[] args = parameters.Length == 0
                ? Array.Empty<object>()
                : new[] { Activator.CreateInstance(parameters[0].ParameterType) };

            method.Invoke(target, args);
        }

        private static void SetPrivateFieldOnPlainObject(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new Exception($"필드 '{fieldName}'을 찾지 못함");
            }

            field.SetValue(target, value);
        }

        private static object GetPrivateFieldOnPlainObject(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new Exception($"필드 '{fieldName}'을 찾지 못함");
            }

            return field.GetValue(target);
        }

        private static void SetPrivateField(UnityEngine.Object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new Exception($"필드 '{fieldName}'을 찾지 못함");
            }

            field.SetValue(target, value);
        }

        private static void SetPrivateString(UnityEngine.Object target, string fieldName, string value)
        {
            SetPrivateField(target, fieldName, value);
        }

        private static void SetPrivateFloat(UnityEngine.Object target, string fieldName, float value)
        {
            SetPrivateField(target, fieldName, value);
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
