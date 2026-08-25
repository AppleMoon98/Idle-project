using System;
using System.Collections.Generic;
using System.Reflection;
using Character;
using Dungeon;
using Enhancement;
using Equipment;
using Gacha;
using Loot;
using Rank;
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
