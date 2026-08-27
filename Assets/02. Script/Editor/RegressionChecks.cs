using System;
using System.Collections.Generic;
using System.Reflection;
using Behavior;
using Character;
using Character.Events;
using Combat;
using Core;
using Core.Pooling;
using Dungeon;
using Enhancement;
using Equipment;
using Gacha;
using Inventory;
using Inventory.Events;
using Loot;
using Loot.Events;
using Offline;
using Rank;
using Save;
using Skill;
using Skill.Events;
using Soldier;
using Soldier.Events;
using Stage;
using Stage.Events;
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
    ///
    /// <para>
    /// **분류/실행 시간(GitHub 이슈 #14):** 이 파일의 검사는 전부 Edit Mode에서 동기적으로
    /// 완료된다 - Play Mode 진입이 필요한 검사는 없다(일부 검사가 Play Mode에서도 동일하게
    /// 동작하는지 별도로 라이브 확인한 이력은 있지만, RunAllChecks 자체는 Edit Mode 밖에서
    /// 돌 이유가 없다). 로컬 실측 실행 시간은 57건 기준 1초 미만(수 프레임 이내) - 전부
    /// ScriptableObject.CreateInstance/합성 GameObject/독립 EventBus로 격리된 순수 로직
    /// 검증이라 씬 로드/에셋 임포트 같은 무거운 단계가 없기 때문이다.
    /// </para>
    /// <para>
    /// **메뉴 실행과 배치 실행이 로직을 공유하는 이유(이슈 #14):** 메뉴에서 13건→57건으로
    /// 검사를 늘려온 과정에서, Unity Test Runner(NUnit)가 이 클래스를 전혀 스캔하지 못해
    /// EditMode/PlayMode 양쪽 다 "0건 발견, Passed"로 집계되는 문제가 있었다 - CI가 이
    /// "0건 성공"을 진짜 성공으로 오인하는 거짓 양성이었다. asmdef 재구성(중기 과제, 위
    /// 문단과 같은 이유로 이번엔 보류) 대신, 검사 실행 본문을 <see cref="RunAllChecks"/>로
    /// 뽑아 대화형 메뉴(<see cref="RunAll"/>)와 CI 배치 진입점(<see cref="RunAllForCI"/>)이
    /// 완전히 같은 로직을 실행하도록 했다 - 로직이 갈리면 "메뉴에서는 통과, CI에서는 다른
    /// 로직이 돌아 결과가 어긋남" 같은 새로운 이원화 문제가 생기기 때문. RunAllForCI는 검사
    /// 총 개수가 0이면(=RunAllChecks 자체가 실행되지 않았거나 검사 등록이 전부 빠진 상태)
    /// 그 자체를 실패로 취급하고, Application.isBatchMode일 때만 EditorApplication.Exit로
    /// 성공/실패를 프로세스 종료 코드에 반영한다(대화형 메뉴에서 실수로 호출돼도 에디터가
    /// 안 죽도록 배치 모드 여부로 가드).
    /// </para>
    /// </summary>
    internal static class RegressionChecks
    {
        [MenuItem("Idle Project/Run Regression Checks (Offline Reward)")]
        private static void RunAll()
        {
            RunAllChecks(out int total, out List<string> failures);
            LogResult(total, failures);
        }

        /// <summary>
        /// CI 배치 진입점 - <c>-executeMethod Editor.RegressionChecks.RunAllForCI</c>로 호출한다.
        /// RunAll()과 완전히 같은 <see cref="RunAllChecks"/> 로직을 돌리되, 검사 총 개수가 0이면
        /// (Test Runner가 이 클래스를 스캔하지 못해 "0건 성공"으로 집계되던 이슈 #14의 재현
        /// 조건과 동일) 그 자체를 실패로 취급한다. Application.isBatchMode일 때만
        /// EditorApplication.Exit를 호출해 종료 코드로 성공/실패를 알린다 - 대화형 에디터에서
        /// 메뉴 대신 이 메서드를 잘못 호출해도(리플렉션 등) 에디터가 강제 종료되지 않는다.
        /// </summary>
        public static void RunAllForCI()
        {
            RunAllChecks(out int total, out List<string> failures);
            LogResult(total, failures);

            bool hasNoChecks = total == 0;

            if (hasNoChecks)
            {
                Debug.LogError("[RegressionChecks] 검사가 0건 실행됨 - RunAllChecks 등록 목록이 비었거나 실행 자체가 실패한 것으로 보임(이슈 #14의 거짓 양성 조건과 동일).");
            }

            if (!Application.isBatchMode)
            {
                return;
            }

            bool succeeded = !hasNoChecks && failures.Count == 0;
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        private static void LogResult(int total, List<string> failures)
        {
            if (failures.Count == 0)
            {
                Debug.Log($"[RegressionChecks] 전부 통과 ({total}/{total}).");
            }
            else
            {
                Debug.LogError($"[RegressionChecks] {failures.Count}/{total}개 실패:\n" + string.Join("\n", failures));
            }
        }

        /// <summary>
        /// 실제 검사 등록/실행 본문. RunAll()(대화형 메뉴)과 RunAllForCI()(CI 배치 진입점)가
        /// 이 메서드 하나를 그대로 공유한다 - 두 진입점의 로직이 갈리지 않도록 하기 위함(이슈 #14).
        /// </summary>
        private static void RunAllChecks(out int total, out List<string> failures)
        {
            var localFailures = new List<string>();
            int localTotal = 0;

            void Check(string name, Action check)
            {
                localTotal++;
                try
                {
                    check();
                }
                catch (Exception e)
                {
                    localFailures.Add($"{name}: {e.Message}");
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
            Check("BigNumber_TryParse_OverflowExponentFallsBackToZeroWithoutThrowing", CheckBigNumberTryParseOverflow);
            Check("SaveService_ClampHelpers_NegativeAndBelowMinimumFallBackToSafeDefault", CheckSaveServiceClampHelpers);

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

            // --- 이슈 #20 추가 코멘트(2026-08-26): PracticeStageController가 같은 안티패턴을
            // 재발시킴(section GK 이후 신규 기능) ---
            Check("PracticeStageController_TryEnter_NoPoolManager_ReturnsFalse",
                CheckPracticeStageControllerTryEnterNoPoolManagerReturnsFalse);

            // --- 이슈 #8: 재화 서비스가 음수/0 소비·지급 요청을 거부(TrySpend*(-5)가 잔액을
            // 늘리는 버그 방지), 저장 복원 시 음수 초기값을 0으로 클램프 ---
            Check("CurrencyService_RejectsNonPositiveAmounts", CheckCurrencyServiceRejectsNonPositiveAmounts);
            Check("EnhancementStoneService_RejectsNonPositiveAmounts", CheckEnhancementStoneServiceRejectsNonPositiveAmounts);
            Check("SoldierTicketService_RejectsNonPositiveAmounts", CheckSoldierTicketServiceRejectsNonPositiveAmounts);
            Check("SkillScrollService_RejectsNonPositiveAmounts", CheckSkillScrollServiceRejectsNonPositiveAmounts);
            Check("EquipmentGachaTicketService_RejectsNonPositiveAmounts", CheckEquipmentGachaTicketServiceRejectsNonPositiveAmounts);
            Check("BossTokenService_RejectsNonPositiveAmounts", CheckBossTokenServiceRejectsNonPositiveAmounts);
            Check("ContentCostValidation_ProjectAssets_NoNegativeCosts", CheckContentCostValidationProjectAssets);

            // --- 이슈 #45: 정수형 보상 재화 5종이 int.MaxValue 근처에서 순수 int 덧셈으로
            // 오버플로해 음수로 반전되던 문제 - long 중간 계산 + int.MaxValue saturate로 수정 ---
            Check("EnhancementStoneService_AddStones_SaturatesAtIntMaxValue", CheckEnhancementStoneServiceSaturatesAtIntMaxValue);
            Check("SoldierTicketService_AddTickets_SaturatesAtIntMaxValue", CheckSoldierTicketServiceSaturatesAtIntMaxValue);
            Check("SkillScrollService_AddScrolls_SaturatesAtIntMaxValue", CheckSkillScrollServiceSaturatesAtIntMaxValue);
            Check("EquipmentGachaTicketService_AddTickets_SaturatesAtIntMaxValue", CheckEquipmentGachaTicketServiceSaturatesAtIntMaxValue);
            Check("BossTokenService_AddTokens_SaturatesAtIntMaxValue", CheckBossTokenServiceSaturatesAtIntMaxValue);

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
            Check("GachaResultRevealController_OnEnable_RestoresAlreadyRevealedSlotsInstantly",
                CheckGachaResultRevealControllerRestoresOnReenable);

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

            // --- 이슈 #21 추가 조치: 이벤트 소스 자체(SoldierRosterService/GachaService/
            // SkillGachaService)를 배치화해 다운스트림 디바운스에만 의존하지 않도록 함 +
            // CPU/GC 예산을 아이템당 할당량 비율로 측정하는 성능 회귀 검사 ---
            Check("SoldierRosterService_AddSoldiersBatch_PublishesEventOnce",
                CheckSoldierRosterServiceAddSoldiersBatchPublishesEventOnce);
            Check("GachaService_Pull_AddsSoldiersAsSingleBatch",
                CheckGachaServicePullAddsSoldiersAsSingleBatch);
            Check("GachaService_Pull300_DoesNotScaleSuperlinearly_GcAllocationBudget",
                CheckGachaServicePull300DoesNotScaleSuperlinearly);
            Check("SkillGachaService_Pull_AggregatesAddCopyByDefinition",
                CheckSkillGachaServicePullAggregatesAddCopyByDefinition);

            // --- 이슈 #22: 300회 요청의 부분 성공/전체 실패가 이유·실행 수 안내 없이 조용히 끝남 ---
            Check("GachaPullToast_FullSuccess_NoToast", CheckGachaPullToastFullSuccessNoToast);
            Check("GachaPullToast_ZeroSuccess_InsufficientCurrency_GenericMessage",
                CheckGachaPullToastZeroSuccessInsufficientCurrency);
            Check("GachaPullToast_PartialSuccess_InsufficientCurrency_IncludesCounts",
                CheckGachaPullToastPartialSuccessInsufficientCurrency);
            Check("GachaPullToast_NoCandidates_ZeroSuccess_UsesProvidedMessage",
                CheckGachaPullToastNoCandidatesZeroSuccess);
            Check("GachaPullToast_AllCandidatesMaxed_ZeroSuccess_UsesAllMaxedMessage",
                CheckGachaPullToastAllCandidatesMaxedZeroSuccess);
            Check("GachaAffordabilityCalculator_FixedCost_ExactDivision",
                CheckGachaAffordabilityCalculatorFixedCost);
            Check("GachaAffordabilityCalculator_FixedCost_ZeroOffByOneExactAndOverBalanceBoundaries",
                CheckGachaAffordabilityCalculatorFixedCostBoundaries);
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
            Check("SkillGachaService_Pull_EmptyCatalog_PublishesDataErrorToast_NotAllMaxedToast",
                CheckSkillGachaServiceEmptyCatalogPublishesDataErrorToast);
            Check("SkillGachaService_HasAnyLevelableCandidate_ReflectsPerTierMaxLevelState",
                CheckSkillGachaServiceHasAnyLevelableCandidate);

            // --- 이슈 #23: CharacterSeparation/NearestHealthScan이 틱마다 Physics2D.OverlapXAll로
            // 배열을 새로 할당하고(캐릭터 34개 프리팹, 헬퍼 호출부 16곳), FindNearest 계열은
            // 캡처 람다까지 추가로 할당하던 지속적인 GC 압력을 NonAlloc 전환으로 해소 ---
            Check("NearestHealthScan_FindNearest_CorrectAndZeroAllocAfterWarmup",
                CheckNearestHealthScanFindNearestCorrectAndZeroAlloc);
            Check("NearestHealthScan_BufferGrowth_PreservesCorrectness",
                CheckNearestHealthScanBufferGrowthPreservesCorrectness);
            Check("CharacterSeparation_PushesApartAndZeroAllocAfterWarmup",
                CheckCharacterSeparationPushesApartAndZeroAlloc);

            // --- 이슈 #12: HUD/팝업이 세로(1080x1920) 전제로만 설계돼 있는데 PlayerSettings가
            // 가로 자동회전까지 허용해 실기기 회전 시 레이아웃이 겹치던 문제 ---
            Check("PlayerSettings_Orientation_LockedToPortraitOnly", CheckPlayerSettingsOrientationLockedToPortrait);

            // --- 이슈 #30: 이중 반납 시 같은 인스턴스가 유휴 스택에 두 번 들어가 서로 다른 두
            // 호출자가 동일한 활성 GameObject를 대여받던 문제 (풀 대여/유휴 불변조건) ---
            Check("ObjectPool_DoubleRelease_RejectedAndKeepsInactiveCountCorrect",
                CheckObjectPoolDoubleReleaseInvariant);
            Check("PoolManager_Release_DiagnosesNullDestroyedAndForeignInstances",
                CheckPoolManagerReleaseDiagnosticsForInvalidInputs);
            Check("PoolManager_IPoolable_CalledExactlyOncePerTransition",
                CheckPoolManagerIPoolableCalledOncePerTransition);
            Check("ObjectPool_MassRepeatedSpawnRelease_ActiveInactiveCountInvariantHolds",
                CheckObjectPoolMassRepeatedSpawnReleaseInvariant);

            // --- 이슈 #31: 음수 소모 요청이 owned.Count < amount 검사를 통과해 오히려
            // 스택을 늘리던 문제(장비 재료 무한 복제) ---
            Check("InventoryService_TryConsume_NegativeOrZeroAmount_RejectedWithNoStateChangeOrEvent",
                CheckInventoryServiceTryConsumeRejectsNonPositiveAmount);
            Check("InventoryService_TryConsume_BoundaryAmounts_IntMinMaxAndExactShortage",
                CheckInventoryServiceTryConsumeBoundaryAmounts);
            Check("InventoryService_AddEnhancementLevel_NonPositiveRejected_OverflowSaturates",
                CheckInventoryServiceAddEnhancementLevelGuardsAndSaturates);
            Check("InventoryService_RestoreSnapshot_DiscardsInvalidEntriesKeepsValidOnes",
                CheckInventoryServiceRestoreSnapshotDiscardsInvalidEntries);
            Check("InventoryService_TryConsume_RepeatedCalls_NeverGoesBelowZero",
                CheckInventoryServiceTryConsumeRepeatedCallsNeverGoesBelowZero);
            Check("EquipmentEnhancementService_TryEnhance_NegativeDuplicatesRequiredConfig_DoesNotInflateCount",
                CheckEquipmentEnhancementServiceMisconfiguredNegativeCostDoesNotInflate);

            // --- 이슈 #32: SkillLoadoutService.RestoreSnapshot이 TryEquip과 달리 레벨 1
            // 이상/중복 장착 금지를 검증하지 않아 손상된 저장 데이터가 미습득·중복 장착을
            // 그대로 런타임 상태로 만들던 문제 ---
            Check("SkillLoadoutService_RestoreSnapshot_RejectsUnlearnedSkill_NoRestoredEquip",
                CheckSkillLoadoutServiceRestoreSnapshotRejectsUnlearnedSkill);
            Check("SkillLoadoutService_RestoreSnapshot_DuplicateLearnedSkillAcrossSlots_LowestSlotWins",
                CheckSkillLoadoutServiceRestoreSnapshotDuplicateFirstSlotWins);
            Check("SkillLoadoutService_RestoreSnapshot_ClearsExistingSlotsOnRepeatedCalls",
                CheckSkillLoadoutServiceRestoreSnapshotClearsOnRepeatedCalls);
            Check("SkillLoadoutService_RestoreDisabledSlots_ResetsToAllEnabledOnRepeatedCalls",
                CheckSkillLoadoutServiceRestoreDisabledSlotsResetsOnRepeatedCalls);
            Check("SkillLoadoutService_RestoreSnapshot_MixedValidAndInvalidEntries_ValidOnesStillRestored",
                CheckSkillLoadoutServiceRestoreSnapshotMixedEntries);
            Check("SkillLoadoutService_RestoreSnapshot_RoundTrip_TryEquipAndExportRemainConsistent",
                CheckSkillLoadoutServiceRestoreSnapshotRoundTripInvariants);

            // --- 이슈 #33: OfflineStageSimulator가 StageSO.TacticEntries(전술 대형)를 통째로
            // 누락해 N-40처럼 전술 웨이브가 있는 스테이지의 실전/오프라인 총 대상 수·체력·
            // 보상이 크게 어긋나던 문제 ---
            Check("TacticSpawnEntry_PairCount_TruncatesOddTotalsConsistently",
                CheckTacticSpawnEntryPairCountTruncation);
            Check("OfflineStageSimulator_RealStage1_40_TotalMatchesStageProgressTrackerFormula",
                CheckOfflineStageSimulatorRealStage1_40TotalMatchesRuntimeFormula);
            Check("OfflineStageSimulator_TacticEntry_LeaderFollowerAlternateHealth_WeightedByChance",
                CheckOfflineStageSimulatorTacticHealthWeightedByChance);
            Check("OfflineStageSimulator_ShieldGuardLeader_EffectiveHealthIncludesShieldMultiplier",
                CheckOfflineStageSimulatorShieldGuardInflatesEffectiveHealth);
            Check("OfflineStageSimulator_AlternateFollowerPrefabNull_IgnoresChanceEntirely",
                CheckOfflineStageSimulatorNullAlternatePrefabIgnoresChance);
            Check("OfflineStageSimulator_NullLeaderPrefab_SkipsGroupWithoutThrowing",
                CheckOfflineStageSimulatorNullLeaderPrefabSkipsGroup);
            Check("OfflineStageSimulator_TacticSpawnDelay_SumsPairIntervalsPlusLastEntryImmediateDelay",
                CheckOfflineStageSimulatorTacticSpawnDelayFormula);
            Check("OfflineStageSimulator_TacticOnlyStage_SimulateSucceedsWithExactPopulationMath",
                CheckOfflineStageSimulatorTacticOnlyStagePopulation);
            Check("OfflineStageSimulator_MixedNormalAndTactic_LootYieldReflectsFullPopulation",
                CheckOfflineStageSimulatorMixedStageLootIncludesTactics);
            Check("OfflineStageSimulator_NoTacticEntries_NullVsEmptyArray_BehavesIdentically",
                CheckOfflineStageSimulatorNoTacticsNullVsEmptyBehaveIdentically);

            // --- 이슈 #34: OfflineStageSimulator.RollLoot이 엔트리별로 killsForEntry를 각자
            // 독립적으로 반올림해 총합이 rewardedKills와 어긋나던 문제 ---
            Check("AllocateByLargestRemainder_SumAlwaysEqualsTotal_Stage1_10RealEntryCounts",
                CheckAllocateByLargestRemainderSumMatchesIssueRepro);
            Check("AllocateByLargestRemainder_SingleGroup_AllocatesEntireTotalExactly",
                CheckAllocateByLargestRemainderSingleGroup);
            Check("AllocateByLargestRemainder_ExtremeRatio_SmallShareLosesToLargerRemainder",
                CheckAllocateByLargestRemainderExtremeRatio);
            Check("AllocateByLargestRemainder_ZeroCountGroup_NeverReceivesAllocation",
                CheckAllocateByLargestRemainderZeroCountGroup);
            Check("OfflineStageSimulator_RollLoot_NoLootGroupExcludedButOthersSumExactly",
                CheckOfflineStageSimulatorRollLootExcludesNoLootGroupButPreservesOthersSum);
            Check("OfflineStageSimulator_RollLoot_SplittingEntrySamePrefab_TotalDifferenceBounded",
                CheckOfflineStageSimulatorRollLootSplitEntryBoundedDifference);

            // --- 이슈 #40: 같은 병사(풀 인스턴스)가 다른 부대 슬롯으로 재등록돼도 이전 부대
            // 목록에서 제거되지 않아 한 GameObject가 두 부대에 동시에 소속되던 문제
            // (추적 딕셔너리 불변조건) ---
            Check("SquadMovementSyncService_ReRegisterToDifferentSquad_RemovesFromPreviousSquad",
                CheckSquadMovementSyncServiceReRegistrationInvariant);

            // --- 이슈 #40 나머지 구멍: 재등록(위 검사)과 달리, 이후로 다시 Register가 절대 호출
            // 되지 않는 경로(순수 배치 해제) - Unregister API 자체와 SoldierRespawner.ReleaseSlot의
            // 실제 배선을 각각 검증한다 ---
            Check("SquadMovementSyncService_Unregister_RemovesMemberAndRecomputesRemainingSquad",
                CheckSquadMovementSyncServiceUnregisterInvariant);
            Check("SoldierRespawner_ReleaseSlot_UnregistersFromSquadMovementSync",
                CheckSoldierRespawnerReleaseSlotUnregistersFromSquadSync);

            // --- 이슈 #41: 습격 전술(SquadRaidCoordinator) 대기 타이머가 병사 동행 금지 던전
            // 안에서도 계속 흘러 병사가 되살아나거나, 던전 퇴장이 아직 대기 중인 습격 부대까지
            // 강제로 드러내던 문제 ---
            Check("SquadRaidCoordinator_DungeonHidden_PausesCountdownAndDefersReveal",
                CheckSquadRaidCoordinatorDungeonHiddenPausesCountdown);
            Check("SoldierRespawner_SetActiveAll_SkipsPendingRaidSquadMembers",
                CheckSoldierRespawnerSetActiveAllSkipsPendingRaidMembers);

            // --- 이슈 #30/#40과 별개로: 모달 전환(던전 팝업 열기/닫기, StageProgressTracker.
            // SetActiveAll)이 밀집 전투 도중 끼어들어도 풀 대여/유휴 불변조건과 추적 딕셔너리가
            // 함께 유지되는지 확인 (section FF에서 SetActiveAll 자체가 실제로 버그였던 지점) ---
            Check("DenseCombatWithModalTransition_PoolAndTrackerInvariantsHold",
                CheckDenseCombatWithModalTransitionInvariants);

            // --- 이슈 #25: Android 시스템 뒤로가기가 팝업 스택을 닫거나 이전 화면으로 이동하지
            // 않던 문제 - IDismissible 스택(LIFO)/오버레이 이탈 정책/실제 팝업 배선 스위프 ---
            Check("BackNavigationService_TryDismissTop_DismissesMostRecentlyRegisteredFirst",
                CheckBackNavigationServiceLifoOrder);
            Check("BackNavigationService_Register_DuplicateRegistrationIsIdempotent",
                CheckBackNavigationServiceDuplicateRegistration);
            Check("BackNavigationService_TryDismissTop_PrunesDestroyedUnityObjectEntry",
                CheckBackNavigationServicePrunesDestroyedEntry);
            Check("BackInputRouter_TryExitWaitingDungeon_ThreeBranches",
                CheckBackInputRouterTryExitWaitingDungeon);
            Check("PopupClasses_AllImplementIDismissible_StructuralSweep",
                CheckPopupClassesImplementIDismissible);


            // --- 이슈 #26: 병사 로스터/배치/부대 전술 복원이 ID·슬롯 불변조건을 검증하지 않아
            // 덮어쓰기(nextInstanceId 충돌)·유령 슬롯·중복 배치·범위 밖 전술 인덱스가 발생하던 문제 ---
            Check("SoldierRosterService_RestoreSnapshot_NormalizesNextInstanceIdAboveCollisions",
                CheckSoldierRosterRestoreNormalizesNextInstanceId);
            Check("SoldierRosterService_RestoreSnapshot_RejectsNegativeAndDuplicateInstanceIds",
                CheckSoldierRosterRestoreRejectsNegativeAndDuplicateIds);
            Check("SoldierRosterService_RestoreSnapshot_ClearsPreviousEntriesOnReRestore",
                CheckSoldierRosterRestoreClearsOnReRestore);
            Check("SoldierRosterService_RestoreSnapshot_NextInstanceIdSaturatesOnOverflow",
                CheckSoldierRosterRestoreSaturatesOnOverflow);
            Check("SoldierDeploymentService_RestoreSnapshot_DiscardsGhostAndOutOfRangeSlots",
                CheckSoldierDeploymentRestoreDiscardsGhostAndOutOfRangeSlots);
            Check("SoldierDeploymentService_RestoreSnapshot_KeepsOnlyLowestSlotForDuplicateInstanceId",
                CheckSoldierDeploymentRestoreDedupesInstanceId);
            Check("SoldierDeploymentService_RestoreSnapshot_ClearsPreviousEntriesOnReRestore",
                CheckSoldierDeploymentRestoreClearsOnReRestore);
            Check("SquadTacticService_SetTactic_RejectsOutOfRangeIndexAndUndefinedEnum",
                CheckSquadTacticServiceSetTacticRejectsInvalid);
            Check("SquadTacticService_RestoreSnapshot_SkipsInvalidEntryWithoutAbortingRest",
                CheckSquadTacticServiceRestoreSkipsInvalidEntry);
            Check("SquadRaidCoordinator_OnTacticChanged_OutOfRangeIndex_DoesNotThrow",
                CheckSquadRaidCoordinatorOnTacticChangedOutOfRangeIndex);


            // --- 이슈 #27: 일반 웨이브 SpawnInterval을 실전은 무시하고(즉시 스폰) 오프라인
            // 시뮬레이터는 병목으로 계산하던 시간 모델 불일치 ---
            Check("MonsterSpawner_TickEntries_IgnoresSpawnIntervalAndSpawnsAllInOneTick",
                CheckMonsterSpawnerIgnoresSpawnIntervalRealCombat);
            Check("OfflineStageSimulator_Simulate_ResultIndependentOfSpawnInterval",
                CheckOfflineStageSimulatorResultIndependentOfSpawnInterval);
            Check("OfflineStageSimulator_Simulate_AllZeroSpawnInterval_DoesNotFail",
                CheckOfflineStageSimulatorAllZeroSpawnIntervalSucceeds);
            Check("OfflineStageSimulator_Simulate_MixedSpawnWithTacticsAndVaryingIntervals_Consistent",
                CheckOfflineStageSimulatorMixedSpawnWithTacticsConsistent);


            // --- 이슈 #28: 컬렉션 스냅샷이 더티인 상태에서 앱 pause/quit이 오면 Save()가 최신
            // 캐시를 재구축하지 않고 낡은 캐시를 영구 저장하던 문제 ---
            Check("SaveService_FlushPendingChanges_RebuildsAllFourDirtySnapshotsRegardlessOfIsDirty",
                CheckSaveServiceFlushRebuildsAllDirtySnapshots);
            Check("SaveService_FlushPendingChanges_ActuallyPersistsAndClearsIsDirty_SafeRoundTrip",
                CheckSaveServiceFlushActuallyPersistsSafely);
            Check("GameBootstrapper_OnApplicationPauseAndQuit_RouteThroughFlushPendingChanges",
                CheckGameBootstrapperLifecycleUsesFlushPendingChanges);


            // --- 이슈 #29: 던전(강화석/스킬/보스/병사 구출) 오버레이 몬스터가 던전 진입 전
            // 일반 스테이지의 골드·장비 드롭까지 중복 지급하던 문제 ---
            Check("LootDropper_OnCharacterDied_SkipsNormalDropsWhileOverlayActive",
                CheckLootDropperSkipsNormalDropsDuringOverlay);
            Check("LootDropper_OnCharacterDied_ResumesNormalDropsAfterOverlayEnds",
                CheckLootDropperResumesNormalDropsAfterOverlay);

            total = localTotal;
            failures = localFailures;
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
        /// BigNumber.TryParse의 catch(FormatException)가 지수/가수 파트의 OverflowException까지
        /// 잡는지 확인한다 - 이슈 #7의 재현 절차(SaveService.LoadGold를 통해 Load() 전체가
        /// 중단되는 것)와 같은 클래스의 손상값이 Gold 필드에서도 크래시 없이 Zero로 폴백해야 한다.
        /// </summary>
        private static void CheckBigNumberTryParseOverflow()
        {
            bool overflowOk = BigNumber.TryParse("1E99999999999999999999", out BigNumber overflowResult);

            if (overflowOk || overflowResult != BigNumber.Zero)
            {
                throw new Exception($"지수 오버플로 입력이 예외 없이 처리됐지만 결과가 기대와 다름: ok={overflowOk}, result={overflowResult}");
            }

            bool mantissaOverflowOk = BigNumber.TryParse(
                "99999999999999999999999999999999999999999999999999999999999999999999999999999999999999E5",
                out BigNumber mantissaOverflowResult);

            // double.Parse는 오버플로 시 예외 대신 PositiveInfinity를 반환하므로(Parse 자체는 성공),
            // 여기서는 "예외 없이 반환됐는지"만 확인한다 - 무한대/비정상 값이 BigNumber 연산에서
            // 어떻게 다뤄지는지는 이 검사의 범위 밖이다.
            _ = mantissaOverflowOk;
            _ = mantissaOverflowResult;

            bool normalOk = BigNumber.TryParse("1.5E10", out BigNumber normalResult);

            if (!normalOk || !AreApproximatelyEqual(normalResult.ToDouble(), 1.5e10))
            {
                throw new Exception($"정상 입력까지 회귀됨: ok={normalOk}, result={normalResult}");
            }
        }

        private static bool AreApproximatelyEqual(double a, double b) => Math.Abs(a - b) < Math.Max(1.0, Math.Abs(b)) * 1e-6;

        /// <summary>
        /// SaveService.ClampNonNegative/ClampAtLeastOne이 음수·경계값을 안전한 기본값으로
        /// 되돌리는지 확인한다 - 이슈 #7 완료 조건("음수·오버플로·누락 값에 안전한 기본값과
        /// 범위 검사")이 LastActiveUnixTime 외 나머지 정수 저장 필드(강화 레벨/티켓·토큰 카운트/
        /// Chapter·StageNumber 등)에도 적용됐는지 검증한다.
        /// </summary>
        private static void CheckSaveServiceClampHelpers()
        {
            MethodInfo clampNonNegative = typeof(SaveService).GetMethod(
                "ClampNonNegative", BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo clampAtLeastOne = typeof(SaveService).GetMethod(
                "ClampAtLeastOne", BindingFlags.NonPublic | BindingFlags.Static);

            if (clampNonNegative == null || clampAtLeastOne == null)
            {
                throw new Exception("ClampNonNegative/ClampAtLeastOne 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            AssertClamp(clampNonNegative, -5, 0, "ClampNonNegative(-5)");
            AssertClamp(clampNonNegative, 0, 0, "ClampNonNegative(0)");
            AssertClamp(clampNonNegative, 42, 42, "ClampNonNegative(42)");
            AssertClamp(clampNonNegative, int.MinValue, 0, "ClampNonNegative(int.MinValue)");

            AssertClamp(clampAtLeastOne, -5, 1, "ClampAtLeastOne(-5)");
            AssertClamp(clampAtLeastOne, 0, 1, "ClampAtLeastOne(0)");
            AssertClamp(clampAtLeastOne, 1, 1, "ClampAtLeastOne(1)");
            AssertClamp(clampAtLeastOne, 7, 7, "ClampAtLeastOne(7)");
        }

        private static void AssertClamp(MethodInfo method, int input, int expected, string label)
        {
            int actual = (int)method.Invoke(null, new object[] { input });

            if (actual != expected)
            {
                throw new Exception($"{label}: 기대={expected} 실제={actual}");
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
        /// GitHub 이슈 #20의 2026-08-26 추가 코멘트 - PracticeStageController(section GK에서 이슈
        /// #20 수정 이후 새로 추가된 기능)가 다른 6개 오버레이 컨트롤러와 같은 "스폰 성공 확인 전에
        /// 상태부터 커밋" 안티패턴을 재발시켰던 것을 확인한다. TrySpawnDummy()가 PoolManager 없이
        /// 실패하면 TryEnter() 전체가 false를 반환하고 IsActive/_dummyInstance 둘 다 그대로여야
        /// 한다. stageController.IsOverlayActive는 AddComponent 직후(Awake 미실행) C# bool 기본값인
        /// false라 별도 초기화 없이도 TryEnter()의 앞쪽 가드를 통과해 TrySpawnDummy()까지 도달한다.
        /// </summary>
        private static void CheckPracticeStageControllerTryEnterNoPoolManagerReturnsFalse()
        {
            var go = new GameObject("RegressionCheck_PracticeStageController");
            go.SetActive(false);

            var stageControllerGo = new GameObject("RegressionCheck_PracticeStageController_StageController");
            stageControllerGo.SetActive(false);

            var dummyPrefab = new GameObject("RegressionCheck_PracticeStageController_DummyPrefab");
            dummyPrefab.SetActive(false);

            try
            {
                var stageController = stageControllerGo.AddComponent<Stage.StageController>();
                var controller = go.AddComponent<Stage.PracticeStageController>();
                SetPrivateField(controller, "stageController", stageController);
                SetPrivateField(controller, "dummyPrefab", dummyPrefab);

                WithNullServices(() =>
                {
                    bool success = controller.TryEnter();

                    if (success)
                    {
                        throw new Exception("PoolManager 없이도 TryEnter()가 true를 반환함");
                    }
                });

                if (controller.IsActive)
                {
                    throw new Exception("TryEnter() 실패 후에도 IsActive가 true로 남음");
                }

                FieldInfo dummyInstanceField = typeof(Stage.PracticeStageController).GetField(
                    "_dummyInstance", BindingFlags.NonPublic | BindingFlags.Instance);
                object dummyInstance = dummyInstanceField?.GetValue(controller);

                if (dummyInstance != null)
                {
                    throw new Exception("TryEnter() 실패 후에도 _dummyInstance가 채워져 있음");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(stageControllerGo);
                UnityEngine.Object.DestroyImmediate(dummyPrefab);
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
        /// GitHub 이슈 #45의 재현 그대로: int.MaxValue 근처에서 AddStones를 호출해도 순수 int
        /// 덧셈처럼 음수로 반전되지 않고 int.MaxValue에서 saturate하는지 확인한다. 경계-1(정확히
        /// int.MaxValue가 되는 지점)과 경계(이미 int.MaxValue인 상태에서 대량 추가)를 모두
        /// 검증하고, saturate된 이후에도 TrySpendStones가 정상적으로 계속 동작함(=진짜 유효한
        /// 양수 잔액이지 손상된 상태가 아님)까지 확인한다.
        /// </summary>
        private static void CheckEnhancementStoneServiceSaturatesAtIntMaxValue()
        {
            var events = new Core.EventBus();

            var atBoundaryMinusOne = new EnhancementStoneService(events, int.MaxValue - 1);
            atBoundaryMinusOne.AddStones(1);

            if (atBoundaryMinusOne.CurrentStones != int.MaxValue)
            {
                throw new Exception($"int.MaxValue-1에서 1 추가 후 {atBoundaryMinusOne.CurrentStones}(기대={int.MaxValue}) - 경계 직전에서 조기 saturate됐거나 계산이 틀림");
            }

            var atBoundary = new EnhancementStoneService(events, int.MaxValue);
            atBoundary.AddStones(1);

            if (atBoundary.CurrentStones != int.MaxValue)
            {
                throw new Exception($"int.MaxValue에서 1 추가 후 {atBoundary.CurrentStones}(기대={int.MaxValue}, 음수 반전 없이 saturate돼야 함) - GitHub 이슈 #45 재현");
            }

            atBoundary.AddStones(int.MaxValue); // 대량 추가도 saturate 유지돼야 함(long 중간 계산 확인).

            if (atBoundary.CurrentStones != int.MaxValue)
            {
                throw new Exception($"대량 추가 후 {atBoundary.CurrentStones}(기대={int.MaxValue})");
            }

            if (!atBoundary.TrySpendStones(5) || atBoundary.CurrentStones != int.MaxValue - 5)
            {
                throw new Exception($"saturate된 잔액에서 정상 소비가 실패함(잔액={atBoundary.CurrentStones}) - 유효한 잔액이 아닌 것으로 보임");
            }
        }

        private static void CheckSoldierTicketServiceSaturatesAtIntMaxValue()
        {
            var events = new Core.EventBus();

            var atBoundaryMinusOne = new SoldierTicketService(events, int.MaxValue - 1);
            atBoundaryMinusOne.AddTickets(1);

            if (atBoundaryMinusOne.CurrentTickets != int.MaxValue)
            {
                throw new Exception($"int.MaxValue-1에서 1 추가 후 {atBoundaryMinusOne.CurrentTickets}(기대={int.MaxValue})");
            }

            var atBoundary = new SoldierTicketService(events, int.MaxValue);
            atBoundary.AddTickets(1);

            if (atBoundary.CurrentTickets != int.MaxValue)
            {
                throw new Exception($"int.MaxValue에서 1 추가 후 {atBoundary.CurrentTickets}(기대={int.MaxValue}, 음수 반전 없이 saturate돼야 함) - GitHub 이슈 #45 재현");
            }

            atBoundary.AddTickets(int.MaxValue);

            if (atBoundary.CurrentTickets != int.MaxValue)
            {
                throw new Exception($"대량 추가 후 {atBoundary.CurrentTickets}(기대={int.MaxValue})");
            }

            if (!atBoundary.TrySpendTickets(5) || atBoundary.CurrentTickets != int.MaxValue - 5)
            {
                throw new Exception($"saturate된 잔액에서 정상 소비가 실패함(잔액={atBoundary.CurrentTickets})");
            }
        }

        private static void CheckSkillScrollServiceSaturatesAtIntMaxValue()
        {
            var events = new Core.EventBus();

            var atBoundaryMinusOne = new SkillScrollService(events, int.MaxValue - 1);
            atBoundaryMinusOne.AddScrolls(1);

            if (atBoundaryMinusOne.CurrentScrolls != int.MaxValue)
            {
                throw new Exception($"int.MaxValue-1에서 1 추가 후 {atBoundaryMinusOne.CurrentScrolls}(기대={int.MaxValue})");
            }

            var atBoundary = new SkillScrollService(events, int.MaxValue);
            atBoundary.AddScrolls(1);

            if (atBoundary.CurrentScrolls != int.MaxValue)
            {
                throw new Exception($"int.MaxValue에서 1 추가 후 {atBoundary.CurrentScrolls}(기대={int.MaxValue}, 음수 반전 없이 saturate돼야 함) - GitHub 이슈 #45 재현");
            }

            atBoundary.AddScrolls(int.MaxValue);

            if (atBoundary.CurrentScrolls != int.MaxValue)
            {
                throw new Exception($"대량 추가 후 {atBoundary.CurrentScrolls}(기대={int.MaxValue})");
            }

            if (!atBoundary.TrySpendScrolls(5) || atBoundary.CurrentScrolls != int.MaxValue - 5)
            {
                throw new Exception($"saturate된 잔액에서 정상 소비가 실패함(잔액={atBoundary.CurrentScrolls})");
            }
        }

        private static void CheckEquipmentGachaTicketServiceSaturatesAtIntMaxValue()
        {
            var events = new Core.EventBus();

            var atBoundaryMinusOne = new EquipmentGachaTicketService(events, int.MaxValue - 1);
            atBoundaryMinusOne.AddTickets(1);

            if (atBoundaryMinusOne.CurrentTickets != int.MaxValue)
            {
                throw new Exception($"int.MaxValue-1에서 1 추가 후 {atBoundaryMinusOne.CurrentTickets}(기대={int.MaxValue})");
            }

            var atBoundary = new EquipmentGachaTicketService(events, int.MaxValue);
            atBoundary.AddTickets(1);

            if (atBoundary.CurrentTickets != int.MaxValue)
            {
                throw new Exception($"int.MaxValue에서 1 추가 후 {atBoundary.CurrentTickets}(기대={int.MaxValue}, 음수 반전 없이 saturate돼야 함) - GitHub 이슈 #45 재현");
            }

            atBoundary.AddTickets(int.MaxValue);

            if (atBoundary.CurrentTickets != int.MaxValue)
            {
                throw new Exception($"대량 추가 후 {atBoundary.CurrentTickets}(기대={int.MaxValue})");
            }

            if (!atBoundary.TrySpendTickets(5) || atBoundary.CurrentTickets != int.MaxValue - 5)
            {
                throw new Exception($"saturate된 잔액에서 정상 소비가 실패함(잔액={atBoundary.CurrentTickets})");
            }
        }

        private static void CheckBossTokenServiceSaturatesAtIntMaxValue()
        {
            var events = new Core.EventBus();

            var atBoundaryMinusOne = new BossTokenService(events, int.MaxValue - 1);
            atBoundaryMinusOne.AddTokens(1);

            if (atBoundaryMinusOne.CurrentTokens != int.MaxValue)
            {
                throw new Exception($"int.MaxValue-1에서 1 추가 후 {atBoundaryMinusOne.CurrentTokens}(기대={int.MaxValue})");
            }

            var atBoundary = new BossTokenService(events, int.MaxValue);
            atBoundary.AddTokens(1);

            if (atBoundary.CurrentTokens != int.MaxValue)
            {
                throw new Exception($"int.MaxValue에서 1 추가 후 {atBoundary.CurrentTokens}(기대={int.MaxValue}, 음수 반전 없이 saturate돼야 함) - GitHub 이슈 #45 재현");
            }

            atBoundary.AddTokens(int.MaxValue);

            if (atBoundary.CurrentTokens != int.MaxValue)
            {
                throw new Exception($"대량 추가 후 {atBoundary.CurrentTokens}(기대={int.MaxValue})");
            }

            if (!atBoundary.TrySpendTokens(5) || atBoundary.CurrentTokens != int.MaxValue - 5)
            {
                throw new Exception($"saturate된 잔액에서 정상 소비가 실패함(잔액={atBoundary.CurrentTokens})");
            }
        }

        /// <summary>
        /// 이슈 #8 완료 조건 "잘못된 SO 비용이 발견되면 콘텐츠 검증 단계에서 구체적인 오류를
        /// 제공함"을 실제 프로젝트 자산 전체를 대상으로 검증한다. Editor.ContentCostValidation과
        /// 같은 Assembly-CSharp-Editor 어셈블리 안이라 리플렉션 없이 직접 호출한다. 여기서는
        /// "현재 콘텐츠에 음수 비용이 없다"만 확인 - "실제로 음수를 넣으면 구체적인 오류가
        /// 나온다"는 동작 자체는 이 도구를 만들 때 Unity Editor에서 직접 자산을 임시로 손상시켜
        /// 검증했다(디스크에는 저장하지 않음).
        /// </summary>
        private static void CheckContentCostValidationProjectAssets()
        {
            List<string> errors = Editor.ContentCostValidation.ValidateAll(out int assetsChecked);

            if (assetsChecked == 0)
            {
                throw new Exception("비용 SO 자산을 하나도 못 찾음 - 프로젝트 경로/타입 이름이 바뀌었는지 확인");
            }

            if (errors.Count > 0)
            {
                throw new Exception($"{assetsChecked}개 자산 중 {errors.Count}건의 비용 오류: {string.Join(" | ", errors)}");
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

        /// <summary>
        /// 리빌 도중(또는 완전히 끝난 뒤) 탭을 벗어났다 돌아오면, OnDisable이 반납했던 슬롯 중
        /// 이미 결정된(_nextIndex 미만) 것들이 OnEnable에서 즉시 다시 스폰되는지 확인한다
        /// (GitHub 이슈 #10 완료 조건 3번 - Unity Editor에서 실제 재현해 찾은 버그: 이 복원
        /// 로직이 없으면 리빌 도중 이탈 후 복귀 시 이미 보여준 앞쪽 슬롯이 다시는 안 나타나고,
        /// 완전히 끝난 뒤 이탈 후 복귀하면 화면이 통째로 비게 됐다). 5개 중 2개만 리빌된 "도중"
        /// 상태로 이탈->복귀 후 나머지를 마저 리빌하고, 그 완료 상태로 다시 이탈->복귀하는 것까지
        /// 전부 한 흐름으로 검증한다.
        /// </summary>
        private static void CheckGachaResultRevealControllerRestoresOnReenable()
        {
            const int slotCount = 5;
            const int revealedBeforeLeaving = 2;

            string prefabPath = "Assets/04. Prefab/UI/GachaResultSlot.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null || !prefab.TryGetComponent(out UI.GachaResultSlotUI slotPrefab))
            {
                throw new Exception($"{prefabPath}에서 UI.GachaResultSlotUI 프리팹을 찾지 못함 - 경로/컴포넌트가 바뀌었는지 확인");
            }

            var pool = new Managers.PoolManager();
            pool.Initialize();
            pool.EnsurePool(prefab, slotCount, slotCount);

            GameObject controllerGo = null;

            try
            {
                var visuals = new UI.GachaResultVisual[slotCount];
                for (int i = 0; i < slotCount; i++)
                {
                    visuals[i] = new UI.GachaResultVisual(null, Color.white);
                }

                controllerGo = CreateRevealController(slotPrefab, out UI.GachaResultRevealController controller);
                SetPrivateField(controller, "_pool", pool);
                SetPrivateField(controller, "_pending", visuals);
                SetPrivateField(controller, "_nextIndex", 0);

                MethodInfo spawnNext = typeof(UI.GachaResultRevealController).GetMethod("SpawnNext", BindingFlags.NonPublic | BindingFlags.Instance);

                // 실제 OnEnable()이 아니라 RestoreAlreadyRevealedSlots()를 직접 호출한다 - OnEnable은
                // GameBootstrapper.Services를 통한 GameTicker 등록도 함께 하는데, 이 검사가 순수
                // Edit Mode 환경(GameBootstrapper가 존재하지 않음)에서 검증하려는 것은 오직 "복원"
                // 로직 하나뿐이라 그 부분만 직접 호출해 무관한 전역 상태 결합을 피한다.
                MethodInfo restore = typeof(UI.GachaResultRevealController).GetMethod("RestoreAlreadyRevealedSlots", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo contentField = typeof(UI.GachaResultRevealController).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo nextIndexField = typeof(UI.GachaResultRevealController).GetField("_nextIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                var content = (Transform)contentField.GetValue(controller);

                for (int i = 0; i < revealedBeforeLeaving; i++)
                {
                    spawnNext.Invoke(controller, null);
                }

                if (content.childCount != revealedBeforeLeaving)
                {
                    throw new Exception($"이탈 전 스폰된 슬롯 수가 {content.childCount}(기대={revealedBeforeLeaving})");
                }

                InvokeOnDisable(controller);

                if (content.childCount != 0)
                {
                    throw new Exception($"OnDisable 이후 슬롯이 반납되지 않음: {content.childCount}");
                }

                restore.Invoke(controller, null);

                if (content.childCount != revealedBeforeLeaving)
                {
                    throw new Exception($"재활성화 후 즉시 복원된 슬롯 수가 {content.childCount}(기대={revealedBeforeLeaving}) - 이미 보여준 슬롯이 재열기 시 사라짐");
                }

                int nextIndexAfterRestore = (int)nextIndexField.GetValue(controller);

                if (nextIndexAfterRestore != revealedBeforeLeaving)
                {
                    throw new Exception($"복원 과정에서 진행 인덱스가 바뀜: {nextIndexAfterRestore}(기대={revealedBeforeLeaving})");
                }

                for (int i = revealedBeforeLeaving; i < slotCount; i++)
                {
                    spawnNext.Invoke(controller, null);
                }

                if (content.childCount != slotCount)
                {
                    throw new Exception($"나머지 리빌 완료 후 슬롯 수가 {content.childCount}(기대={slotCount})");
                }

                InvokeOnDisable(controller);
                restore.Invoke(controller, null);

                if (content.childCount != slotCount)
                {
                    throw new Exception($"완전히 끝난 리빌을 재열기했는데 슬롯 수가 {content.childCount}(기대={slotCount}) - 화면이 비는 회귀");
                }

                InvokeOnDisable(controller);
            }
            finally
            {
                if (controllerGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(controllerGo);
                }

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
            var scrollRect = go.AddComponent<UnityEngine.UI.ScrollRect>();
            controller = go.AddComponent<UI.GachaResultRevealController>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(go.transform);

            // ScrollRect.SetNormalizedPosition(내부적으로 ScrollToBottom이 호출)이 자기 자신의
            // content(스크롤 대상 RectTransform, 우리 컨트롤러의 private "content" 필드와는 별개)가
            // null이면 널 체크 없이 그대로 NRE를 던진다 - GitHub 이슈 #10 회귀 검사(재활성화 시
            // 즉시 복원, ScrollToBottom 경로를 처음으로 실제 호출) 작성 중 실제로 겪은 함정.
            scrollRect.content = contentGo.GetComponent<RectTransform>();

            SetPrivateField(controller, "content", contentGo.transform);
            SetPrivateField(controller, "slotPrefab", slotPrefab);

            // GameObject를 비활성 상태로 만든 채 컴포넌트를 추가하므로(OnEnable이 곧장 실행되지
            // 않도록) Unity가 Awake()를 아직 호출하지 않았다 - 실제 씬이라면 Awake()가 이미 끝난
            // 뒤 비활성화됐을 시점이라 _scrollRect가 채워져 있지만, 이 합성 오브젝트는 처음부터
            // 비활성이라 _scrollRect가 계속 null로 남는다. RestoreAlreadyRevealedSlots 등이 이
            // 필드를 쓰는 경로(ScrollToBottom)를 검증하려면 실제 라이프사이클처럼 Awake()를 한 번
            // 대신 실행해줘야 한다(GitHub 이슈 #10 회귀 검사 작성 중 실제로 겪은 함정).
            MethodInfo awake = typeof(UI.GachaResultRevealController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            awake.Invoke(controller, null);

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

        /// <summary>
        /// GitHub 이슈 #21 - SoldierRosterService.AddSoldiersBatch(N개)가 SoldierRosterChangedEvent를
        /// 딱 1번만 발행하고, 반환된 N개 유닛이 서로 다른 InstanceId를 갖는지 확인한다.
        /// </summary>
        private static void CheckSoldierRosterServiceAddSoldiersBatchPublishesEventOnce()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            SoldierSO definition = null;

            try
            {
                definition = ScriptableObject.CreateInstance<SoldierSO>();

                int publishCount = 0;
                events.Subscribe<SoldierRosterChangedEvent>(_ => publishCount++);

                var definitions = new List<SoldierSO> { definition, definition, definition, definition, definition };
                IReadOnlyList<OwnedSoldier> added = roster.AddSoldiersBatch(definitions);

                if (publishCount != 1)
                {
                    throw new Exception($"AddSoldiersBatch(5개)가 SoldierRosterChangedEvent를 {publishCount}번 발행함(기대=1)");
                }

                if (added.Count != 5)
                {
                    throw new Exception($"반환된 유닛 수가 {added.Count}(기대=5)");
                }

                var distinctIds = new HashSet<int>();

                foreach (OwnedSoldier owned in added)
                {
                    distinctIds.Add(owned.InstanceId);
                }

                if (distinctIds.Count != 5)
                {
                    throw new Exception("배치로 추가된 유닛들의 InstanceId가 중복됨");
                }
            }
            finally
            {
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #21 - GachaService.Pull(tier, N)이 내부적으로 N번 굴려도, 로스터 반영/
        /// SoldierRosterChangedEvent 발행은 배치 전체에 딱 1번만 일어나는지 확인한다(section GY
        /// 이후 이 파일이 확립한 "합성 GachaTableSO/GachaPoolEntry를 SetPrivateField로 조립"
        /// 패턴 재사용).
        /// </summary>
        private static void CheckGachaServicePullAddsSoldiersAsSingleBatch()
        {
            var events = new EventBus();
            var tickets = new SoldierTicketService(events, 1000);
            var currency = new CurrencyService(events);
            var roster = new SoldierRosterService(events);
            SoldierSO definition = null;
            GachaTableSO table = null;

            try
            {
                definition = ScriptableObject.CreateInstance<SoldierSO>();

                var entry = new GachaPoolEntry();
                SetPrivateFieldOnPlainObject(entry, "soldier", definition);
                SetPrivateFieldOnPlainObject(entry, "weight", 1);

                table = ScriptableObject.CreateInstance<GachaTableSO>();
                SetPrivateField(table, "entries", new[] { entry });
                SetPrivateField(table, "ticketCostPerPull", 1);

                var service = new GachaService(events, tickets, currency, roster, new[] { table });

                int publishCount = 0;
                events.Subscribe<SoldierRosterChangedEvent>(_ => publishCount++);

                IReadOnlyList<OwnedSoldier> results = service.Pull(0, 50);

                if (results.Count != 50)
                {
                    throw new Exception($"Pull(0,50) 결과가 {results.Count}개(기대=50)");
                }

                if (publishCount != 1)
                {
                    throw new Exception($"50연 뽑기가 SoldierRosterChangedEvent를 {publishCount}번 발행함(기대=1)");
                }
            }
            finally
            {
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                if (table != null)
                {
                    UnityEngine.Object.DestroyImmediate(table);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #21 완료 조건("저사양 모바일 기준 CPU·GC 예산이 정의되고 측정됨") - 절대
        /// ms/바이트 임계값 대신 아이템당 GC 할당량의 "비율"로 판정한다. 하드웨어/CI 러너 성능에
        /// 좌우되는 벽시계 시간과 달리 GC.GetAllocatedBytesForCurrentThread()는 같은 코드 경로에서
        /// 항상 같은 바이트 수를 반환하므로(타이머 노이즈 없음), 절대 임계값을 잡을 때 겪는 flaky
        /// 위험(이슈 #11 조사 때 자동 UI 레이캐스트 검사를 포기한 것과 같은 이유) 없이도 재발을
        /// 감지할 수 있다. 30개짜리 배치를 먼저 돌려 로스터를 채운 뒤, 그 상태에서 300개짜리
        /// 배치를 이어서 돌린다 - 이슈가 실제로 재현한 버그(기존 로스터가 클수록 다음 배치의
        /// "아이템당" 비용이 커지는 패턴, 100/300/600개 순차 추가 시 4ms/20ms/82ms로 초선형
        /// 증가)를 그대로 재현하는 순서다. 아이템당 할당량이 3배 이상 벌어지면 초선형 회귀로
        /// 판정한다. 절대 시간은 하드웨어 차이를 감안한 관대한 안전망(3초)으로만 별도 확인한다.
        /// </summary>
        private static void CheckGachaServicePull300DoesNotScaleSuperlinearly()
        {
            var events = new EventBus();
            var tickets = new SoldierTicketService(events, 10000);
            var currency = new CurrencyService(events);
            var roster = new SoldierRosterService(events);
            SoldierSO definition = null;
            GachaTableSO table = null;

            try
            {
                definition = ScriptableObject.CreateInstance<SoldierSO>();

                var entry = new GachaPoolEntry();
                SetPrivateFieldOnPlainObject(entry, "soldier", definition);
                SetPrivateFieldOnPlainObject(entry, "weight", 1);

                table = ScriptableObject.CreateInstance<GachaTableSO>();
                SetPrivateField(table, "entries", new[] { entry });
                SetPrivateField(table, "ticketCostPerPull", 1);

                var service = new GachaService(events, tickets, currency, roster, new[] { table });

                const int smallCount = 30;
                const int largeCount = 300;
                const double safetyFactor = 3.0;
                const long generousTimeBudgetMs = 3000;

                long allocBeforeSmall = GC.GetAllocatedBytesForCurrentThread();
                service.Pull(0, smallCount);
                long allocSmall = GC.GetAllocatedBytesForCurrentThread() - allocBeforeSmall;

                long allocBeforeLarge = GC.GetAllocatedBytesForCurrentThread();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                service.Pull(0, largeCount);
                sw.Stop();
                long allocLarge = GC.GetAllocatedBytesForCurrentThread() - allocBeforeLarge;

                double bytesPerItemSmall = (double)allocSmall / smallCount;
                double bytesPerItemLarge = (double)allocLarge / largeCount;

                if (allocSmall > 0 && bytesPerItemLarge > bytesPerItemSmall * safetyFactor)
                {
                    throw new Exception(
                        $"아이템당 GC 할당량이 초선형으로 증가함 - {smallCount}개: {bytesPerItemSmall:F1}B/개, " +
                        $"{largeCount}개: {bytesPerItemLarge:F1}B/개(허용 배율 {safetyFactor}배 초과)");
                }

                if (sw.ElapsedMilliseconds > generousTimeBudgetMs)
                {
                    throw new Exception($"{largeCount}연 뽑기가 {sw.ElapsedMilliseconds}ms 소요됨(관대한 상한 {generousTimeBudgetMs}ms 초과)");
                }
            }
            finally
            {
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                if (table != null)
                {
                    UnityEngine.Object.DestroyImmediate(table);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #21 - SkillGachaService.Pull(tier, N)이 같은 스킬을 여러 번 뽑아도
        /// SkillService.AddCopy 호출(및 SkillCountChangedEvent 발행)이 서로 다른 스킬 종류
        /// 수만큼만 일어나는지 확인한다. 카탈로그에 스킬을 하나만 넣어 50연 전부가 그 한 스킬로만
        /// 귀결되도록 강제한다 - 이벤트가 1번만 떠야 정상이고, 50번 뜨면 회귀다.
        /// </summary>
        private static void CheckSkillGachaServicePullAggregatesAddCopyByDefinition()
        {
            var events = new EventBus();
            var scrolls = new SkillScrollService(events, 1000);
            var currency = new CurrencyService(events);
            var skillService = new SkillService(events);
            SkillSO definition = null;
            SkillCatalogSO catalog = null;
            SkillGachaTableSO table = null;

            try
            {
                definition = ScriptableObject.CreateInstance<SkillSO>();

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", new[] { definition });

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);
                SetPrivateField(table, "ticketCostPerPull", 1);

                var service = new SkillGachaService(events, scrolls, currency, skillService, new[] { table });

                int publishCount = 0;
                events.Subscribe<SkillCountChangedEvent>(_ => publishCount++);

                IReadOnlyList<SkillSO> results = service.Pull(0, 50);

                if (results.Count != 50)
                {
                    throw new Exception($"Pull(0,50) 결과가 {results.Count}개(기대=50)");
                }

                if (publishCount != 1)
                {
                    throw new Exception($"단일 스킬 카탈로그로 50연 뽑기해도 SkillCountChangedEvent가 {publishCount}번 발행됨(기대=1)");
                }

                if (skillService.GetCount(definition) != 50)
                {
                    throw new Exception($"최종 보유 개수가 {skillService.GetCount(definition)}(기대=50)");
                }
            }
            finally
            {
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }

                if (table != null)
                {
                    UnityEngine.Object.DestroyImmediate(table);
                }
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
        /// GachaPullToast 단위로 재현한다 - NoCandidates(데이터 오류)는 noCandidatesMessage를
        /// 그대로 쓴다(allMaxedMessage는 AllCandidatesMaxed 전용이라 여기서는 안 쓰임).
        /// </summary>
        private static void CheckGachaPullToastNoCandidatesZeroSuccess()
        {
            var events = new EventBus();
            var toasts = new List<string>();
            events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

            GachaPullToast.PublishIfIncomplete(
                events, 0, 300, GachaPullStopReason.NoCandidates,
                noCandidatesMessage: "뽑기 콘텐츠를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.",
                allMaxedMessage: "모든 스킬이 최대 레벨입니다.");

            if (toasts.Count != 1 || toasts[0] != "뽑기 콘텐츠를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.")
            {
                throw new Exception($"후보 없음(데이터 오류, 0/300) 토스트가 기대와 다름: [{string.Join(", ", toasts)}]");
            }
        }

        /// <summary>
        /// GitHub 이슈 #22 - AllCandidatesMaxed(정상적인 성장 완료 상태)는 noCandidatesMessage가
        /// 아니라 allMaxedMessage를 써야 한다는 것을 GachaPullToast 단위로 확인한다 - NoCandidates
        /// 검사(바로 위)와 정확히 대칭인 케이스.
        /// </summary>
        private static void CheckGachaPullToastAllCandidatesMaxedZeroSuccess()
        {
            var events = new EventBus();
            var toasts = new List<string>();
            events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

            GachaPullToast.PublishIfIncomplete(
                events, 0, 300, GachaPullStopReason.AllCandidatesMaxed,
                noCandidatesMessage: "뽑기 콘텐츠를 불러오지 못했습니다. 잠시 후 다시 시도해주세요.",
                allMaxedMessage: "모든 스킬이 최대 레벨입니다.");

            if (toasts.Count != 1 || toasts[0] != "모든 스킬이 최대 레벨입니다.")
            {
                throw new Exception($"전부 만렙(0/300) 토스트가 기대와 다름: [{string.Join(", ", toasts)}]");
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

        /// <summary>
        /// GitHub 이슈 #22 완료 조건("재화 0, 비용보다 1 부족, 정확한 비용, 초과 잔액 경계가
        /// 검증됨")을 고정 비용 경로(CalculateMaxAffordableFixedCostPulls) 기준으로 정확히
        /// 그 네 값으로 확인한다 - 기존 검사(305/100, 50/100)는 계산기의 일반적인 정확성을
        /// 덮지만, 이슈가 콕 집은 경계값 자체를 명시적으로 검증하지는 않았다.
        /// </summary>
        private static void CheckGachaAffordabilityCalculatorFixedCostBoundaries()
        {
            const int cost = 100;

            if (GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(0, cost) != 0)
            {
                throw new Exception("잔액 0인데 0회가 아님");
            }

            if (GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(cost - 1, cost) != 0)
            {
                throw new Exception("비용보다 1 부족한 잔액인데 0회가 아님");
            }

            if (GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(cost, cost) != 1)
            {
                throw new Exception("잔액이 비용과 정확히 일치하는데 1회가 아님");
            }

            if (GachaAffordabilityCalculator.CalculateMaxAffordableFixedCostPulls(cost + 50, cost) != 1)
            {
                throw new Exception("비용을 초과했지만 2회분에는 못 미치는 잔액인데 1회가 아님");
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
        /// GitHub 이슈 #22 - 바로 위 검사(전부 만렙)와 대칭인 "진짜 데이터 오류" 시나리오. 카탈로그
        /// 자체가 비어있으면(스킬 0개) "모든 스킬이 최대 레벨입니다."가 아니라 별도의 데이터 오류
        /// 메시지가 떠야 한다 - 이 둘을 하나로 뭉쳐 처리하던 것이 이슈의 핵심 지적이었다("만렙이라
        /// 그런가 보다"로 오인되는 실제 콘텐츠 버그).
        /// </summary>
        private static void CheckSkillGachaServiceEmptyCatalogPublishesDataErrorToast()
        {
            SkillCatalogSO catalog = null;
            SkillGachaTableSO table = null;

            try
            {
                var events = new EventBus();
                var toasts = new List<string>();
                events.Subscribe<ToastMessageRequestedEvent>(evt => toasts.Add(evt.Message));

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", Array.Empty<SkillSO>());

                table = ScriptableObject.CreateInstance<SkillGachaTableSO>();
                SetPrivateField(table, "catalog", catalog);
                SetPrivateField(table, "weightPerSkill", 1);
                SetPrivateField(table, "ticketCostPerPull", 1);
                SetPrivateField(table, "currencyType", GachaCurrencyType.Ticket);

                var skillService = new SkillService(events);
                var scrolls = new SkillScrollService(events, initialScrolls: 1000);
                var currency = new CurrencyService(events);
                var service = new SkillGachaService(events, scrolls, currency, skillService, new[] { table });

                IReadOnlyList<SkillSO> results = service.Pull(0, 300);

                if (results.Count != 0)
                {
                    throw new Exception($"빈 카탈로그인데 {results.Count}건이 성공함(기대=0)");
                }

                if (toasts.Contains("모든 스킬이 최대 레벨입니다."))
                {
                    throw new Exception("빈 카탈로그(데이터 오류)인데 '전부 만렙' 메시지가 뜸 - 두 원인이 여전히 뭉쳐 있음");
                }

                if (!toasts.Contains("뽑기 콘텐츠를 불러오지 못했습니다. 잠시 후 다시 시도해주세요."))
                {
                    throw new Exception($"데이터 오류 안내 토스트가 발행되지 않음: [{string.Join(", ", toasts)}]");
                }
            }
            finally
            {
                if (table != null) UnityEngine.Object.DestroyImmediate(table);
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
        /// 후보 GameObject(CircleCollider2D + Health)를 origin 기준 오프셋 위치에 만든다. Health.
        /// IsDead는 Awake() 없이도(Edit Mode에서는 자동 호출 안 됨, section GY의 함정) C# 기본값
        /// false라 이 검사들에는 별도 Awake 리플렉션 호출이 필요 없다 - TakeDamage/Revive를 쓰는
        /// 검사와 다른 지점.
        /// </summary>
        private static GameObject CreateHealthCandidate(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = 0.3f;
            go.AddComponent<Health>();
            return go;
        }

        /// <summary>
        /// GitHub 이슈 #23 - NearestHealthScan.FindNearest가 (a) 실제로 가장 가까운 후보를 정확히
        /// 찾고, (b) 워밍업 이후 반복 호출에서 GC 할당이 0바이트임을 함께 확인한다. 실제 씬 콘텐츠와
        /// 절대 겹치지 않도록 원점에서 멀리 떨어진 좌표(100000,100000)를 기준으로 후보를 배치한다.
        /// Physics2D 쿼리는 Edit Mode에서도 동작하지만 마지막 SyncTransforms 이후의 상태만 반영하므로
        /// (section BO의 함정), 위치를 잡은 뒤 반드시 SyncTransforms를 호출한다.
        /// </summary>
        private static void CheckNearestHealthScanFindNearestCorrectAndZeroAlloc()
        {
            Vector3 origin = new Vector3(100000f, 100000f, 0f);
            var candidates = new List<GameObject>();

            try
            {
                // 0.5, 1.5, 2.5, ..., 4.5 만큼 떨어진 5개 후보 - 가장 가까운 건 인덱스 0(거리 0.5).
                for (int i = 0; i < 5; i++)
                {
                    candidates.Add(CreateHealthCandidate($"RegressionCheck_NHS_Candidate_{i}", origin + new Vector3(0.5f + i, 0f, 0f)));
                }

                Physics2D.SyncTransforms();

                Health nearest = NearestHealthScan.FindNearest(origin, 10f, ~0);

                if (nearest == null || nearest.gameObject != candidates[0])
                {
                    throw new Exception($"가장 가까운 후보(거리 0.5)를 못 찾음 - 결과: {(nearest == null ? "null" : nearest.gameObject.name)}");
                }

                // 범위를 벗어난 후보는 걸리지 않아야 한다 - Physics2D.OverlapCircle은 쿼리 반경뿐
                // 아니라 대상 콜라이더 자신의 반경(0.3)까지 합쳐서 겹침을 판정하므로, 최근접
                // 거리(0.5)에서 콜라이더 반경(0.3)을 뺀 값(0.2)보다 확실히 작은 쿼리 반경을 써야
                // 실제로 안 걸린다.
                Health outOfRange = NearestHealthScan.FindNearest(origin, 0.1f, ~0);

                if (outOfRange != null)
                {
                    throw new Exception("범위(0.1, 콜라이더 반경 0.3 포함해도 최근접 0.5보다 확실히 작음) 밖의 후보가 걸림");
                }

                // 워밍업(버퍼를 이 시나리오에 필요한 크기로 확장) 후 반복 호출은 0바이트여야 한다.
                NearestHealthScan.FindNearest(origin, 10f, ~0);

                long before = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 1000; i++)
                {
                    NearestHealthScan.FindNearest(origin, 10f, ~0);
                }

                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                if (allocated != 0)
                {
                    throw new Exception($"워밍업 후 1000회 호출에서 {allocated}바이트 할당됨(기대=0)");
                }
            }
            finally
            {
                foreach (GameObject go in candidates)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #23 - NearestHealthScan의 공유 버퍼가 가득 찼을 때(이 검사는 시작 크기 32를
        /// 넘는 40개 후보를 배치) 결과가 잘리지 않고 전부 확장·재시도를 거쳐 정확한 최근접을
        /// 찾는지 확인한다. 다른 검사가 이미 버퍼를 키워놨을 수 있어(static 공유) BufferGrowthCount
        /// 절대값이 아니라 "이 검사 도중 변화가 있었는지"만 비교한다.
        /// </summary>
        private static void CheckNearestHealthScanBufferGrowthPreservesCorrectness()
        {
            Vector3 origin = new Vector3(200000f, 200000f, 0f);
            var candidates = new List<GameObject>();

            try
            {
                const int candidateCount = 40;

                for (int i = 0; i < candidateCount; i++)
                {
                    // 가장 먼 것부터 배치하고 마지막(인덱스 candidateCount-1)을 가장 가깝게 둬서,
                    // 버퍼 뒤쪽에 몰린 결과도 놓치지 않는지 확인한다.
                    float distance = candidateCount - i;
                    candidates.Add(CreateHealthCandidate($"RegressionCheck_NHS_Growth_{i}", origin + new Vector3(distance, 0f, 0f)));
                }

                Physics2D.SyncTransforms();

                int growthBefore = NearestHealthScan.BufferGrowthCount;
                Health nearest = NearestHealthScan.FindNearest(origin, candidateCount + 10f, ~0);
                int growthAfter = NearestHealthScan.BufferGrowthCount;

                if (nearest == null || nearest.gameObject != candidates[candidateCount - 1])
                {
                    throw new Exception($"40개 후보 중 가장 가까운 것(거리 1)을 못 찾음 - 버퍼 확장 도중 결과 유실 의심. 결과: {(nearest == null ? "null" : nearest.gameObject.name)}");
                }

                if (growthAfter < growthBefore)
                {
                    throw new Exception("BufferGrowthCount가 감소함(있을 수 없는 상태)");
                }
            }
            finally
            {
                foreach (GameObject go in candidates)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #23 - CharacterSeparation이 (a) 겹친 두 캐릭터를 실제로 서로 밀어내고,
        /// (b) 워밍업 이후 반복 틱에서 GC 할당이 0바이트임을 함께 확인한다. Awake()/OnEnable()은
        /// Edit Mode에서 자동 호출되지 않고(ExecuteAlways 없음) OnEnable()이 TickerRegistration을
        /// 통해 GameTicker 등록까지 시도하므로, 그 경로를 아예 타지 않고 _collider/_bodyRadius를
        /// 리플렉션으로 직접 채운 뒤 ITickable.Tick()만 직접 호출한다.
        /// </summary>
        private static void CheckCharacterSeparationPushesApartAndZeroAlloc()
        {
            Vector3 basePosition = new Vector3(300000f, 300000f, 0f);
            GameObject goA = null;
            GameObject goB = null;

            try
            {
                goA = new GameObject("RegressionCheck_Separation_A");
                goA.transform.position = basePosition;
                var colliderA = goA.AddComponent<CircleCollider2D>();
                colliderA.radius = 0.5f;
                var separationA = goA.AddComponent<CharacterSeparation>();

                goB = new GameObject("RegressionCheck_Separation_B");
                // 반지름 합(1.0)보다 가깝게 겹쳐서 배치 - 밀어내야 하는 상태.
                goB.transform.position = basePosition + new Vector3(0.4f, 0f, 0f);
                var colliderB = goB.AddComponent<CircleCollider2D>();
                colliderB.radius = 0.5f;
                var separationB = goB.AddComponent<CharacterSeparation>();

                SetPrivateField(separationA, "_collider", colliderA);
                SetPrivateField(separationA, "_bodyRadius", 0.5f);
                SetPrivateField(separationB, "_collider", colliderB);
                SetPrivateField(separationB, "_bodyRadius", 0.5f);

                Physics2D.SyncTransforms();

                float distanceBefore = Vector3.Distance(goA.transform.position, goB.transform.position);

                ((ITickable)separationA).Tick(0.1f);
                ((ITickable)separationB).Tick(0.1f);
                Physics2D.SyncTransforms();

                float distanceAfter = Vector3.Distance(goA.transform.position, goB.transform.position);

                if (distanceAfter <= distanceBefore)
                {
                    throw new Exception($"겹친 두 캐릭터가 서로 안 밀려남 - 틱 전 거리 {distanceBefore:F3}, 틱 후 거리 {distanceAfter:F3}");
                }

                // 워밍업 후 반복 틱은 0바이트여야 한다.
                ((ITickable)separationA).Tick(0.1f);

                long before = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 1000; i++)
                {
                    ((ITickable)separationA).Tick(0.1f);
                }

                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                if (allocated != 0)
                {
                    throw new Exception($"워밍업 후 1000회 Tick에서 {allocated}바이트 할당됨(기대=0)");
                }
            }
            finally
            {
                if (goA != null) UnityEngine.Object.DestroyImmediate(goA);
                if (goB != null) UnityEngine.Object.DestroyImmediate(goB);
            }
        }

        /// <summary>
        /// PlayerSettings의 화면 방향이 세로 고정(Portrait)이고, 레거시 autorotate 플래그들도
        /// 가로 방향은 전부 꺼져 있는지 확인한다(GitHub 이슈 #12 - HUD/팝업이 전부 1080x1920
        /// 세로 기준으로만 설계돼 있는데 PlayerSettings는 AutoRotation으로 가로 회전까지 허용해
        /// 실기기에서 회전하면 레이아웃이 겹쳤다). defaultInterfaceOrientation이 AutoRotation이
        /// 아닌 한 allowedAutorotateTo* 플래그들은 런타임에 무시되지만, 설정 파일만 보고 오해하지
        /// 않도록(요청한 "PlayerSettings와 일치" 완료 조건) 전부 명시적으로 꺼져 있는지까지
        /// 확인한다. 순수 정적 설정값 확인이라 UI 레이캐스트 계열 검사(이슈 #11)와 달리 Edit/Play
        /// 모드 어느 쪽에서 돌려도 안정적이다.
        /// </summary>
        private static void CheckPlayerSettingsOrientationLockedToPortrait()
        {
            if (UnityEditor.PlayerSettings.defaultInterfaceOrientation != UnityEditor.UIOrientation.Portrait)
            {
                throw new Exception($"defaultInterfaceOrientation이 Portrait가 아님: {UnityEditor.PlayerSettings.defaultInterfaceOrientation}");
            }

            if (!UnityEditor.PlayerSettings.allowedAutorotateToPortrait)
            {
                throw new Exception("allowedAutorotateToPortrait가 꺼져 있음 - 세로 자체를 못 씀");
            }

            if (UnityEditor.PlayerSettings.allowedAutorotateToPortraitUpsideDown)
            {
                throw new Exception("allowedAutorotateToPortraitUpsideDown이 켜져 있음");
            }

            if (UnityEditor.PlayerSettings.allowedAutorotateToLandscapeLeft)
            {
                throw new Exception("allowedAutorotateToLandscapeLeft가 켜져 있음");
            }

            if (UnityEditor.PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                throw new Exception("allowedAutorotateToLandscapeRight가 켜져 있음");
            }
        }

        /// <summary>
        /// Core.Pooling.ObjectPool&lt;T&gt;.Release가 이중 반납을 거부해, 같은 인스턴스가 유휴
        /// 스택에 두 번 들어가는 것(그 결과 서로 다른 두 Get() 호출자가 같은 활성 객체를 받는 것)과
        /// Managers.PoolManager가 그 실패를 IPoolable.OnDespawned 중복 호출 없이 그대로 전파하는지
        /// 확인한다 - "사망 이벤트 처리와 세션 정리 경로가 같은 프레임에 동일 몬스터를 반납"하는
        /// 식으로 실제 발생할 수 있는 이중 반납 시나리오다. 독립된 PoolManager/임시 GameObject로
        /// 검증해 실제 씬 상태와 무관하다(임시 "프리팹"은 에셋일 필요 없이 평범한 GameObject 참조면
        /// 충분 - PoolManager는 이를 딕셔너리 키이자 Instantiate 원본으로만 쓴다).
        /// </summary>
        private static void CheckObjectPoolDoubleReleaseInvariant()
        {
            var pool = new Managers.PoolManager();
            pool.Initialize();

            GameObject prefab = null;

            try
            {
                prefab = new GameObject("RegressionCheck_PoolInvariant_Prefab");
                prefab.SetActive(false);
                pool.EnsurePool(prefab, 1, 4);

                GameObject instanceA = pool.Get(prefab, Vector3.zero, Quaternion.identity);

                bool firstRelease = pool.Release(instanceA);
                bool secondRelease = pool.Release(instanceA); // 이중 반납

                if (!firstRelease)
                {
                    throw new Exception("정상적인 첫 반납이 실패로 보고됨");
                }

                if (secondRelease)
                {
                    throw new Exception("이중 반납이 거부되지 않고 성공으로 보고됨");
                }

                GameObject reGetA = pool.Get(prefab, Vector3.one, Quaternion.identity);
                GameObject reGetB = pool.Get(prefab, new Vector3(2f, 2f, 2f), Quaternion.identity);

                if (ReferenceEquals(reGetA, reGetB))
                {
                    throw new Exception("연속 두 Get()이 같은 활성 인스턴스를 반환함(이중 반납으로 유휴 스택에 중복 삽입된 결과로 보임)");
                }
            }
            finally
            {
                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #30 완료 조건 - "이중 반납, 다른 풀 객체, 파괴된 객체, null 입력의 정책과
        /// 진단이 명확함". PoolManager.Release의 네 가지 비정상 입력 각각이 문서화된 정책대로
        /// 동작하는지 확인한다: null 인스턴스는 경고 로그 후 false(예외 없음), 파괴된 인스턴스도
        /// Unity의 오버로드된 == null이 이를 감지해 같은 경로로 처리됨(예외 없음), 이 PoolManager가
        /// 스폰한 적 없는(PooledInstance 태그가 없거나 등록되지 않은 프리팹을 가리키는) "다른 풀"
        /// 객체는 InvalidOperationException으로 명확히 실패한다. 세 경로 모두 다른 인스턴스의
        /// 정상 반납에 영향을 주지 않는지도 함께 확인한다.
        /// </summary>
        private static void CheckPoolManagerReleaseDiagnosticsForInvalidInputs()
        {
            var pool = new Managers.PoolManager();
            pool.Initialize();

            GameObject prefab = null;
            GameObject foreignObject = null;

            try
            {
                prefab = new GameObject("RegressionCheck_PoolDiagnostics_Prefab");
                prefab.SetActive(false);
                pool.EnsurePool(prefab, 1, 4);

                // null 입력 - 예외 없이 false
                if (pool.Release(null))
                {
                    throw new Exception("null 인스턴스 반납이 true를 반환함(기대: 예외 없이 false)");
                }

                // 파괴된 인스턴스 - Unity의 오버로드된 == null이 감지해 null과 동일하게 처리됨
                GameObject destroyed = pool.Get(prefab, Vector3.zero, Quaternion.identity);
                UnityEngine.Object.DestroyImmediate(destroyed);

                if (pool.Release(destroyed))
                {
                    throw new Exception("파괴된 인스턴스 반납이 true를 반환함(기대: 예외 없이 false)");
                }

                // 다른 풀(이 PoolManager가 스폰한 적 없는) 객체 - 명확한 예외로 실패해야 함
                foreignObject = new GameObject("RegressionCheck_PoolDiagnostics_ForeignObject");
                bool threw = false;

                try
                {
                    pool.Release(foreignObject);
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }

                if (!threw)
                {
                    throw new Exception("이 PoolManager가 스폰한 적 없는 객체 반납이 예외 없이 통과함(기대: InvalidOperationException)");
                }

                // 위 세 가지 비정상 입력을 거치는 동안 정상 인스턴스의 반납은 영향받지 않아야 함
                GameObject normal = pool.Get(prefab, Vector3.one, Quaternion.identity);

                if (!pool.Release(normal))
                {
                    throw new Exception("비정상 입력들을 거친 뒤 정상 인스턴스의 반납이 실패로 보고됨");
                }
            }
            finally
            {
                if (foreignObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(foreignObject);
                }

                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #30 완료 조건 - "IPoolable 콜백이 상태 전환당 한 번만 호출됨". Get()이
        /// OnSpawned를, Release()가 OnDespawned를 정확히 한 번씩만 호출하는지, 그리고 이중 반납
        /// 시도는 OnDespawned를 추가로 호출하지 않는지(ObjectPool.Release가 false를 반환하면
        /// PoolManager가 NotifyDespawned를 아예 건너뛰므로) 카운터로 직접 확인한다.
        /// </summary>
        private static void CheckPoolManagerIPoolableCalledOncePerTransition()
        {
            var pool = new Managers.PoolManager();
            pool.Initialize();

            GameObject prefab = null;

            try
            {
                prefab = new GameObject("RegressionCheck_PoolIPoolable_Prefab");
                prefab.SetActive(false);
                prefab.AddComponent<RegressionCheckPoolableCounter>();
                pool.EnsurePool(prefab, 1, 4);

                GameObject instance = pool.Get(prefab, Vector3.zero, Quaternion.identity);
                var counter = instance.GetComponent<RegressionCheckPoolableCounter>();

                if (counter.SpawnedCount != 1)
                {
                    throw new Exception($"Get() 1회 후 OnSpawned 호출 횟수가 {counter.SpawnedCount}(기대=1)");
                }

                if (counter.DespawnedCount != 0)
                {
                    throw new Exception($"Get() 1회 후 OnDespawned가 이미 {counter.DespawnedCount}회 호출됨(기대=0)");
                }

                pool.Release(instance);

                if (counter.DespawnedCount != 1)
                {
                    throw new Exception($"Release() 1회 후 OnDespawned 호출 횟수가 {counter.DespawnedCount}(기대=1)");
                }

                // 이중 반납 - ObjectPool.Release가 거부하므로 OnDespawned가 추가로 불리면 안 됨
                pool.Release(instance);

                if (counter.DespawnedCount != 1)
                {
                    throw new Exception($"이중 반납 시도 후 OnDespawned 호출 횟수가 {counter.DespawnedCount}(기대=1, 상태 전환당 한 번만)");
                }

                // 재대여 - OnSpawned가 다시 정확히 한 번 더 호출돼야 함(누적 2회)
                GameObject reGet = pool.Get(prefab, Vector3.one, Quaternion.identity);
                var reGetCounter = reGet.GetComponent<RegressionCheckPoolableCounter>();

                if (reGetCounter.SpawnedCount != 2)
                {
                    throw new Exception($"재대여 후 OnSpawned 누적 호출 횟수가 {reGetCounter.SpawnedCount}(기대=2)");
                }

                if (reGetCounter.DespawnedCount != 1)
                {
                    throw new Exception($"재대여 후 OnDespawned 누적 호출 횟수가 {reGetCounter.DespawnedCount}(기대=1, 변화 없어야 함)");
                }
            }
            finally
            {
                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #30 완료 조건 - "대량 반복 spawn/release 후 활성·유휴·전체 개수 불변조건
        /// 유지". maxSize보다 큰 규모로 Get/Release를 반복하며(일부는 즉시 반납, 일부는 잠시
        /// 대여 상태로 남김) 매 단계마다 CountActive+CountInactive가 실제로 살아있는 인스턴스
        /// 총량과 정확히 일치하는지 확인한다 - 이중 반납 버그가 재발하면 유휴 스택에 중복 삽입된
        /// 참조 때문에 CountInactive가 실제 살아있는 유휴 인스턴스 수보다 크게 보고될 것이다.
        /// </summary>
        private static void CheckObjectPoolMassRepeatedSpawnReleaseInvariant()
        {
            const int iterations = 500;
            // maxSize를 iterations보다 넉넉히 크게 잡아 Release()의 상한 초과(Object.Destroy)
            // 경로 자체를 절대 타지 않게 한다 - Object.Destroy는 Edit Mode에서 무시되고
            // "Destroy may not be called from edit mode!" 경고만 남긴 채 인스턴스가 실제로는
            // 파괴되지 않아(고아 GameObject로 남음), 이 검사의 핵심 목적(누적 정확성 검증)과
            // 무관한 콘솔 잡음/누수를 만든다(section GY가 같은 이유로 pool 크기를 totalUnits보다
            // 넉넉히 잡은 것과 동일한 관례). Get()이 항상 유휴 스택을 먼저 소비하므로 생성되는
            // 인스턴스 총량은 Get() 호출 횟수(iterations)를 절대 넘지 않는다.
            const int maxSize = iterations + 1;

            var pool = new Managers.PoolManager();
            pool.Initialize();

            GameObject prefab = null;
            var stillCheckedOut = new List<GameObject>();

            try
            {
                prefab = new GameObject("RegressionCheck_PoolMassRepeat_Prefab");
                prefab.SetActive(false);
                pool.EnsurePool(prefab, 4, maxSize);

                FieldInfo poolsField = typeof(Managers.PoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
                var pools = (Dictionary<GameObject, ObjectPool<GameObject>>)poolsField.GetValue(pool);
                ObjectPool<GameObject> objectPool = pools[prefab];

                for (int i = 0; i < iterations; i++)
                {
                    GameObject instance = pool.Get(prefab, Vector3.zero, Quaternion.identity);

                    // 3번에 1번꼴로는 당장 반납하지 않고 계속 대여 상태로 남긴다(대여 중인 인스턴스가
                    // 섞인 채로도 불변조건이 유지되는지 확인하기 위함).
                    if (i % 3 == 0)
                    {
                        stillCheckedOut.Add(instance);
                        continue;
                    }

                    if (!pool.Release(instance))
                    {
                        throw new Exception($"{i}번째 반복에서 정상 반납이 실패로 보고됨");
                    }

                    if (objectPool.CountActive != stillCheckedOut.Count)
                    {
                        throw new Exception($"{i}번째 반복 직후 대여 개수가 {objectPool.CountActive}(기대={stillCheckedOut.Count})");
                    }

                    if (objectPool.CountInactive > maxSize)
                    {
                        throw new Exception($"{i}번째 반복 직후 유휴 개수가 maxSize({maxSize})를 초과함(실제={objectPool.CountInactive}) - 반납이 상한을 무시하고 쌓이는 중");
                    }
                }

                int expectedActive = stillCheckedOut.Count;

                if (objectPool.CountActive != expectedActive)
                {
                    throw new Exception($"{iterations}회 반복 완료 후 대여 개수가 {objectPool.CountActive}(기대={expectedActive})");
                }

                // 남겨둔 대여 인스턴스를 전부 반납하면 대여 개수가 정확히 0이 되어야 한다.
                foreach (GameObject leftover in stillCheckedOut)
                {
                    if (!pool.Release(leftover))
                    {
                        throw new Exception("마무리 반납 단계에서 대여 중이던 인스턴스의 반납이 실패로 보고됨(중복 삽입/유실 의심)");
                    }
                }

                stillCheckedOut.Clear();

                if (objectPool.CountActive != 0)
                {
                    throw new Exception($"전량 반납 후 대여 개수가 {objectPool.CountActive}(기대=0)");
                }

                if (objectPool.CountInactive > maxSize)
                {
                    throw new Exception($"전량 반납 후 유휴 개수가 maxSize({maxSize})를 초과함(실제={objectPool.CountInactive})");
                }
            }
            finally
            {
                foreach (GameObject leftover in stillCheckedOut)
                {
                    if (leftover != null)
                    {
                        pool.Release(leftover);
                    }
                }

                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #30 검사 전용 IPoolable 카운터 컴포넌트 - OnSpawned/OnDespawned가 실제로
        /// 몇 번 호출되는지 세는 것 외에는 아무 상태도 갖지 않는다.
        /// </summary>
        private sealed class RegressionCheckPoolableCounter : MonoBehaviour, IPoolable
        {
            public int SpawnedCount { get; private set; }
            public int DespawnedCount { get; private set; }

            public void OnSpawned()
            {
                SpawnedCount++;
            }

            public void OnDespawned()
            {
                DespawnedCount++;
            }
        }

        /// <summary>
        /// GitHub 이슈 #31 재현 절차 그대로 - 실제 EquipmentSO 한 개를 보유 1개인 상태로 만들고
        /// TryConsume(definition, -5)를 호출한다. 수정 전에는 owned.Count < amount(1 < -5,
        /// false)를 그대로 통과해 owned.Count -= amount(1 - (-5) = 6)로 오히려 늘어났다(이슈의
        /// 실제 로그 "before=1, after=6"과 정확히 일치). 0도 함께 거부되는지, 그리고 두 경우 모두
        /// InventoryChangedEvent가 전혀 발행되지 않는지(완료 조건 "실패 시 InventoryChangedEvent와
        /// 저장 더티 상태가 발생하지 않음" - SaveService의 더티 플래그는 이 이벤트 구독으로만
        /// 세워지므로, 이벤트가 안 뜨면 더티 상태도 자동으로 안 뜬다) 함께 확인한다.
        /// </summary>
        private static void CheckInventoryServiceTryConsumeRejectsNonPositiveAmount()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            inventory.Initialize();

            EquipmentSO equipment = null;
            int inventoryChangedCount = 0;
            Action<InventoryChangedEvent> onChanged = _ => inventoryChangedCount++;

            try
            {
                equipment = ScriptableObject.CreateInstance<EquipmentSO>();
                events.Publish(new ItemDroppedEvent(equipment)); // owned.Count = 1

                events.Subscribe(onChanged);

                if (inventory.TryConsume(equipment, -5))
                {
                    throw new Exception("TryConsume(-5)가 true를 반환함(GitHub 이슈 #31 재현)");
                }

                if (!inventory.TryGet(equipment, out OwnedEquipment afterNegative) || afterNegative.Count != 1)
                {
                    throw new Exception($"음수 소모 시도 후 Count가 {(inventory.TryGet(equipment, out OwnedEquipment o) ? o.Count.ToString() : "라인 소실")}(기대=1, 변화 없어야 함)");
                }

                if (inventory.TryConsume(equipment, 0))
                {
                    throw new Exception("TryConsume(0)이 true를 반환함(0은 상태 변경이 없으므로 거부돼야 함)");
                }

                if (!inventory.TryGet(equipment, out OwnedEquipment afterZero) || afterZero.Count != 1)
                {
                    throw new Exception($"0 소모 시도 후 Count가 {afterZero.Count}(기대=1, 변화 없어야 함)");
                }

                if (inventoryChangedCount != 0)
                {
                    throw new Exception($"실패한 TryConsume 호출들이 InventoryChangedEvent를 {inventoryChangedCount}회 발행함(기대=0)");
                }
            }
            finally
            {
                events.Unsubscribe(onChanged);
                inventory.Shutdown();

                if (equipment != null)
                {
                    UnityEngine.Object.DestroyImmediate(equipment);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #31 완료 조건 - "int.MinValue, int.MaxValue, 현재 보유량 정확히 일치/1
        /// 부족 경계 검증". amount=int.MinValue는 amount<=0 가드에서 즉시 거부되어(뺄셈 자체가
        /// 일어나지 않아 오버플로 경로를 안 탐), amount=int.MaxValue는 보유량 부족으로 거부,
        /// 보유량과 정확히 일치하는 소모는 성공해 Count가 정확히 0이 되고, 그 상태에서 1개
        /// 부족한 소모(사실상 아무것도 없는데 1개 요청)는 실패해야 한다.
        /// </summary>
        private static void CheckInventoryServiceTryConsumeBoundaryAmounts()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            inventory.Initialize();

            EquipmentSO equipment = null;
            const int owned = 3;

            try
            {
                equipment = ScriptableObject.CreateInstance<EquipmentSO>();

                for (int i = 0; i < owned; i++)
                {
                    events.Publish(new ItemDroppedEvent(equipment));
                }

                if (inventory.TryConsume(equipment, int.MinValue))
                {
                    throw new Exception("TryConsume(int.MinValue)가 true를 반환함(오버플로 경로가 열려 있을 위험)");
                }

                if (!inventory.TryGet(equipment, out OwnedEquipment afterMin) || afterMin.Count != owned)
                {
                    throw new Exception($"int.MinValue 소모 시도 후 Count가 {afterMin.Count}(기대={owned}, 변화 없어야 함)");
                }

                if (inventory.TryConsume(equipment, int.MaxValue))
                {
                    throw new Exception("TryConsume(int.MaxValue)가 true를 반환함(보유량을 훨씬 초과하는데도 성공)");
                }

                // 보유량과 정확히 일치 - 성공해야 하고 Count가 정확히 0이 되어야 한다.
                if (!inventory.TryConsume(equipment, owned))
                {
                    throw new Exception("보유량과 정확히 일치하는 소모가 실패로 보고됨");
                }

                if (!inventory.TryGet(equipment, out OwnedEquipment afterExact) || afterExact.Count != 0)
                {
                    throw new Exception($"정확히 일치하는 소모 후 Count가 {afterExact.Count}(기대=0)");
                }

                // 이제 0개인데 1개를 요청 - 1개 부족 경계, 실패해야 한다.
                if (inventory.TryConsume(equipment, 1))
                {
                    throw new Exception("보유량이 0인데 1개 소모 요청이 성공으로 보고됨(1개 부족 경계 위반)");
                }
            }
            finally
            {
                inventory.Shutdown();

                if (equipment != null)
                {
                    UnityEngine.Object.DestroyImmediate(equipment);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #31 - AddEnhancementLevel(definition, levels)도 음수 레벨을 허용하던 문제.
        /// levels <= 0은 조용히 무시되고(CurrencyService.AddGold 등이 이미 쓰는 "Add*는 조용히
        /// no-op" 관례, 이슈 #8과 동일 방향), 오버플로는 long 계산 후 int.MaxValue로 saturate돼야
        /// 한다(EquipmentEnhancementService.GetNextStoneCost가 이미 쓰는 것과 동일한 관례).
        /// </summary>
        private static void CheckInventoryServiceAddEnhancementLevelGuardsAndSaturates()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            inventory.Initialize();

            EquipmentSO equipment = null;
            int inventoryChangedCount = 0;
            Action<InventoryChangedEvent> onChanged = _ => inventoryChangedCount++;

            try
            {
                equipment = ScriptableObject.CreateInstance<EquipmentSO>();
                events.Publish(new ItemDroppedEvent(equipment));

                events.Subscribe(onChanged);

                inventory.AddEnhancementLevel(equipment, -3);
                inventory.AddEnhancementLevel(equipment, 0);

                if (!inventory.TryGet(equipment, out OwnedEquipment afterNonPositive) || afterNonPositive.EnhancementLevel != 0)
                {
                    throw new Exception($"음수/0 레벨 추가 후 EnhancementLevel이 {afterNonPositive.EnhancementLevel}(기대=0, 변화 없어야 함)");
                }

                if (inventoryChangedCount != 0)
                {
                    throw new Exception($"음수/0 레벨 추가가 InventoryChangedEvent를 {inventoryChangedCount}회 발행함(기대=0)");
                }

                // 오버플로 saturate 확인 - 최대치 근처로 올려둔 뒤 큰 값을 한 번 더 더한다.
                inventory.AddEnhancementLevel(equipment, int.MaxValue - 1);

                if (!inventory.TryGet(equipment, out OwnedEquipment afterFirst) || afterFirst.EnhancementLevel != int.MaxValue - 1)
                {
                    throw new Exception($"정상 범위 내 레벨 추가 후 EnhancementLevel이 {afterFirst.EnhancementLevel}(기대={int.MaxValue - 1})");
                }

                inventory.AddEnhancementLevel(equipment, 100);

                if (!inventory.TryGet(equipment, out OwnedEquipment afterOverflow) || afterOverflow.EnhancementLevel != int.MaxValue)
                {
                    throw new Exception($"오버플로 유발 후 EnhancementLevel이 {afterOverflow.EnhancementLevel}(기대={int.MaxValue}, saturate돼야 함)");
                }
            }
            finally
            {
                events.Unsubscribe(onChanged);
                inventory.Shutdown();

                if (equipment != null)
                {
                    UnityEngine.Object.DestroyImmediate(equipment);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #31 완료 조건 - "복원 시 음수 수량/레벨을 안전하게 거부·정규화하고 유효
        /// 항목은 계속 복원함". 네 항목(음수 Count, 음수 EnhancementLevel, 카탈로그에 없는
        /// StableId, 완전히 유효한 항목)을 한 스냅샷에 섞어 넣고, RestoreResult의 각 사유별
        /// 카운트가 정확한지 + 실제로 유효한 항목 하나만 인벤토리에 반영됐는지 확인한다.
        /// </summary>
        private static void CheckInventoryServiceRestoreSnapshotDiscardsInvalidEntries()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            inventory.Initialize();

            EquipmentSO validEquipment = null;
            EquipmentCatalogSO catalog = null;

            try
            {
                validEquipment = ScriptableObject.CreateInstance<EquipmentSO>();
                SetPrivateString(validEquipment, "stableId", "issue31-valid");

                catalog = ScriptableObject.CreateInstance<EquipmentCatalogSO>();
                SetPrivateField(catalog, "items", new[] { validEquipment });

                var snapshot = new[]
                {
                    new InventoryService.OwnedEquipmentSnapshot { StableId = "issue31-valid", Count = -1, EnhancementLevel = 0 },
                    new InventoryService.OwnedEquipmentSnapshot { StableId = "issue31-valid", Count = 0, EnhancementLevel = -1 },
                    new InventoryService.OwnedEquipmentSnapshot { StableId = "issue31-missing-from-catalog", Count = 1, EnhancementLevel = 0 },
                    new InventoryService.OwnedEquipmentSnapshot { StableId = "issue31-valid", Count = 5, EnhancementLevel = 2 },
                };

                InventoryService.RestoreResult result = inventory.RestoreSnapshot(snapshot, catalog);

                if (result.RestoredCount != 1)
                {
                    throw new Exception($"복원 성공 건수가 {result.RestoredCount}(기대=1)");
                }

                if (result.DiscardedNegativeCount != 1)
                {
                    throw new Exception($"음수 Count로 폐기된 건수가 {result.DiscardedNegativeCount}(기대=1)");
                }

                if (result.DiscardedNegativeEnhancementLevel != 1)
                {
                    throw new Exception($"음수 EnhancementLevel로 폐기된 건수가 {result.DiscardedNegativeEnhancementLevel}(기대=1)");
                }

                if (result.DiscardedMissingCatalogEntry != 1)
                {
                    throw new Exception($"카탈로그 없음으로 폐기된 건수가 {result.DiscardedMissingCatalogEntry}(기대=1)");
                }

                if (!result.HasDiscardedEntries || result.TotalDiscarded != 3)
                {
                    throw new Exception($"TotalDiscarded가 {result.TotalDiscarded}(기대=3)");
                }

                if (!inventory.TryGet(validEquipment, out OwnedEquipment restored) || restored.Count != 5 || restored.EnhancementLevel != 2)
                {
                    throw new Exception("마지막 유효 항목(Count=5, Level=2)이 정확히 복원되지 않음 - 손상된 항목들이 유효 항목 복원을 막았을 가능성");
                }
            }
            finally
            {
                inventory.Shutdown();

                if (validEquipment != null)
                {
                    UnityEngine.Object.DestroyImmediate(validEquipment);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #31 완료 조건 - "여러 번 빠르게 소모해도 수량이 0 미만 또는 오버플로되지
        /// 않음". Count=3에서 1개씩 4번 연속 소모 - 앞의 3번은 성공하며 Count가 3→2→1→0으로
        /// 정확히 줄고, 4번째는 실패해 Count가 음수로 내려가지 않는지 확인한다. TryConsume이
        /// 매 호출마다 그 시점의 owned.Count를 다시 확인하므로 이는 새 코드 경로가 아니라 이미
        /// 존재하던 검사(owned.Count < amount)가 amount>0 보장 하에서도 여전히 정확히 동작하는지
        /// 확인하는 회귀 방지 성격이 강하다.
        /// </summary>
        private static void CheckInventoryServiceTryConsumeRepeatedCallsNeverGoesBelowZero()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            inventory.Initialize();

            EquipmentSO equipment = null;

            try
            {
                equipment = ScriptableObject.CreateInstance<EquipmentSO>();

                for (int i = 0; i < 3; i++)
                {
                    events.Publish(new ItemDroppedEvent(equipment));
                }

                var results = new bool[4];

                for (int i = 0; i < 4; i++)
                {
                    results[i] = inventory.TryConsume(equipment, 1);
                }

                if (!results[0] || !results[1] || !results[2])
                {
                    throw new Exception($"연속 소모 1~3회차 결과가 [{results[0]},{results[1]},{results[2]}](기대=전부 true)");
                }

                if (results[3])
                {
                    throw new Exception("보유량이 바닥난 4회차 소모가 성공으로 보고됨(Count가 음수로 내려갈 위험)");
                }

                if (!inventory.TryGet(equipment, out OwnedEquipment final) || final.Count != 0)
                {
                    throw new Exception($"연속 소모 종료 후 Count가 {final.Count}(기대=0, 음수 불가)");
                }
            }
            finally
            {
                inventory.Shutdown();

                if (equipment != null)
                {
                    UnityEngine.Object.DestroyImmediate(equipment);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #31 완료 조건 - "합성·강화 SO에 잘못된 비용이 있어도 수량이 증가하지
        /// 않음". EquipmentEnhancementConfigSO.DuplicatesRequiredPerLevel을 리플렉션으로 음수로
        /// 오염시킨 실제 시나리오 - 이 경우 owned.Count < duplicatesRequired + 1 검사 자체가
        /// (음수+1이라) 사실상 항상 통과해버리므로, TryEnhance 내부의 "보유량 확인" 방어선이
        /// 무력화된다. 그래도 InventoryService.TryConsume 자신이 amount <= 0을 거부하는 최종
        /// 방어선이 되어, 실제 EquipmentEnhancementService.TryEnhance를 통해 호출해도 Count가
        /// 늘어나지 않는지 end-to-end로 확인한다(이슈가 명시한 "UI/서비스 호출부 검증에만
        /// 의존하지 말고 상태를 소유한 서비스가 최종 방어선이 된다" 요구사항 그대로).
        /// </summary>
        private static void CheckEquipmentEnhancementServiceMisconfiguredNegativeCostDoesNotInflate()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            inventory.Initialize();
            var stones = new EnhancementStoneService(events, initialStones: 1_000_000);

            EquipmentSO equipment = null;
            EquipmentEnhancementConfigSO config = null;

            try
            {
                equipment = ScriptableObject.CreateInstance<EquipmentSO>();
                events.Publish(new ItemDroppedEvent(equipment)); // owned.Count = 1

                config = ScriptableObject.CreateInstance<EquipmentEnhancementConfigSO>();
                SetPrivateField(config, "duplicatesRequiredPerLevel", -5); // 오염된 SO 값
                SetPrivateField(config, "stoneCostBase", 10);
                SetPrivateField(config, "stoneCostIncreasePerLevel", 0);
                SetPrivateField(config, "maxLevel", 100);

                var enhancement = new EquipmentEnhancementService(inventory, stones, config, null);

                bool succeeded = enhancement.TryEnhance(equipment);

                if (!inventory.TryGet(equipment, out OwnedEquipment afterEnhance))
                {
                    throw new Exception("강화 시도 후 라인 자체가 사라짐");
                }

                if (afterEnhance.Count > 1)
                {
                    throw new Exception($"음수 DuplicatesRequiredPerLevel 설정으로 TryEnhance(succeeded={succeeded}) 이후 Count가 {afterEnhance.Count}(기대<=1, 재료 무한 복제 위험)");
                }
            }
            finally
            {
                inventory.Shutdown();

                if (equipment != null)
                {
                    UnityEngine.Object.DestroyImmediate(equipment);
                }

                if (config != null)
                {
                    UnityEngine.Object.DestroyImmediate(config);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #32 재현 절차 그대로 - 레벨 0(미습득)인 실제 스킬을 슬롯 0/1에 동시에
        /// 넣은 스냅샷을 복원한다. 수정 전에는 두 슬롯 모두 채워졌다(이슈의 실제 로그
        /// "TryEquipUnlearned=False"인데 "restoredSlot0=True, restoredSlot1=True"). 수정 후에는
        /// RestoreResult.DiscardedUnlearnedSkill=2, RestoredCount=0이어야 하고 두 슬롯 모두 비어야
        /// 한다.
        /// </summary>
        private static void CheckSkillLoadoutServiceRestoreSnapshotRejectsUnlearnedSkill()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var loadout = new SkillLoadoutService(events, skillService);

            SkillSO skill = null;
            SkillCatalogSO catalog = null;

            try
            {
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(skill, "stableId", "issue32-unlearned");

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                // 레벨을 전혀 부여하지 않았으므로 GetLevel(skill) == 0(미습득) 그대로다.
                if (loadout.TryEquip(0, skill))
                {
                    throw new Exception("정상 TryEquip이 미습득 스킬을 장착시킴(테스트 전제 자체가 깨짐)");
                }

                var snapshot = new[]
                {
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 0, StableId = "issue32-unlearned" },
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 1, StableId = "issue32-unlearned" },
                };

                SkillLoadoutService.RestoreResult result = loadout.RestoreSnapshot(snapshot, catalog);

                if (result.RestoredCount != 0)
                {
                    throw new Exception($"미습득 스킬 복원 성공 건수가 {result.RestoredCount}(기대=0, GitHub 이슈 #32 재현)");
                }

                if (result.DiscardedUnlearnedSkill != 2)
                {
                    throw new Exception($"미습득으로 폐기된 건수가 {result.DiscardedUnlearnedSkill}(기대=2)");
                }

                if (loadout.GetEquipped(0) != null || loadout.GetEquipped(1) != null)
                {
                    throw new Exception("미습득 스킬 복원 시도 후에도 슬롯 0/1 중 하나 이상이 채워짐");
                }
            }
            finally
            {
                if (skill != null)
                {
                    UnityEngine.Object.DestroyImmediate(skill);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #32 완료 조건 - "한 스킬은 최대 한 슬롯에만 결정적으로 복원됨". 이미
        /// 습득(레벨 1)한 스킬 하나를 슬롯 3과 슬롯 0에 동시에 넣되, 배열 자체는 일부러 슬롯
        /// 역순(3, 0)으로 저장해둔다 - RestoreSnapshot이 SlotIndex 오름차순으로 정렬한 뒤
        /// 처리해야 저장 순서와 무관하게 항상 "더 낮은 슬롯 인덱스가 우선"하는 결정적 결과가
        /// 나온다는 것을 함께 검증한다.
        /// </summary>
        private static void CheckSkillLoadoutServiceRestoreSnapshotDuplicateFirstSlotWins()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var loadout = new SkillLoadoutService(events, skillService);

            SkillSO skill = null;
            SkillCatalogSO catalog = null;

            try
            {
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(skill, "stableId", "issue32-dup");

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                skillService.RestoreSnapshot(
                    new[] { new SkillService.SkillLevelSnapshot { StableId = "issue32-dup", Level = 1 } },
                    catalog);

                var snapshot = new[]
                {
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 3, StableId = "issue32-dup" },
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 0, StableId = "issue32-dup" },
                };

                SkillLoadoutService.RestoreResult result = loadout.RestoreSnapshot(snapshot, catalog);

                if (result.RestoredCount != 1)
                {
                    throw new Exception($"복원 성공 건수가 {result.RestoredCount}(기대=1)");
                }

                if (result.DiscardedDuplicateDefinition != 1)
                {
                    throw new Exception($"중복으로 폐기된 건수가 {result.DiscardedDuplicateDefinition}(기대=1)");
                }

                if (loadout.GetEquipped(0) != skill)
                {
                    throw new Exception("낮은 슬롯 인덱스(0)가 아니라 다른 슬롯이 우선됨(결정적 정책 위반)");
                }

                if (loadout.GetEquipped(3) != null)
                {
                    throw new Exception("더 높은 슬롯 인덱스(3)에도 같은 스킬이 남아있음(중복 장착)");
                }
            }
            finally
            {
                if (skill != null)
                {
                    UnityEngine.Object.DestroyImmediate(skill);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #32 완료 조건 - "연속 복원 전에 기존 슬롯과 enabled 상태가 기본값으로
        /// 초기화됨"(슬롯 절반). 첫 복원으로 슬롯 0을 채운 뒤, 두 번째 복원을 빈 스냅샷으로
        /// 호출하면 슬롯 0이 이전 저장의 잔류값 없이 반드시 비어야 한다 - 계정 전환/데이터
        /// 초기화 후 재시딩 시나리오를 그대로 재현한다.
        /// </summary>
        private static void CheckSkillLoadoutServiceRestoreSnapshotClearsOnRepeatedCalls()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var loadout = new SkillLoadoutService(events, skillService);

            SkillSO skill = null;
            SkillCatalogSO catalog = null;

            try
            {
                skill = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(skill, "stableId", "issue32-stale");

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", new[] { skill });

                skillService.RestoreSnapshot(
                    new[] { new SkillService.SkillLevelSnapshot { StableId = "issue32-stale", Level = 1 } },
                    catalog);

                loadout.RestoreSnapshot(
                    new[] { new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 0, StableId = "issue32-stale" } },
                    catalog);

                if (loadout.GetEquipped(0) != skill)
                {
                    throw new Exception("첫 복원 직후 슬롯 0이 채워지지 않음(테스트 전제 자체가 깨짐)");
                }

                // 두 번째(다른) 저장을 복원 - 이번엔 아무 슬롯도 채우지 않는 빈 스냅샷.
                loadout.RestoreSnapshot(Array.Empty<SkillLoadoutService.SkillLoadoutSnapshotEntry>(), catalog);

                if (loadout.GetEquipped(0) != null)
                {
                    throw new Exception("두 번째(빈) 복원 이후에도 슬롯 0에 이전 저장의 스킬이 잔류함");
                }
            }
            finally
            {
                if (skill != null)
                {
                    UnityEngine.Object.DestroyImmediate(skill);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #32 완료 조건 - "연속 복원 전에 기존 슬롯과 enabled 상태가 기본값으로
        /// 초기화됨"(나머지 절반, enabled). 첫 복원으로 슬롯 2를 꺼둔 뒤, 두 번째 복원을 빈
        /// 배열로 호출하면 슬롯 2가 기본값(켜짐)으로 되돌아가야 한다.
        /// </summary>
        private static void CheckSkillLoadoutServiceRestoreDisabledSlotsResetsOnRepeatedCalls()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var loadout = new SkillLoadoutService(events, skillService);

            loadout.RestoreDisabledSlots(new[] { 2 });

            if (loadout.IsEnabled(2))
            {
                throw new Exception("첫 복원 직후 슬롯 2가 여전히 켜짐(테스트 전제 자체가 깨짐)");
            }

            loadout.RestoreDisabledSlots(Array.Empty<int>());

            if (!loadout.IsEnabled(2))
            {
                throw new Exception("두 번째(빈) 복원 이후에도 슬롯 2가 꺼진 채로 남음(이전 저장의 잔류 상태)");
            }
        }

        /// <summary>
        /// GitHub 이슈 #32 완료 조건 - "잘못된 슬롯 인덱스·빈/알 수 없는 ID·중복·미습득 항목을
        /// 각각 검증함" + "유효 항목은 손상 항목과 함께 있어도 정상 복원됨". 범위 밖 슬롯, 카탈로그에
        /// 없는 StableId, 미습득 스킬, 이미 앞에서 배정된 스킬과의 중복, 완전히 유효한 항목을 한
        /// 스냅샷에 섞어 넣고 RestoreResult의 사유별 카운트와 실제 복원 결과를 확인한다.
        /// </summary>
        private static void CheckSkillLoadoutServiceRestoreSnapshotMixedEntries()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var loadout = new SkillLoadoutService(events, skillService);

            SkillSO learnedA = null;
            SkillSO unlearnedB = null;
            SkillCatalogSO catalog = null;

            try
            {
                learnedA = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(learnedA, "stableId", "issue32-mixed-learned");

                unlearnedB = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(unlearnedB, "stableId", "issue32-mixed-unlearned");

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", new[] { learnedA, unlearnedB });

                skillService.RestoreSnapshot(
                    new[] { new SkillService.SkillLevelSnapshot { StableId = "issue32-mixed-learned", Level = 1 } },
                    catalog);
                // unlearnedB는 의도적으로 레벨을 부여하지 않는다(0강 그대로).

                var snapshot = new[]
                {
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 99, StableId = "issue32-mixed-learned" }, // 범위 밖
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 1, StableId = "issue32-does-not-exist" }, // 카탈로그 없음
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 2, StableId = "issue32-mixed-unlearned" }, // 미습득
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 4, StableId = "issue32-mixed-learned" }, // 유효(먼저 슬롯 0을 차지한 뒤엔 중복)
                    new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 0, StableId = "issue32-mixed-learned" }, // 유효(가장 낮은 슬롯)
                };

                SkillLoadoutService.RestoreResult result = loadout.RestoreSnapshot(snapshot, catalog);

                if (result.RestoredCount != 1)
                {
                    throw new Exception($"복원 성공 건수가 {result.RestoredCount}(기대=1)");
                }

                if (result.DiscardedOutOfRangeSlot != 1)
                {
                    throw new Exception($"범위 밖 슬롯으로 폐기된 건수가 {result.DiscardedOutOfRangeSlot}(기대=1)");
                }

                if (result.DiscardedMissingCatalogEntry != 1)
                {
                    throw new Exception($"카탈로그 없음으로 폐기된 건수가 {result.DiscardedMissingCatalogEntry}(기대=1)");
                }

                if (result.DiscardedUnlearnedSkill != 1)
                {
                    throw new Exception($"미습득으로 폐기된 건수가 {result.DiscardedUnlearnedSkill}(기대=1)");
                }

                if (result.DiscardedDuplicateDefinition != 1)
                {
                    throw new Exception($"중복으로 폐기된 건수가 {result.DiscardedDuplicateDefinition}(기대=1)");
                }

                if (loadout.GetEquipped(0) != learnedA)
                {
                    throw new Exception("가장 낮은 슬롯(0)에 유효 항목이 복원되지 않음");
                }

                if (loadout.GetEquipped(4) != null)
                {
                    throw new Exception("더 높은 슬롯(4)에 중복 항목이 남아있음");
                }
            }
            finally
            {
                if (learnedA != null)
                {
                    UnityEngine.Object.DestroyImmediate(learnedA);
                }

                if (unlearnedB != null)
                {
                    UnityEngine.Object.DestroyImmediate(unlearnedB);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #32 완료 조건 - "복원 후 TryEquip, 자동 시전, 저장 재내보내기의 불변조건이
        /// 유지됨". 유효한 복원(슬롯 0에 스킬 A) 직후: (1) 다른 슬롯에 다른 학습된 스킬 B를
        /// TryEquip하면 정상적으로 장착되고 A는 그대로 남아있는지(복원된 상태가 정상 API를
        /// 방해하지 않음), (2) ExportSnapshot이 복원 직후 상태를 정확히 그대로(딱 1건, 슬롯 0,
        /// 스킬 A) 재직렬화하는지 확인한다.
        /// </summary>
        private static void CheckSkillLoadoutServiceRestoreSnapshotRoundTripInvariants()
        {
            var events = new EventBus();
            var skillService = new SkillService(events);
            var loadout = new SkillLoadoutService(events, skillService);

            SkillSO skillA = null;
            SkillSO skillB = null;
            SkillCatalogSO catalog = null;

            try
            {
                skillA = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(skillA, "stableId", "issue32-roundtrip-a");

                skillB = ScriptableObject.CreateInstance<SkillSO>();
                SetPrivateString(skillB, "stableId", "issue32-roundtrip-b");

                catalog = ScriptableObject.CreateInstance<SkillCatalogSO>();
                SetPrivateField(catalog, "skills", new[] { skillA, skillB });

                skillService.RestoreSnapshot(
                    new[]
                    {
                        new SkillService.SkillLevelSnapshot { StableId = "issue32-roundtrip-a", Level = 1 },
                        new SkillService.SkillLevelSnapshot { StableId = "issue32-roundtrip-b", Level = 1 },
                    },
                    catalog);

                loadout.RestoreSnapshot(
                    new[] { new SkillLoadoutService.SkillLoadoutSnapshotEntry { SlotIndex = 0, StableId = "issue32-roundtrip-a" } },
                    catalog);

                if (!loadout.TryEquip(1, skillB))
                {
                    throw new Exception("복원된 상태 위에서 정상 TryEquip이 실패함");
                }

                if (loadout.GetEquipped(0) != skillA || loadout.GetEquipped(1) != skillB)
                {
                    throw new Exception("복원 이후 정상 TryEquip을 거쳤는데 슬롯 상태가 기대와 다름");
                }

                SkillLoadoutService.SkillLoadoutSnapshotEntry[] exported = loadout.ExportSnapshot(catalog);

                if (exported.Length != 2)
                {
                    throw new Exception($"복원+정상 장착 이후 ExportSnapshot 항목 수가 {exported.Length}(기대=2)");
                }
            }
            finally
            {
                if (skillA != null)
                {
                    UnityEngine.Object.DestroyImmediate(skillA);
                }

                if (skillB != null)
                {
                    UnityEngine.Object.DestroyImmediate(skillB);
                }

                if (catalog != null)
                {
                    UnityEngine.Object.DestroyImmediate(catalog);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "홀수 TotalUnitCount... 경계를 검증함". TacticSpawnEntry.
        /// PairCount(Stage.MonsterSpawner.TickTactics/Stage.StageProgressTracker.CalculateTotal/
        /// Offline.OfflineStageSimulator가 공유하는 단일 진실 공급원)가 홀수·0·음수 TotalUnitCount를
        /// 정확히 정수 나눗셈+0 하한으로 처리하는지 확인한다.
        /// </summary>
        private static void CheckTacticSpawnEntryPairCountTruncation()
        {
            AssertPairCount(36, 18);
            AssertPairCount(5, 2);
            AssertPairCount(1, 0);
            AssertPairCount(0, 0);
            AssertPairCount(-3, 0);
        }

        private static void AssertPairCount(int totalUnitCount, int expectedPairCount)
        {
            var entry = new TacticSpawnEntry();
            SetPrivateFieldOnPlainObject(entry, "totalUnitCount", totalUnitCount);

            if (entry.PairCount != expectedPairCount)
            {
                throw new Exception($"TotalUnitCount={totalUnitCount}일 때 PairCount가 {entry.PairCount}(기대={expectedPairCount})");
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 재현 절차 그대로 - 실제 Stage_1_40.asset(1-40, 이슈가 직접 대조한 그
        /// 스테이지)을 프로젝트에서 로드해, 실전 클리어 판정 공식(Stage.StageProgressTracker.
        /// CalculateTotal, private static)과 수정된 OfflineStageSimulator의 유효 스폰 구성 총합을
        /// 각각 리플렉션으로 계산해 비교한다. 이슈의 실측값(normal=5, tactics=36,
        /// runtimeClearTotal=41, 수정 전 offlineTotal=5)대로 41로 정확히 일치해야 한다 - 수정
        /// 전이었다면 오프라인 쪽이 5(일반 웨이브만)로 나와 이 검사가 실패했을 것이다.
        /// </summary>
        private static void CheckOfflineStageSimulatorRealStage1_40TotalMatchesRuntimeFormula()
        {
            StageSO stage = LoadRealStageAsset("Stage_1_40");

            if (stage == null)
            {
                throw new Exception("Stage_1_40.asset을 프로젝트에서 찾지 못함(경로/이름이 바뀌었는지 확인)");
            }

            MethodInfo calculateTotal = typeof(StageProgressTracker).GetMethod("CalculateTotal", BindingFlags.NonPublic | BindingFlags.Static);

            if (calculateTotal == null)
            {
                throw new Exception("StageProgressTracker.CalculateTotal을 찾지 못함(리팩터링으로 이름/시그니처가 바뀌었는지 확인)");
            }

            int runtimeTotal = (int)calculateTotal.Invoke(null, new object[] { stage });

            MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
            object groups = buildGroups.Invoke(null, new object[] { stage });

            int offlineTotal = SumEffectiveSpawnGroupCounts(groups);

            if (runtimeTotal != 41)
            {
                throw new Exception($"Stage_1_40의 실전 클리어 판정 총량이 {runtimeTotal}(기대=41, 이슈 실측값과 다름 - 콘텐츠가 바뀌었는지 확인)");
            }

            if (offlineTotal != 41)
            {
                throw new Exception($"Stage_1_40의 오프라인 유효 스폰 구성 총량이 {offlineTotal}(기대=41, GitHub 이슈 #33 재현 - 전술 대형 36명이 누락됐을 가능성)");
            }

            if (runtimeTotal != offlineTotal)
            {
                throw new Exception($"실전 총량({runtimeTotal})과 오프라인 총량({offlineTotal})이 어긋남");
            }
        }

        /// <summary>
        /// EffectiveSpawnGroup(private readonly struct)의 List에서 Count 필드 합을 반올림해
        /// 정수로 돌려준다 - 리플렉션 전용 검사 헬퍼.
        /// </summary>
        private static int SumEffectiveSpawnGroupCounts(object groupList)
        {
            var list = (System.Collections.IEnumerable)groupList;
            Type groupType = null;
            float total = 0f;

            foreach (object group in list)
            {
                groupType ??= group.GetType();
                total += (float)groupType.GetField("Count").GetValue(group);
            }

            return Mathf.RoundToInt(total);
        }

        private static float GetEffectiveSpawnGroupHealth(object groupList, int index)
        {
            var list = new List<object>();

            foreach (object group in (System.Collections.IEnumerable)groupList)
            {
                list.Add(group);
            }

            Type groupType = list[index].GetType();
            return (float)groupType.GetField("Health").GetValue(list[index]);
        }

        private static float GetEffectiveSpawnGroupCount(object groupList, int index)
        {
            var list = new List<object>();

            foreach (object group in (System.Collections.IEnumerable)groupList)
            {
                list.Add(group);
            }

            Type groupType = list[index].GetType();
            return (float)groupType.GetField("Count").GetValue(list[index]);
        }

        private static int CountEffectiveSpawnGroups(object groupList)
        {
            int count = 0;

            foreach (object _ in (System.Collections.IEnumerable)groupList)
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// 프로젝트에 실제로 존재하는 StageSO 에셋을 이름으로 찾아 로드한다(정확히 하나여야 함).
        /// GitHub 이슈 #33의 Stage_1_40 대조 검사 전용 헬퍼.
        /// </summary>
        private static StageSO LoadRealStageAsset(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:StageSO");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var stage = AssetDatabase.LoadAssetAtPath<StageSO>(path);

                if (stage != null && stage.name == assetName)
                {
                    return stage;
                }
            }

            return null;
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "전술 리더·추종자·대체 추종자의 체력 기대값이 정책대로
        /// 반영됨". 리더 HP=100/추종자 HP=50/대체 추종자 HP=200, AlternateFollowerChance=0.25인
        /// 합성 전술 엔트리(pairCount=3)를 만들어, BuildEffectiveSpawnGroups가 리더 그룹(Count=3,
        /// Health=100), 추종자 그룹(Count=3×0.75=2.25, Health=50), 대체 추종자 그룹(Count=3×0.25=
        /// 0.75, Health=200) 정확히 3개를 만드는지 확인한다 - 실제 RNG 없이 결정적 기대값으로
        /// 쪼개는 정책 그대로.
        /// </summary>
        private static void CheckOfflineStageSimulatorTacticHealthWeightedByChance()
        {
            GameObject leaderPrefab = null;
            GameObject followerPrefab = null;
            GameObject alternatePrefab = null;
            CharacterStatsSO leaderStats = null;
            CharacterStatsSO followerStats = null;
            CharacterStatsSO alternateStats = null;

            try
            {
                leaderPrefab = CreateOfflineTacticPrefab("Leader", 100f, out leaderStats);
                followerPrefab = CreateOfflineTacticPrefab("Follower", 50f, out followerStats);
                alternatePrefab = CreateOfflineTacticPrefab("Alternate", 200f, out alternateStats);

                var entry = new TacticSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "leaderPrefab", leaderPrefab);
                SetPrivateFieldOnPlainObject(entry, "followerPrefab", followerPrefab);
                SetPrivateFieldOnPlainObject(entry, "alternateFollowerPrefab", alternatePrefab);
                SetPrivateFieldOnPlainObject(entry, "alternateFollowerChance", 0.25f);
                SetPrivateFieldOnPlainObject(entry, "totalUnitCount", 6); // pairCount = 3

                StageSO stage = ScriptableObject.CreateInstance<StageSO>();

                try
                {
                    SetPrivateField(stage, "spawnEntries", Array.Empty<MonsterSpawnEntry>());
                    SetPrivateField(stage, "tacticEntries", new[] { entry });

                    MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
                    object groups = buildGroups.Invoke(null, new object[] { stage });

                    if (CountEffectiveSpawnGroups(groups) != 3)
                    {
                        throw new Exception($"그룹 수가 {CountEffectiveSpawnGroups(groups)}(기대=3: 리더/추종자/대체 추종자)");
                    }

                    AssertApprox(100f, GetEffectiveSpawnGroupHealth(groups, 0), "리더 그룹 체력");
                    AssertApprox(3f, GetEffectiveSpawnGroupCount(groups, 0), "리더 그룹 마릿수");

                    AssertApprox(50f, GetEffectiveSpawnGroupHealth(groups, 1), "추종자 그룹 체력");
                    AssertApprox(2.25f, GetEffectiveSpawnGroupCount(groups, 1), "추종자 그룹 마릿수(3 × 0.75)");

                    AssertApprox(200f, GetEffectiveSpawnGroupHealth(groups, 2), "대체 추종자 그룹 체력");
                    AssertApprox(0.75f, GetEffectiveSpawnGroupCount(groups, 2), "대체 추종자 그룹 마릿수(3 × 0.25)");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(stage);
                }
            }
            finally
            {
                if (leaderPrefab != null) UnityEngine.Object.DestroyImmediate(leaderPrefab);
                if (followerPrefab != null) UnityEngine.Object.DestroyImmediate(followerPrefab);
                if (alternatePrefab != null) UnityEngine.Object.DestroyImmediate(alternatePrefab);
                if (leaderStats != null) UnityEngine.Object.DestroyImmediate(leaderStats);
                if (followerStats != null) UnityEngine.Object.DestroyImmediate(followerStats);
                if (alternateStats != null) UnityEngine.Object.DestroyImmediate(alternateStats);
            }
        }

        /// <summary>
        /// 합성 몬스터 프리팹(CharacterStatsProvider만 부착, 비활성 GameObject) 하나를 만든다 -
        /// GitHub 이슈 #33 검사 전용 헬퍼.
        /// </summary>
        private static GameObject CreateOfflineTacticPrefab(string name, float maxHealth, out CharacterStatsSO stats)
        {
            stats = ScriptableObject.CreateInstance<CharacterStatsSO>();
            SetPrivateFloat(stats, "maxHealth", maxHealth);

            var prefab = new GameObject($"RegressionCheck_OfflineTactic_{name}");
            prefab.SetActive(false);
            CharacterStatsProvider provider = prefab.AddComponent<CharacterStatsProvider>();
            SetPrivateField(provider, "baseStats", stats);

            return prefab;
        }

        /// <summary>
        /// GitHub 이슈 #33 - 사용자가 명시적으로 선택한 정책("방패 체력 포함, MaxHealth×2.5")을
        /// 검증한다. ShieldGuard(shieldHealthMultiplier=1.5)가 붙은 리더 프리팹(MaxHealth=100)의
        /// 유효 체력이 100×(1+1.5)=250이어야 한다 - 방패를 무시했다면 100으로 계산돼 방패병을
        /// 실제보다 훨씬 약하게(오프라인 보상 과대 산정) 취급했을 것이다.
        /// </summary>
        private static void CheckOfflineStageSimulatorShieldGuardInflatesEffectiveHealth()
        {
            GameObject prefab = null;
            CharacterStatsSO stats = null;

            try
            {
                prefab = CreateOfflineTacticPrefab("ShieldedLeader", 100f, out stats);
                ShieldGuard shieldGuard = prefab.AddComponent<ShieldGuard>();
                SetPrivateField(shieldGuard, "shieldHealthMultiplier", 1.5f);

                MethodInfo tryGetHealth = typeof(OfflineStageSimulator).GetMethod("TryGetEffectiveHealth", BindingFlags.NonPublic | BindingFlags.Static);
                var args = new object[] { prefab, null };
                bool success = (bool)tryGetHealth.Invoke(null, args);
                float health = (float)args[1];

                if (!success)
                {
                    throw new Exception("ShieldGuard가 붙은 프리팹에서 TryGetEffectiveHealth가 실패로 보고됨");
                }

                AssertApprox(250f, health, "ShieldGuard 리더의 유효 체력(MaxHealth×2.5)");
            }
            finally
            {
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
                if (stats != null) UnityEngine.Object.DestroyImmediate(stats);
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "대체 프리팹 확률 경계". AlternateFollowerPrefab이 null이면
        /// MonsterSpawner.SpawnFormationPair의 null 우선 검사(entry.AlternateFollowerPrefab != null
        /// && Random.value &lt; ...)와 동일하게, AlternateFollowerChance가 0.9처럼 커도 전량
        /// FollowerPrefab으로만 가야 한다(대체 그룹 자체가 생기면 안 됨).
        /// </summary>
        private static void CheckOfflineStageSimulatorNullAlternatePrefabIgnoresChance()
        {
            GameObject leaderPrefab = null;
            GameObject followerPrefab = null;
            CharacterStatsSO leaderStats = null;
            CharacterStatsSO followerStats = null;
            StageSO stage = null;

            try
            {
                leaderPrefab = CreateOfflineTacticPrefab("Leader2", 100f, out leaderStats);
                followerPrefab = CreateOfflineTacticPrefab("Follower2", 50f, out followerStats);

                var entry = new TacticSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "leaderPrefab", leaderPrefab);
                SetPrivateFieldOnPlainObject(entry, "followerPrefab", followerPrefab);
                SetPrivateFieldOnPlainObject(entry, "alternateFollowerPrefab", null);
                SetPrivateFieldOnPlainObject(entry, "alternateFollowerChance", 0.9f); // 무의미해야 함
                SetPrivateFieldOnPlainObject(entry, "totalUnitCount", 4);

                stage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stage, "spawnEntries", Array.Empty<MonsterSpawnEntry>());
                SetPrivateField(stage, "tacticEntries", new[] { entry });

                MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
                object groups = buildGroups.Invoke(null, new object[] { stage });

                if (CountEffectiveSpawnGroups(groups) != 2)
                {
                    throw new Exception($"그룹 수가 {CountEffectiveSpawnGroups(groups)}(기대=2: 리더+추종자, 대체 그룹은 생기면 안 됨)");
                }

                AssertApprox(2f, GetEffectiveSpawnGroupCount(groups, 1), "AlternateFollowerPrefab=null일 때 추종자 그룹 마릿수(전량, 2)");
            }
            finally
            {
                if (leaderPrefab != null) UnityEngine.Object.DestroyImmediate(leaderPrefab);
                if (followerPrefab != null) UnityEngine.Object.DestroyImmediate(followerPrefab);
                if (leaderStats != null) UnityEngine.Object.DestroyImmediate(leaderStats);
                if (followerStats != null) UnityEngine.Object.DestroyImmediate(followerStats);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "null 프리팹... 경계를 검증함". LeaderPrefab이 null인
        /// 손상된 전술 엔트리도 예외 없이 처리되고(콘텐츠 오류를 조용히 건너뜀, 일반 엔트리의
        /// 기존 방어 관례와 동일), 유효한 FollowerPrefab 쪽 그룹은 정상적으로 만들어지는지
        /// 확인한다.
        /// </summary>
        private static void CheckOfflineStageSimulatorNullLeaderPrefabSkipsGroup()
        {
            GameObject followerPrefab = null;
            CharacterStatsSO followerStats = null;
            StageSO stage = null;

            try
            {
                followerPrefab = CreateOfflineTacticPrefab("Follower3", 50f, out followerStats);

                var entry = new TacticSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "leaderPrefab", null);
                SetPrivateFieldOnPlainObject(entry, "followerPrefab", followerPrefab);
                SetPrivateFieldOnPlainObject(entry, "totalUnitCount", 4);

                stage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stage, "spawnEntries", Array.Empty<MonsterSpawnEntry>());
                SetPrivateField(stage, "tacticEntries", new[] { entry });

                MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
                object groups = buildGroups.Invoke(null, new object[] { stage }); // 예외 없이 통과해야 함

                if (CountEffectiveSpawnGroups(groups) != 1)
                {
                    throw new Exception($"그룹 수가 {CountEffectiveSpawnGroups(groups)}(기대=1: 추종자만, 리더는 null이라 제외)");
                }

                AssertApprox(2f, GetEffectiveSpawnGroupCount(groups, 0), "리더 없이도 추종자 그룹은 정상 생성됨");
            }
            finally
            {
                if (followerPrefab != null) UnityEngine.Object.DestroyImmediate(followerPrefab);
                if (followerStats != null) UnityEngine.Object.DestroyImmediate(followerStats);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "페어 간격과 즉시 웨이브 지연이 유효 스폰 시간에 반영됨".
        /// 전술 엔트리 2개(각각 pairCount=3/2, pairSpawnInterval=0.5/1.0)를 만들고 마지막 엔트리의
        /// immediateEntryDelay=2를 설정 - 기대값은 (3×0.5 + 2×1.0) + 2 = (1.5+2) + 2 = 5.5.
        /// 첫 번째 엔트리의 immediateEntryDelay는 합산에서 제외돼야 한다(마지막 엔트리에서만
        /// 의미 있음, MonsterSpawner.FinishTacticEntry와 동일).
        /// </summary>
        private static void CheckOfflineStageSimulatorTacticSpawnDelayFormula()
        {
            var entryA = new TacticSpawnEntry();
            SetPrivateFieldOnPlainObject(entryA, "totalUnitCount", 6); // pairCount = 3
            SetPrivateFieldOnPlainObject(entryA, "pairSpawnInterval", 0.5f);
            SetPrivateFieldOnPlainObject(entryA, "immediateEntryDelay", 999f); // 무시돼야 함(마지막 아님)

            var entryB = new TacticSpawnEntry();
            SetPrivateFieldOnPlainObject(entryB, "totalUnitCount", 4); // pairCount = 2
            SetPrivateFieldOnPlainObject(entryB, "pairSpawnInterval", 1.0f);
            SetPrivateFieldOnPlainObject(entryB, "immediateEntryDelay", 2f);

            StageSO stage = ScriptableObject.CreateInstance<StageSO>();

            try
            {
                SetPrivateField(stage, "spawnEntries", Array.Empty<MonsterSpawnEntry>());
                SetPrivateField(stage, "tacticEntries", new[] { entryA, entryB });

                MethodInfo calculateDelay = typeof(OfflineStageSimulator).GetMethod("CalculateTacticSpawnDelay", BindingFlags.NonPublic | BindingFlags.Static);
                float delay = (float)calculateDelay.Invoke(null, new object[] { stage });

                AssertApprox(5.5f, delay, "전술 스폰 지연 합계((3×0.5 + 2×1.0) + 마지막 엔트리의 immediateEntryDelay=2)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "전술만 있음" 경계 + Simulate()의 population 산술이
        /// 정확한지. 일반 웨이브 없이 전술 엔트리(pairCount=5, 리더/추종자 HP 둘 다 100 -
        /// 가중평균 없이 순수 population 산술만 검증하기 위해 동일 체력으로 통일)만으로 구성된
        /// 스테이지를 totalDps=100, budget=100초로 시뮬레이션한다. population=10, 평균체력=100
        /// → effectiveKillRate=1/초, timeToClear=10초(전술 스폰 지연 0이므로), timesCleared=10,
        /// totalMonstersKilled=100(나머지 없이 딱 떨어짐).
        /// </summary>
        private static void CheckOfflineStageSimulatorTacticOnlyStagePopulation()
        {
            GameObject leaderPrefab = null;
            GameObject followerPrefab = null;
            CharacterStatsSO leaderStats = null;
            CharacterStatsSO followerStats = null;
            StageSO stage = null;

            try
            {
                leaderPrefab = CreateOfflineTacticPrefab("PopLeader", 100f, out leaderStats);
                followerPrefab = CreateOfflineTacticPrefab("PopFollower", 100f, out followerStats);

                var entry = new TacticSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "leaderPrefab", leaderPrefab);
                SetPrivateFieldOnPlainObject(entry, "followerPrefab", followerPrefab);
                SetPrivateFieldOnPlainObject(entry, "totalUnitCount", 10); // pairCount = 5 → 총 10마리
                SetPrivateFieldOnPlainObject(entry, "pairSpawnInterval", 0f);
                SetPrivateFieldOnPlainObject(entry, "immediateEntryDelay", 0f);

                stage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stage, "spawnEntries", Array.Empty<MonsterSpawnEntry>());
                SetPrivateField(stage, "tacticEntries", new[] { entry });

                var simulator = new OfflineStageSimulator(null, null, 1f);
                OfflineStageSimulator.Result result = simulator.Simulate(stage, totalDps: 100f, budget: 100f);

                if (!result.Success)
                {
                    throw new Exception("전술 엔트리만 있는 스테이지인데 시뮬레이션이 실패로 보고됨(GitHub 이슈 #33 재현 - 전술만 있으면 population=0으로 계산됐을 가능성)");
                }

                if (result.TotalMonstersKilled != 100)
                {
                    throw new Exception($"TotalMonstersKilled가 {result.TotalMonstersKilled}(기대=100 = population 10 × timesCleared 10)");
                }

                if (result.TimesCleared != 10)
                {
                    throw new Exception($"TimesCleared가 {result.TimesCleared}(기대=10)");
                }
            }
            finally
            {
                if (leaderPrefab != null) UnityEngine.Object.DestroyImmediate(leaderPrefab);
                if (followerPrefab != null) UnityEngine.Object.DestroyImmediate(followerPrefab);
                if (leaderStats != null) UnityEngine.Object.DestroyImmediate(leaderStats);
                if (followerStats != null) UnityEngine.Object.DestroyImmediate(followerStats);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "전술 유닛 보상 포함/제외 정책이 실전과 일치함" +
        /// "일반+전술 혼합" 경계. 일반 몬스터는 dropChance=0(절대 골드를 안 줌)으로, 전술
        /// 리더/추종자는 dropChance=1(항상 100골드)로 구성한다 - 이렇게 두 소스를 완전히
        /// 분리해두면 TotalGold가 0이냐 아니냐만으로 "전술 유닛의 사망이 실제로 골드 보상에
        /// 기여했는가"를 모호함 없이 판별할 수 있다. (처치 수(TotalMonstersKilled)로 두 시나리오를
        /// 비교하는 방식은 시도해봤으나, 이 시뮬레이터의 정상상태 근사에서는 population 크기와
        /// 무관하게 killRate×budget에 총 처치 수가 수렴해(population과 timeToClear가 같은 비율로
        /// 늘어나 서로 상쇄됨) 우연히 두 시나리오가 같은 값에 도달할 수 있어 신뢰할 수 없는
        /// 비교였다 - 실제로 처음 이 방식으로 작성했을 때 정확히 그 우연의 일치로 실패했다.)
        /// </summary>
        private static void CheckOfflineStageSimulatorMixedStageLootIncludesTactics()
        {
            GameObject normalPrefab = null;
            GameObject leaderPrefab = null;
            GameObject followerPrefab = null;
            CharacterStatsSO normalStats = null;
            CharacterStatsSO leaderStats = null;
            CharacterStatsSO followerStats = null;
            MonsterLootSO normalLoot = null;
            MonsterLootSO leaderLoot = null;
            MonsterLootSO followerLoot = null;
            StageSO normalOnlyStage = null;
            StageSO mixedStage = null;

            try
            {
                normalPrefab = CreateOfflineTacticPrefab("MixedNormal", 100f, out normalStats);
                normalLoot = CreateGuaranteedGoldLoot(100, dropChance: 0f); // 절대 드롭하지 않음
                normalPrefab.AddComponent<MonsterLootProvider>();
                SetPrivateField(normalPrefab.GetComponent<MonsterLootProvider>(), "loot", normalLoot);

                var normalEntry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(normalEntry, "monsterPrefab", normalPrefab);
                SetPrivateFieldOnPlainObject(normalEntry, "count", 5);

                normalOnlyStage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(normalOnlyStage, "spawnEntries", new[] { normalEntry });
                SetPrivateField(normalOnlyStage, "tacticEntries", Array.Empty<TacticSpawnEntry>());

                leaderPrefab = CreateOfflineTacticPrefab("MixedLeader", 100f, out leaderStats);
                leaderLoot = CreateGuaranteedGoldLoot(100, dropChance: 1f);
                leaderPrefab.AddComponent<MonsterLootProvider>();
                SetPrivateField(leaderPrefab.GetComponent<MonsterLootProvider>(), "loot", leaderLoot);

                followerPrefab = CreateOfflineTacticPrefab("MixedFollower", 100f, out followerStats);
                followerLoot = CreateGuaranteedGoldLoot(100, dropChance: 1f);
                followerPrefab.AddComponent<MonsterLootProvider>();
                SetPrivateField(followerPrefab.GetComponent<MonsterLootProvider>(), "loot", followerLoot);

                var tacticEntry = new TacticSpawnEntry();
                SetPrivateFieldOnPlainObject(tacticEntry, "leaderPrefab", leaderPrefab);
                SetPrivateFieldOnPlainObject(tacticEntry, "followerPrefab", followerPrefab);
                SetPrivateFieldOnPlainObject(tacticEntry, "totalUnitCount", 10); // pairCount = 5 → 10마리 추가

                mixedStage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(mixedStage, "spawnEntries", new[] { normalEntry });
                SetPrivateField(mixedStage, "tacticEntries", new[] { tacticEntry });

                var simulator = new OfflineStageSimulator(null, null, 1f);
                OfflineStageSimulator.Result normalOnlyResult = simulator.Simulate(normalOnlyStage, totalDps: 1000f, budget: 1000f);
                OfflineStageSimulator.Result mixedResult = simulator.Simulate(mixedStage, totalDps: 1000f, budget: 1000f);

                if (!normalOnlyResult.Success || !mixedResult.Success)
                {
                    throw new Exception($"시뮬레이션 실패(normalOnly={normalOnlyResult.Success}, mixed={mixedResult.Success})");
                }

                if (normalOnlyResult.TotalGold != BigNumber.Zero)
                {
                    throw new Exception($"일반 몬스터의 dropChance=0인데도 골드가 {normalOnlyResult.TotalGold} 나옴(테스트 전제 자체가 깨짐)");
                }

                if (mixedResult.TotalGold == BigNumber.Zero)
                {
                    throw new Exception("전술 웨이브(dropChance=1인 리더/추종자 포함)를 추가했는데도 골드가 0으로 나옴 - 전술 유닛이 보상 계산에서 제외되고 있을 가능성(GitHub 이슈 #33 완료 조건: 전술 유닛 보상 포함 정책)");
                }
            }
            finally
            {
                if (normalPrefab != null) UnityEngine.Object.DestroyImmediate(normalPrefab);
                if (leaderPrefab != null) UnityEngine.Object.DestroyImmediate(leaderPrefab);
                if (followerPrefab != null) UnityEngine.Object.DestroyImmediate(followerPrefab);
                if (normalStats != null) UnityEngine.Object.DestroyImmediate(normalStats);
                if (leaderStats != null) UnityEngine.Object.DestroyImmediate(leaderStats);
                if (followerStats != null) UnityEngine.Object.DestroyImmediate(followerStats);
                if (normalLoot != null) UnityEngine.Object.DestroyImmediate(normalLoot);
                if (leaderLoot != null) UnityEngine.Object.DestroyImmediate(leaderLoot);
                if (followerLoot != null) UnityEngine.Object.DestroyImmediate(followerLoot);
                if (normalOnlyStage != null) UnityEngine.Object.DestroyImmediate(normalOnlyStage);
                if (mixedStage != null) UnityEngine.Object.DestroyImmediate(mixedStage);
            }
        }

        /// <summary>
        /// dropChance 확률로 minGold=maxGold=amount를 굴리는 MonsterLootSO를 만든다(GitHub 이슈
        /// #33 검사 전용 헬퍼).
        /// </summary>
        private static MonsterLootSO CreateGuaranteedGoldLoot(int amount, float dropChance = 1f)
        {
            var loot = ScriptableObject.CreateInstance<MonsterLootSO>();
            SetPrivateFieldOnPlainObject(loot, "minGold", amount);
            SetPrivateFieldOnPlainObject(loot, "maxGold", amount);
            SetPrivateFieldOnPlainObject(loot, "dropChance", dropChance);
            return loot;
        }

        /// <summary>
        /// GitHub 이슈 #33 완료 조건 - "전술 없음" 경계. TacticEntries가 null인 스테이지와 빈
        /// 배열([])인 스테이지가 완전히 동일한 Simulate 결과를 내는지 확인한다 - 수정 전부터
        /// 있던 일반 웨이브 전용 경로가 이번 리팩터링으로 조금이라도 달라지지 않았는지 확인하는
        /// 회귀 방지 검사(GitHub 이슈 #27이 이미 검증한 "일반 웨이브 결과는 SpawnInterval과
        /// 무관함"과는 별개로, 이번엔 TacticEntries null/empty 자체를 대조한다).
        /// </summary>
        private static void CheckOfflineStageSimulatorNoTacticsNullVsEmptyBehaveIdentically()
        {
            GameObject prefab = null;
            CharacterStatsSO stats = null;
            MonsterLootSO loot = null;
            StageSO stageNullTactics = null;
            StageSO stageEmptyTactics = null;

            try
            {
                prefab = CreateOfflineTacticPrefab("NoTactics", 100f, out stats);
                loot = CreateGuaranteedGoldLoot(50);
                prefab.AddComponent<MonsterLootProvider>();
                SetPrivateField(prefab.GetComponent<MonsterLootProvider>(), "loot", loot);

                var normalEntry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(normalEntry, "monsterPrefab", prefab);
                SetPrivateFieldOnPlainObject(normalEntry, "count", 7);

                stageNullTactics = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stageNullTactics, "spawnEntries", new[] { normalEntry });
                // tacticEntries는 건드리지 않아 기본값(null) 그대로.

                stageEmptyTactics = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stageEmptyTactics, "spawnEntries", new[] { normalEntry });
                SetPrivateField(stageEmptyTactics, "tacticEntries", Array.Empty<TacticSpawnEntry>());

                var simulator = new OfflineStageSimulator(null, null, 1f);
                OfflineStageSimulator.Result nullResult = simulator.Simulate(stageNullTactics, totalDps: 50f, budget: 500f);
                OfflineStageSimulator.Result emptyResult = simulator.Simulate(stageEmptyTactics, totalDps: 50f, budget: 500f);

                if (nullResult.Success != emptyResult.Success
                    || nullResult.TotalMonstersKilled != emptyResult.TotalMonstersKilled
                    || nullResult.TimesCleared != emptyResult.TimesCleared)
                {
                    throw new Exception($"TacticEntries=null과 빈 배열의 결과가 다름(null: success={nullResult.Success},killed={nullResult.TotalMonstersKilled},cleared={nullResult.TimesCleared} / empty: success={emptyResult.Success},killed={emptyResult.TotalMonstersKilled},cleared={emptyResult.TimesCleared})");
                }
            }
            finally
            {
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
                if (stats != null) UnityEngine.Object.DestroyImmediate(stats);
                if (loot != null) UnityEngine.Object.DestroyImmediate(loot);
                if (stageNullTactics != null) UnityEngine.Object.DestroyImmediate(stageNullTactics);
                if (stageEmptyTactics != null) UnityEngine.Object.DestroyImmediate(stageEmptyTactics);
            }
        }

        /// <summary>
        /// 정해진 count 배열대로 서로 다른 프리팹(공유 baseStats, MaxHealth=100)을 만들어
        /// MonsterSpawnEntry로 감싼 StageSO를 하나 구성하고, OfflineStageSimulator.
        /// BuildEffectiveSpawnGroups(private static)를 리플렉션으로 호출해 그 결과(object,
        /// 실제로는 List&lt;EffectiveSpawnGroup&gt;)를 돌려준다 - GitHub 이슈 #34 검사 전용 헬퍼.
        /// EffectiveSpawnGroup 자체가 private nested struct라 이 스테이지를 거쳐 간접적으로만
        /// 만들 수 있다. 생성된 UnityEngine.Object들은 toDestroy에 추가되므로 호출자가 정리한다.
        /// </summary>
        private static object BuildOfflineGroupsForCounts(int[] counts, List<UnityEngine.Object> toDestroy)
        {
            var entries = new MonsterSpawnEntry[counts.Length];

            for (int i = 0; i < counts.Length; i++)
            {
                GameObject prefab = CreateOfflineTacticPrefab($"AllocTest{i}", 100f, out CharacterStatsSO stats);
                toDestroy.Add(prefab);
                toDestroy.Add(stats);

                var entry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "monsterPrefab", prefab);
                SetPrivateFieldOnPlainObject(entry, "count", counts[i]);
                entries[i] = entry;
            }

            StageSO stage = ScriptableObject.CreateInstance<StageSO>();
            toDestroy.Add(stage);
            SetPrivateField(stage, "spawnEntries", entries);
            SetPrivateField(stage, "tacticEntries", Array.Empty<TacticSpawnEntry>());

            MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
            return buildGroups.Invoke(null, new object[] { stage });
        }

        /// <summary>
        /// OfflineStageSimulator.AllocateByLargestRemainder(private static)를 리플렉션으로 호출한다
        /// (GitHub 이슈 #34 검사 전용 헬퍼).
        /// </summary>
        private static int[] InvokeAllocateByLargestRemainder(object groups, float totalCount, int total)
        {
            MethodInfo method = typeof(OfflineStageSimulator).GetMethod("AllocateByLargestRemainder", BindingFlags.NonPublic | BindingFlags.Static);
            return (int[])method.Invoke(null, new object[] { groups, totalCount, total });
        }

        /// <summary>
        /// GitHub 이슈 #34 재현 절차 그대로 - 실제 Stage 1-10의 엔트리 마릿수 구성([15,7,8,1],
        /// 총 31마리)으로 groups를 만들고, 처치 수 0~65(이슈가 실측한 구체적 불일치 지점 1,5,9,
        /// 11,13,15,16,18,20을 전부 포함하고, 여러 회 클리어+나머지 경계까지 넉넉히 덮도록)에 대해
        /// 배분 합이 항상 정확히 처치 수와 같은지 확인한다. 수정 전에는 이슈가 실측한 그대로
        /// killed=1→배분 0, killed=20→배분 21처럼 어긋났을 것이다.
        /// </summary>
        private static void CheckAllocateByLargestRemainderSumMatchesIssueRepro()
        {
            var toDestroy = new List<UnityEngine.Object>();

            try
            {
                object groups = BuildOfflineGroupsForCounts(new[] { 15, 7, 8, 1 }, toDestroy);

                for (int killed = 0; killed <= 65; killed++)
                {
                    int[] allocations = InvokeAllocateByLargestRemainder(groups, 31f, killed);
                    int sum = 0;

                    foreach (int allocation in allocations)
                    {
                        sum += allocation;
                    }

                    if (sum != killed)
                    {
                        throw new Exception($"killed={killed}일 때 배분 합이 {sum}(기대={killed}) - GitHub 이슈 #34 재현(이슈 실측 불일치 지점: 1→0, 5→4, 9→8, 11→10, 13→12, 15→14, 16→17, 18→19, 20→21)");
                    }
                }
            }
            finally
            {
                foreach (UnityEngine.Object obj in toDestroy)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #34 완료 조건 - "엔트리 1개/다수... 검증". 그룹이 하나뿐이면 나눌 대상이
        /// 없으므로 항상 total 전체가 그 그룹에 그대로 배분돼야 한다(모든 처치 수에서).
        /// </summary>
        private static void CheckAllocateByLargestRemainderSingleGroup()
        {
            var toDestroy = new List<UnityEngine.Object>();

            try
            {
                object groups = BuildOfflineGroupsForCounts(new[] { 5 }, toDestroy);

                for (int killed = 0; killed <= 20; killed++)
                {
                    int[] allocations = InvokeAllocateByLargestRemainder(groups, 5f, killed);

                    if (allocations.Length != 1 || allocations[0] != killed)
                    {
                        throw new Exception($"단일 그룹인데 killed={killed}일 때 배분이 {(allocations.Length > 0 ? allocations[0].ToString() : "그룹 없음")}(기대={killed})");
                    }
                }
            }
            finally
            {
                foreach (UnityEngine.Object obj in toDestroy)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #34 완료 조건 - "극단적 비율... 검증". Count=[1, 999](totalCount=1000)에서
        /// total=3을 배분하면 정확한 몫은 [0.003, 2.997] - floor는 [0,2], 나머지는 [0.003,0.997].
        /// 남은 1개는 나머지가 훨씬 큰 두 번째 그룹에 가야 하므로 극단적으로 작은 첫 그룹은 0을
        /// 유지해야 한다(0을 받으면 안 되는데 받는 것도, 반대로 큰 그룹이 부당하게 못 받는 것도
        /// 아닌지 확인).
        /// </summary>
        private static void CheckAllocateByLargestRemainderExtremeRatio()
        {
            var toDestroy = new List<UnityEngine.Object>();

            try
            {
                object groups = BuildOfflineGroupsForCounts(new[] { 1, 999 }, toDestroy);
                int[] allocations = InvokeAllocateByLargestRemainder(groups, 1000f, 3);

                if (allocations[0] != 0)
                {
                    throw new Exception($"극단적으로 작은 비율(1/1000) 그룹이 total=3에서 {allocations[0]}을 받음(기대=0)");
                }

                if (allocations[1] != 3)
                {
                    throw new Exception($"압도적으로 큰 비율(999/1000) 그룹이 total=3에서 {allocations[1]}을 받음(기대=3)");
                }
            }
            finally
            {
                foreach (UnityEngine.Object obj in toDestroy)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #34 완료 조건 - "Count=0... 검증". Count=0인 그룹은 exactShare/floor/나머지가
        /// 항상 정확히 0이라(0을 아무리 곱하고 나눠도 부동소수점 오차 없이 정확히 0), 다른 그룹에
        /// 양의 나머지가 하나라도 남아있는 한 최대 나머지법의 순위 경쟁에서 절대 선택될 수 없다
        /// (수학적으로 보장됨 - remaining보다 나머지가 큰 그룹이 항상 remaining개 이상 존재).
        /// </summary>
        private static void CheckAllocateByLargestRemainderZeroCountGroup()
        {
            var toDestroy = new List<UnityEngine.Object>();

            try
            {
                object groups = BuildOfflineGroupsForCounts(new[] { 0, 5, 0, 3 }, toDestroy);

                for (int killed = 0; killed <= 8; killed++)
                {
                    int[] allocations = InvokeAllocateByLargestRemainder(groups, 8f, killed);

                    if (allocations[0] != 0 || allocations[2] != 0)
                    {
                        throw new Exception($"killed={killed}일 때 Count=0 그룹이 배분을 받음(인덱스0={allocations[0]}, 인덱스2={allocations[2]}, 기대=둘 다 0)");
                    }

                    int sum = allocations[0] + allocations[1] + allocations[2] + allocations[3];

                    if (sum != killed)
                    {
                        throw new Exception($"killed={killed}일 때 배분 합이 {sum}(기대={killed})");
                    }
                }
            }
            finally
            {
                foreach (UnityEngine.Object obj in toDestroy)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #34 완료 조건 - "null/무보상 프리팹 정책 검증" + "골드와 장비가 동일한
        /// 보상 대상 처치 배분을 공유함". 세 그룹(A: 골드 로트 보유, B: MonsterLootProvider 자체가
        /// 없음 - "보상 불가" 프리팹, C: 골드 로트 보유) 중 B의 배분 몫은 최대 나머지법의 공정한
        /// 배분에는 동일하게 참여하지만(다른 그룹의 배분을 왜곡하지 않음) 실제 골드/장비 굴림에서는
        /// 완전히 제외되는지 확인한다 - A+C의 배분 합만큼만 정확히 골드(둘 다 10골드 100% 확률)와
        /// 장비(스테이지 공통 테이블 100% 확률)가 나와야 한다.
        /// </summary>
        private static void CheckOfflineStageSimulatorRollLootExcludesNoLootGroupButPreservesOthersSum()
        {
            GameObject lootPrefabA = null;
            GameObject noLootPrefabB = null;
            GameObject lootPrefabC = null;
            CharacterStatsSO statsA = null;
            CharacterStatsSO statsB = null;
            CharacterStatsSO statsC = null;
            MonsterLootSO lootA = null;
            MonsterLootSO lootC = null;
            EquipmentSO equipment = null;
            StageSO stage = null;

            try
            {
                lootPrefabA = CreateOfflineTacticPrefab("RollLootA", 100f, out statsA);
                lootA = CreateGuaranteedGoldLoot(10, dropChance: 1f);
                lootPrefabA.AddComponent<MonsterLootProvider>();
                SetPrivateField(lootPrefabA.GetComponent<MonsterLootProvider>(), "loot", lootA);

                noLootPrefabB = CreateOfflineTacticPrefab("RollLootB_NoLoot", 100f, out statsB);
                // MonsterLootProvider를 의도적으로 부착하지 않음 - "보상 불가" 프리팹 시나리오.

                lootPrefabC = CreateOfflineTacticPrefab("RollLootC", 100f, out statsC);
                lootC = CreateGuaranteedGoldLoot(10, dropChance: 1f);
                lootPrefabC.AddComponent<MonsterLootProvider>();
                SetPrivateField(lootPrefabC.GetComponent<MonsterLootProvider>(), "loot", lootC);

                equipment = ScriptableObject.CreateInstance<EquipmentSO>();
                var dropEntry = new EquipmentDropEntry();
                SetPrivateFieldOnPlainObject(dropEntry, "equipment", equipment);
                SetPrivateFieldOnPlainObject(dropEntry, "dropChance", 1f);

                var entryA = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entryA, "monsterPrefab", lootPrefabA);
                SetPrivateFieldOnPlainObject(entryA, "count", 10);

                var entryB = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entryB, "monsterPrefab", noLootPrefabB);
                SetPrivateFieldOnPlainObject(entryB, "count", 10);

                var entryC = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entryC, "monsterPrefab", lootPrefabC);
                SetPrivateFieldOnPlainObject(entryC, "count", 10);

                stage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stage, "spawnEntries", new[] { entryA, entryB, entryC });
                SetPrivateField(stage, "tacticEntries", Array.Empty<TacticSpawnEntry>());
                SetPrivateField(stage, "equipmentDrops", new[] { dropEntry });

                MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
                object groups = buildGroups.Invoke(null, new object[] { stage });

                const int monstersKilled = 17; // totalCount=30, 나머지가 실제로 발생하는 임의 값
                int[] allocations = InvokeAllocateByLargestRemainder(groups, 30f, monstersKilled);
                int expectedLootableSum = allocations[0] + allocations[2]; // A + C (B는 배분은 받되 굴리기에서 제외)

                MethodInfo rollLootMethod = typeof(OfflineStageSimulator).GetMethod("RollLoot", BindingFlags.NonPublic | BindingFlags.Static);
                var equipmentEarned = new List<EquipmentSO>();
                var args = new object[] { stage, groups, 30, monstersKilled, 1f, BigNumber.Zero, equipmentEarned };
                rollLootMethod.Invoke(null, args);

                var totalGold = (BigNumber)args[5];

                if (equipmentEarned.Count != expectedLootableSum)
                {
                    throw new Exception($"장비 획득 개수가 {equipmentEarned.Count}(기대={expectedLootableSum} = A+C 배분 합, B는 제외) - 보상 불가 그룹이 굴리기에서 제대로 제외되지 않거나 다른 그룹의 배분을 왜곡했을 가능성");
                }

                BigNumber expectedGold = expectedLootableSum * 10;

                if (totalGold != expectedGold)
                {
                    throw new Exception($"골드 총액이 {totalGold}(기대={expectedGold} = (A+C 배분 합) × 10골드)");
                }
            }
            finally
            {
                if (lootPrefabA != null) UnityEngine.Object.DestroyImmediate(lootPrefabA);
                if (noLootPrefabB != null) UnityEngine.Object.DestroyImmediate(noLootPrefabB);
                if (lootPrefabC != null) UnityEngine.Object.DestroyImmediate(lootPrefabC);
                if (statsA != null) UnityEngine.Object.DestroyImmediate(statsA);
                if (statsB != null) UnityEngine.Object.DestroyImmediate(statsB);
                if (statsC != null) UnityEngine.Object.DestroyImmediate(statsC);
                if (lootA != null) UnityEngine.Object.DestroyImmediate(lootA);
                if (lootC != null) UnityEngine.Object.DestroyImmediate(lootC);
                if (equipment != null) UnityEngine.Object.DestroyImmediate(equipment);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
            }
        }

        /// <summary>
        /// GitHub 이슈 #34 완료 조건 - "엔트리 순서/분할만 바꿔도 전체 기대 보상이 비정상적으로
        /// 변하지 않음". 같은 프리팹을 가리키는 엔트리 하나(Count=20)와, 같은 프리팹을 가리키는
        /// 엔트리 둘(Count=10+10)로 쪼갠 경우를 비교한다. 최대 나머지법(Hamilton's method)은
        /// 이론적으로 완전한 분할 불변성을 보장하지는 않지만(apportionment 이론에서 이미 알려진
        /// 한계 - 각 그룹의 배분이 정확한 몫에서 최대 1 미만만 벗어난다는 것만 보장됨), 분할해도
        /// 그 편차는 항상 작은 범위(여기서는 넉넉히 2 이하) 안에 머물러야 한다 - "비정상적으로"
        /// 크게 벌어지지 않는지가 이 조건의 핵심이다(완전히 동일해야 한다는 조건이 아님).
        /// </summary>
        private static void CheckOfflineStageSimulatorRollLootSplitEntryBoundedDifference()
        {
            var toDestroyCombined = new List<UnityEngine.Object>();
            var toDestroySplit = new List<UnityEngine.Object>();

            try
            {
                GameObject sharedPrefab = CreateOfflineTacticPrefab("SplitShared", 100f, out CharacterStatsSO sharedStats);
                toDestroyCombined.Add(sharedPrefab);
                toDestroyCombined.Add(sharedStats);

                var combinedEntry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(combinedEntry, "monsterPrefab", sharedPrefab);
                SetPrivateFieldOnPlainObject(combinedEntry, "count", 20);

                StageSO combinedStage = ScriptableObject.CreateInstance<StageSO>();
                toDestroyCombined.Add(combinedStage);
                SetPrivateField(combinedStage, "spawnEntries", new[] { combinedEntry });
                SetPrivateField(combinedStage, "tacticEntries", Array.Empty<TacticSpawnEntry>());

                var splitEntryA = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(splitEntryA, "monsterPrefab", sharedPrefab);
                SetPrivateFieldOnPlainObject(splitEntryA, "count", 10);

                var splitEntryB = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(splitEntryB, "monsterPrefab", sharedPrefab);
                SetPrivateFieldOnPlainObject(splitEntryB, "count", 10);

                StageSO splitStage = ScriptableObject.CreateInstance<StageSO>();
                toDestroySplit.Add(splitStage);
                SetPrivateField(splitStage, "spawnEntries", new[] { splitEntryA, splitEntryB });
                SetPrivateField(splitStage, "tacticEntries", Array.Empty<TacticSpawnEntry>());

                MethodInfo buildGroups = typeof(OfflineStageSimulator).GetMethod("BuildEffectiveSpawnGroups", BindingFlags.NonPublic | BindingFlags.Static);
                object combinedGroups = buildGroups.Invoke(null, new object[] { combinedStage });
                object splitGroups = buildGroups.Invoke(null, new object[] { splitStage });

                for (int killed = 0; killed <= 20; killed++)
                {
                    int[] combinedAllocations = InvokeAllocateByLargestRemainder(combinedGroups, 20f, killed);
                    int[] splitAllocations = InvokeAllocateByLargestRemainder(splitGroups, 20f, killed);

                    int combinedTotal = combinedAllocations[0];
                    int splitTotal = splitAllocations[0] + splitAllocations[1];
                    int difference = Math.Abs(combinedTotal - splitTotal);

                    if (difference > 2)
                    {
                        throw new Exception($"killed={killed}일 때 분할 전(combined={combinedTotal})과 분할 후(split={splitTotal})의 차이가 {difference}(기대: 2 이하) - 엔트리를 쪼개는 것만으로 보상이 비정상적으로 변함");
                    }

                    if (splitTotal != killed)
                    {
                        throw new Exception($"분할된 두 엔트리(같은 프리팹)의 배분 합이 {splitTotal}(기대={killed}) - 전체 합계 보존 자체가 깨짐");
                    }
                }
            }
            finally
            {
                foreach (UnityEngine.Object obj in toDestroyCombined)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }

                foreach (UnityEngine.Object obj in toDestroySplit)
                {
                    if (obj != null)
                    {
                        UnityEngine.Object.DestroyImmediate(obj);
                    }
                }
            }
        }

        /// <summary>
        /// 같은 인스턴스를 서로 다른 부대(SquadIndexOf 결과가 다른 슬롯 인덱스)로 재등록하면,
        /// 이전 부대의 멤버 목록에서 실제로 제거되는지 확인한다 - 재등록 전에 이전 목록을 정리하지
        /// 않으면 한 GameObject가 두 부대에 동시에 소속된 것처럼 집계돼(_members는 새 Member로
        /// 덮어써지지만 이전 Member 객체가 이전 부대 리스트에 그대로 남음), RecomputeSquad가
        /// 이전 부대의 이동속도/교전 집계에 유령 멤버를 영구히 포함시킨다. 배치 변경/재소환으로
        /// 같은 풀 인스턴스가 다른 부대 슬롯에 재등록되는 실제 시나리오(전투 지속 중 배치 UI를
        /// 빠르게 조작하는 경우 등)를 그대로 재현한다. Soldier.SoldierDeploymentService.
        /// SlotsPerSquad를 그대로 써서 서로 다른 부대에 속하는 두 슬롯 인덱스를 고른다.
        /// </summary>
        private static void CheckSquadMovementSyncServiceReRegistrationInvariant()
        {
            var events = new EventBus();
            var service = new SquadMovementSyncService(events);
            service.Initialize();

            GameObject instance = null;
            CharacterStatsSO baseStats = null;

            try
            {
                baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(baseStats, "moveSpeed", 2f);

                instance = new GameObject("RegressionCheck_SquadMember");
                var provider = instance.AddComponent<CharacterStatsProvider>();
                SetPrivateField(provider, "baseStats", baseStats);

                const int firstSquadSlot = 0;
                const int secondSquadSlot = SoldierDeploymentService.SlotsPerSquad;

                service.Register(instance, firstSquadSlot, false);

                if (service.GetSquadMembers(0).Count != 1)
                {
                    throw new Exception($"최초 등록 직후 부대 0의 인원이 {service.GetSquadMembers(0).Count}(기대=1)");
                }

                service.Register(instance, secondSquadSlot, false); // 다른 부대로 재등록

                if (service.GetSquadMembers(0).Count != 0)
                {
                    throw new Exception($"재등록 후에도 이전 부대 0에 유령 멤버가 남음(인원={service.GetSquadMembers(0).Count}, 기대=0)");
                }

                if (service.GetSquadMembers(1).Count != 1)
                {
                    throw new Exception($"재등록 후 새 부대 1의 인원이 {service.GetSquadMembers(1).Count}(기대=1)");
                }

                if (!service.TryGetSlotIndex(instance, out int slotIndex) || slotIndex != secondSquadSlot)
                {
                    throw new Exception("재등록 후 슬롯 인덱스가 새 값으로 갱신되지 않음");
                }
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                if (baseStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(baseStats);
                }

                service.Shutdown();
            }
        }

        /// <summary>
        /// GitHub 이슈 #40의 나머지 구멍 - 위 재등록 검사(Register가 같은 인스턴스로 다시 호출되는
        /// 경로)와 달리, 이후로 다시 Register가 절대 호출되지 않는 경로(순수 배치 해제)를 재현한다.
        /// Unregister 한 번으로 즉시 부대 목록에서 빠지고, 남은 부대원의 이동속도 클램프가 그
        /// 순간 재계산되는지(제거된 멤버가 최저속이었을 경우 남은 멤버가 더 빠른 값으로 즉시
        /// 갱신되는지) 확인한다. GameBootstrapper.Services의 실제 SoldierEnhancementService(이
        /// 개발 세션의 실제 강화 레벨)에 값이 좌우되지 않도록, 절대 수치가 아니라 "제거 전후
        /// 상대 비교"(클램프됐던 값 → 제거 후 더 커짐)로만 검증한다.
        /// </summary>
        private static void CheckSquadMovementSyncServiceUnregisterInvariant()
        {
            var events = new EventBus();
            var service = new SquadMovementSyncService(events);
            service.Initialize();

            GameObject slow = null;
            GameObject fast = null;
            CharacterStatsSO slowStats = null;
            CharacterStatsSO fastStats = null;

            try
            {
                slowStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(slowStats, "moveSpeed", 1f);
                fastStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(fastStats, "moveSpeed", 3f);

                slow = new GameObject("RegressionCheck_SquadMember_Slow");
                var slowProvider = slow.AddComponent<CharacterStatsProvider>();
                SetPrivateField(slowProvider, "baseStats", slowStats);

                fast = new GameObject("RegressionCheck_SquadMember_Fast");
                var fastProvider = fast.AddComponent<CharacterStatsProvider>();
                SetPrivateField(fastProvider, "baseStats", fastStats);

                const int squadSlotA = 0;
                const int squadSlotB = 1;

                service.Register(slow, squadSlotA, false);
                service.Register(fast, squadSlotB, false);

                float clampedTogether = fastProvider.Stats.MoveSpeed;

                if (!Mathf.Approximately(clampedTogether, slowProvider.Stats.MoveSpeed))
                {
                    throw new Exception($"등록 직후 fast({clampedTogether})가 부대 최저속(slow={slowProvider.Stats.MoveSpeed})으로 클램프되지 않음");
                }

                service.Unregister(slow);

                if (service.GetSquadMembers(0).Count != 1 || service.GetSquadMembers(0)[0] != fast)
                {
                    throw new Exception($"Unregister 이후 부대 인원 구성이 예상과 다름(인원={service.GetSquadMembers(0).Count})");
                }

                if (service.TryGetSlotIndex(slow, out _))
                {
                    throw new Exception("Unregister 이후에도 인스턴스가 여전히 등록된 것으로 조회됨");
                }

                float afterRemoval = fastProvider.Stats.MoveSpeed;

                if (!(afterRemoval > clampedTogether))
                {
                    throw new Exception($"Unregister 이후 fast의 속도({afterRemoval})가 이전 클램프 속도({clampedTogether})보다 커지지 않음 - RecomputeSquad가 즉시 안 불렸을 가능성");
                }

                // 멱등/방어: 이미 제거된 인스턴스, null 인스턴스 모두 예외 없이 조용히 무시.
                service.Unregister(slow);
                service.Unregister(null);
            }
            finally
            {
                if (slow != null)
                {
                    UnityEngine.Object.DestroyImmediate(slow);
                }

                if (fast != null)
                {
                    UnityEngine.Object.DestroyImmediate(fast);
                }

                if (slowStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(slowStats);
                }

                if (fastStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(fastStats);
                }

                service.Shutdown();
            }
        }

        /// <summary>
        /// GitHub 이슈 #40의 실제 회귀 지점 - Soldier.SoldierRespawner.ReleaseSlot(배치 해제/재편성으로
        /// 살아있는 인스턴스를 CharacterDiedEvent 없이 풀로 반환하는 경로)이 실제로
        /// Soldier.SquadMovementSyncService.Unregister를 호출해, 재등록 없이도 이전 부대 목록에서
        /// 빠지는지 확인한다. 수정 전에는 이 경로가 SquadMovementSyncService를 전혀 몰라, 배치
        /// 해제된 인스턴스가 비활성화된 채 풀에 있으면서도 이전 부대에 영원히 유령으로 남았다
        /// (그 뒤로 같은 인스턴스가 다시 Register되는 일이 없어 위 재등록 경로로도 해소되지 않음).
        /// GameBootstrapper.Services를 이 검사만의 격리된 ServiceLocator(실제 SquadMovementSyncService
        /// 하나만 등록)로 임시 교체해, ReleaseSlot이 실전과 동일하게 TryGet으로 서비스를 찾는
        /// 경로 그대로 검증한다(WithNullServices와 같은 안전한 백업/복원 패턴).
        /// </summary>
        private static void CheckSoldierRespawnerReleaseSlotUnregistersFromSquadSync()
        {
            var events = new EventBus();
            var squadSync = new SquadMovementSyncService(events);
            squadSync.Initialize();

            var pool = new Managers.PoolManager();
            pool.Initialize();

            PropertyInfo servicesProperty = typeof(GameBootstrapper).GetProperty("Services", BindingFlags.Public | BindingFlags.Static);

            if (servicesProperty == null)
            {
                throw new Exception("GameBootstrapper.Services 프로퍼티를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            object originalServices = servicesProperty.GetValue(null);
            GameObject prefab = null;
            SoldierRespawner respawner = null;
            CharacterStatsSO baseStats = null;

            try
            {
                var locator = new ServiceLocator();
                locator.Register(squadSync);
                servicesProperty.SetValue(null, locator);

                baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(baseStats, "moveSpeed", 2f);

                prefab = new GameObject("RegressionCheck_ReleaseSlot_Prefab");
                prefab.SetActive(false);
                CharacterStatsProvider provider = prefab.AddComponent<CharacterStatsProvider>();
                SetPrivateField(provider, "baseStats", baseStats);
                pool.EnsurePool(prefab, 1, 4);

                GameObject instance = pool.Get(prefab, Vector3.zero, Quaternion.identity);

                const int slotIndex = SoldierDeploymentService.SlotsPerSquad; // 부대 1

                squadSync.Register(instance, slotIndex, false);

                if (squadSync.GetSquadMembers(1).Count != 1)
                {
                    throw new Exception($"등록 직후 부대 1의 인원이 {squadSync.GetSquadMembers(1).Count}(기대=1)");
                }

                respawner = new SoldierRespawner(events, pool, null, null, null);
                var slot = new SoldierSpawnSlot();
                SetPrivateFieldOnPlainObject(slot, "slotIndex", slotIndex);
                respawner.RegisterSpawned(instance, slot);

                respawner.ReleaseSlot(slotIndex);

                if (squadSync.GetSquadMembers(1).Count != 0)
                {
                    throw new Exception($"ReleaseSlot 이후에도 부대 1에 유령 멤버가 남음(인원={squadSync.GetSquadMembers(1).Count}, 기대=0) - GitHub 이슈 #40 재현");
                }

                if (squadSync.TryGetSlotIndex(instance, out _))
                {
                    throw new Exception("ReleaseSlot 이후에도 인스턴스가 SquadMovementSyncService에 여전히 등록된 것으로 조회됨");
                }
            }
            finally
            {
                servicesProperty.SetValue(null, originalServices);
                respawner?.Dispose();
                squadSync.Shutdown();

                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                if (baseStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(baseStats);
                }

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #41 - SquadRaidCoordinator의 습격 대기 타이머가 병사 동행 금지 던전 안에서도
        /// 계속 흘러, 던전 안인데도 카운트다운이 만료돼 부대가 다시 등장(ExecuteRaid)해버리던
        /// 문제. SetDungeonHidden(true)로 숨긴 동안은 Tick 자체가 완전히 멈춰(카운트다운 소비/
        /// 실행 없음) 있다가, SetDungeonHidden(false)로 되돌리면 숨기기 직전 남아있던 시간만큼만
        /// 더 흘러야 실행되는지(=일시정지이지 리셋도 즉시발동도 아님) 확인한다.
        /// GameBootstrapper.Services를 이 검사만의 격리된 ServiceLocator(실제
        /// SquadTacticService/SquadMovementSyncService)로 임시 교체해 SquadRaidCoordinator.Awake/
        /// OnEnable이 실전과 동일한 경로로 두 서비스를 조회하게 한다(WithNullServices와 같은
        /// 안전한 백업/복원 패턴). GameTicker는 등록하지 않고 ITickable.Tick을 직접 호출해
        /// 타이밍을 결정론적으로 통제한다 - stageController를 안 채워도(ExecuteRaid 자신의
        /// 방어적 null 가드로 조용히 끝남) _isPending 플래그 자체는 Tick 안에서 ExecuteRaid보다
        /// 먼저 false로 바뀌므로, IsInstancePending만으로 이 검사가 겨냥한 "타이머가 실제로
        /// 멈췄는가"는 충분히 검증된다.
        /// </summary>
        private static void CheckSquadRaidCoordinatorDungeonHiddenPausesCountdown()
        {
            var events = new EventBus();
            var movementSync = new SquadMovementSyncService(events);
            movementSync.Initialize();
            var tactics = new SquadTacticService(events);
            tactics.Initialize();

            PropertyInfo servicesProperty = typeof(GameBootstrapper).GetProperty("Services", BindingFlags.Public | BindingFlags.Static);

            if (servicesProperty == null)
            {
                throw new Exception("GameBootstrapper.Services 프로퍼티를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            object originalServices = servicesProperty.GetValue(null);
            GameObject memberInstance = null;
            GameObject coordinatorGo = null;
            CharacterStatsSO baseStats = null;

            try
            {
                var locator = new ServiceLocator();
                locator.Register(movementSync);
                locator.Register(tactics);
                servicesProperty.SetValue(null, locator);

                baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(baseStats, "moveSpeed", 2f);

                memberInstance = new GameObject("RegressionCheck_RaidMember");
                var provider = memberInstance.AddComponent<CharacterStatsProvider>();
                SetPrivateField(provider, "baseStats", baseStats);

                const int squadIndex = 0;
                movementSync.Register(memberInstance, squadIndex * SoldierDeploymentService.SlotsPerSquad, false);
                tactics.SetTactic(squadIndex, SquadTacticType.LeftRightRaid);

                coordinatorGo = new GameObject("RegressionCheck_RaidCoordinator");
                var coordinator = coordinatorGo.AddComponent<SquadRaidCoordinator>();

                coordinator.OnStageStarted();

                if (memberInstance.activeSelf)
                {
                    throw new Exception("OnStageStarted 직후 습격 대기 부대원이 숨겨지지 않음");
                }

                if (!coordinator.IsInstancePending(memberInstance))
                {
                    throw new Exception("OnStageStarted 직후 IsInstancePending이 false를 반환함 - 무장(ArmSquad)이 실패한 것으로 보임");
                }

                coordinator.SetDungeonHidden(true);

                // 지연시간(기본 8초)을 훨씬 뛰어넘는 델타를 여러 번 흘려도, 숨겨진 동안은
                // 카운트다운 자체가 진행되지 않아야 한다 - 이슈가 재현한 "던전 안에서 습격이
                // 실행됨"과 정반대 결과.
                ((ITickable)coordinator).Tick(1000f);
                ((ITickable)coordinator).Tick(1000f);

                if (!coordinator.IsInstancePending(memberInstance))
                {
                    throw new Exception("던전 은닉 중(SetDungeonHidden(true))인데도 카운트다운이 진행돼 습격이 실행됨(GitHub 이슈 #41 재현)");
                }

                coordinator.SetDungeonHidden(false);

                // 원래 지연시간(8초)에 훨씬 못 미치는 델타 - 아직 실행되면 안 된다(재개가 처음부터
                // 다시 시작하거나 즉시 발동하는 게 아니라, 숨기기 전 남은 시간을 그대로 이어받아야 함).
                ((ITickable)coordinator).Tick(0.1f);

                if (!coordinator.IsInstancePending(memberInstance))
                {
                    throw new Exception("숨김 해제 직후 짧은 델타만으로 습격이 실행됨 - 재개 시 남은 시간이 보존되지 않고 즉시 발동한 것으로 보임");
                }

                // 8초 지연시간을 확실히 넘기는 델타 - 이제는 실행돼야 한다.
                ((ITickable)coordinator).Tick(20f);

                if (coordinator.IsInstancePending(memberInstance))
                {
                    throw new Exception("숨김 해제 후 충분한 시간이 지나도 습격이 재개(실행)되지 않음");
                }
            }
            finally
            {
                servicesProperty.SetValue(null, originalServices);
                movementSync.Shutdown();
                tactics.Shutdown();

                if (coordinatorGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(coordinatorGo);
                }

                if (memberInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(memberInstance);
                }

                if (baseStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(baseStats);
                }
            }
        }

        /// <summary>
        /// GitHub 이슈 #41 - 던전 퇴장이 SoldierRespawner.SetActiveAll(true)로 병사 전원을 일괄
        /// 재활성화할 때, 아직 습격 대기 중인 부대원까지 강제로 드러내던 문제. 습격 대기 중인
        /// 인스턴스(pendingMember)는 SetActiveAll(true) 이후에도 계속 숨겨져 있어야 하고, 습격과
        /// 무관한 인스턴스(normalMember, 던전이 똑같이 숨겼다가 정상적으로 되돌리는 경우)는
        /// 정상적으로 다시 보여야 한다 - 후자를 함께 검증해야 "선택적으로 건너뛰는지"(아무것도
        /// 안 켜지는 폭넓은 실패와 구분)를 실제로 확인할 수 있다.
        /// </summary>
        private static void CheckSoldierRespawnerSetActiveAllSkipsPendingRaidMembers()
        {
            var events = new EventBus();
            var movementSync = new SquadMovementSyncService(events);
            movementSync.Initialize();
            var tactics = new SquadTacticService(events);
            tactics.Initialize();

            PropertyInfo servicesProperty = typeof(GameBootstrapper).GetProperty("Services", BindingFlags.Public | BindingFlags.Static);

            if (servicesProperty == null)
            {
                throw new Exception("GameBootstrapper.Services 프로퍼티를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            object originalServices = servicesProperty.GetValue(null);
            GameObject coordinatorGo = null;
            GameObject pendingMember = null;
            GameObject normalMember = null;
            CharacterStatsSO baseStats = null;
            SoldierRespawner respawner = null;

            try
            {
                var locator = new ServiceLocator();
                locator.Register(movementSync);
                locator.Register(tactics);
                servicesProperty.SetValue(null, locator);

                baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(baseStats, "moveSpeed", 2f);

                pendingMember = new GameObject("RegressionCheck_PendingRaidMember");
                var pendingProvider = pendingMember.AddComponent<CharacterStatsProvider>();
                SetPrivateField(pendingProvider, "baseStats", baseStats);

                normalMember = new GameObject("RegressionCheck_NormalMember");
                normalMember.AddComponent<CharacterStatsProvider>();

                const int squadIndex = 0;
                movementSync.Register(pendingMember, squadIndex * SoldierDeploymentService.SlotsPerSquad, false);
                tactics.SetTactic(squadIndex, SquadTacticType.LeftRightRaid);

                coordinatorGo = new GameObject("RegressionCheck_RaidCoordinator");
                var coordinator = coordinatorGo.AddComponent<SquadRaidCoordinator>();
                coordinator.OnStageStarted(); // pendingMember를 숨기고 습격 카운트다운을 무장한다.

                if (pendingMember.activeSelf)
                {
                    throw new Exception("사전 조건 실패: OnStageStarted 직후 pendingMember가 숨겨지지 않음");
                }

                // 던전 진입 시 SoldierSpawner.SetSoldiersActive(false)가 습격 대기 여부와 무관하게
                // 전원을 숨기는 것과 동일한 상황을 재현 - normalMember도 여기서 숨긴다.
                normalMember.SetActive(false);

                respawner = new SoldierRespawner(events, null, null, null, null, coordinator);

                var pendingSlot = new SoldierSpawnSlot();
                SetPrivateFieldOnPlainObject(pendingSlot, "slotIndex", 0);
                respawner.RegisterSpawned(pendingMember, pendingSlot);

                var normalSlot = new SoldierSpawnSlot();
                SetPrivateFieldOnPlainObject(normalSlot, "slotIndex", 1);
                respawner.RegisterSpawned(normalMember, normalSlot);

                respawner.SetActiveAll(true); // 던전 퇴장 - 전원 재활성화 시도.

                if (pendingMember.activeSelf)
                {
                    throw new Exception("SetActiveAll(true)이 아직 습격 대기 중인 부대원까지 강제로 드러냄(GitHub 이슈 #41 재현)");
                }

                if (!normalMember.activeSelf)
                {
                    throw new Exception("SetActiveAll(true)이 습격과 무관한 인스턴스까지 건너뜀 - 선택적 스킵이 아니라 전체가 동작하지 않는 것으로 보임");
                }
            }
            finally
            {
                servicesProperty.SetValue(null, originalServices);
                respawner?.Dispose();
                movementSync.Shutdown();
                tactics.Shutdown();

                if (coordinatorGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(coordinatorGo);
                }

                if (pendingMember != null)
                {
                    UnityEngine.Object.DestroyImmediate(pendingMember);
                }

                if (normalMember != null)
                {
                    UnityEngine.Object.DestroyImmediate(normalMember);
                }

                if (baseStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(baseStats);
                }
            }
        }

        /// <summary>
        /// 모달 전환(던전 팝업 등)이 밀집 전투 도중 끼어들어도 풀 대여/유휴 상태와
        /// StageProgressTracker의 추적 딕셔너리가 어긋나지 않는지 확인한다. 이슈 #30(풀 이중
        /// 반납)/#40(부대 추적 중복 소속)과 별개로, StageProgressTracker.SetActiveAll 자체는
        /// section FF에서 실제로 버그(오버레이 재개 시 몬스터 HP/위치가 복원되지 않음)가 났던
        /// 지점이라 별도 검증이 필요하다. 유닛 수는 10개로 제한한다 - 이 검사는 결정론적
        /// 시뮬레이션이라(실시간 30초를 기다리지 않음) 20개 이상/30초라는 규모 자체가 다른 코드
        /// 경로를 추가로 발동시키지 않는다(SetActiveAll은 Dictionary 크기와 무관하게 동일하게
        /// 순회하고, ObjectPool의 HashSet 기반 대여 추적도 N에 따라 다르게 동작하지 않는다) -
        /// 사망/생존/피격/이동한 상태가 섞인 채로 모달이 한 번 열렸다 닫히는 조합 자체가 핵심이다.
        /// Health.TakeDamage/Die는 GameBootstrapper.Events(전역 정적 버스)로만 발행하므로,
        /// StageProgressTracker가 구독하는 이 검사만의 로컬 EventBus에는 CharacterDiedEvent를
        /// 직접 발행해줘야 한다(Health 자체의 IsDead/Current 갱신은 TakeDamage 호출만으로도
        /// 정상 동작하므로 이는 검증 자체를 왜곡하지 않는다).
        /// </summary>
        private static void CheckDenseCombatWithModalTransitionInvariants()
        {
            const int totalUnits = 10;
            const int firstWaveKills = 4;

            var events = new EventBus();
            var pool = new Managers.PoolManager();
            pool.Initialize();

            GameObject prefab = null;
            StageSO stage = null;
            CharacterStatsSO baseStats = null;
            StageProgressTracker tracker = null;
            var spawned = new List<GameObject>();
            var spawnPositions = new List<Vector3>();
            int stageClearedCount = 0;
            Action<StageClearedEvent> onStageCleared = _ => stageClearedCount++;

            try
            {
                baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(baseStats, "maxHealth", 100f);

                prefab = new GameObject("RegressionCheck_DenseCombat_Prefab");
                prefab.SetActive(false);
                CharacterStatsProvider provider = prefab.AddComponent<CharacterStatsProvider>();
                SetPrivateField(provider, "baseStats", baseStats);
                prefab.AddComponent<Health>();

                var entry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "monsterPrefab", prefab);
                SetPrivateFieldOnPlainObject(entry, "count", totalUnits);

                stage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stage, "spawnEntries", new[] { entry });

                pool.EnsurePool(prefab, totalUnits, totalUnits + 5);

                events.Subscribe(onStageCleared);
                tracker = new StageProgressTracker(stage, events);

                FieldInfo poolsField = typeof(Managers.PoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
                var pools = (Dictionary<GameObject, ObjectPool<GameObject>>)poolsField.GetValue(pool);
                ObjectPool<GameObject> objectPool = pools[prefab];

                // Health에는 [ExecuteAlways]가 없어 Edit Mode(이 메뉴 실행 자체가 Edit Mode)에서는
                // SetActive(true)가 Awake()를 자동으로 유발하지 않는다 - Play Mode라면 PoolManager.
                // Get()의 SetActive(true) → NotifySpawned() → Health.OnSpawned()(Revive() 호출)
                // 순서가 문제없이 통과하지만, Edit Mode에서는 Awake가 아예 안 불려 _statsProvider가
                // null인 채로 Revive()가 곧장 NRE를 던진다(실제로 겪음 - pool.Get() 내부에서 발생해
                // Get()이 반환된 뒤에 Awake를 대신 호출해주는 것만으로는 이미 늦다). pool.Get()을
                // 처음 호출하기 전에, EnsurePool이 prewarm해둔 인스턴스(아직 비활성, ObjectPool의
                // 내부 스택에 대기 중) 전원에 Awake()를 미리 리플렉션으로 실행해둔다.
                MethodInfo healthAwake = typeof(Health).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo poolStackField = typeof(ObjectPool<GameObject>).GetField("_pool", BindingFlags.NonPublic | BindingFlags.Instance);
                var prewarmedStack = (Stack<GameObject>)poolStackField.GetValue(objectPool);

                foreach (GameObject prewarmed in prewarmedStack)
                {
                    healthAwake.Invoke(prewarmed.GetComponent<Health>(), null);
                }

                for (int i = 0; i < totalUnits; i++)
                {
                    var position = new Vector3(i, 0f, 0f);
                    GameObject instance = pool.Get(prefab, position, Quaternion.identity);
                    spawned.Add(instance);
                    spawnPositions.Add(position);
                    tracker.RegisterSpawned(instance, position);
                }

                if (objectPool.CountActive != totalUnits)
                {
                    throw new Exception($"스폰 직후 대여 개수가 {objectPool.CountActive}(기대={totalUnits})");
                }

                // 1파: 4마리 처치
                for (int i = 0; i < firstWaveKills; i++)
                {
                    KillUnit(spawned[i], events, pool);
                }

                if (objectPool.CountActive != totalUnits - firstWaveKills)
                {
                    throw new Exception($"1파 처치 후 대여 개수가 {objectPool.CountActive}(기대={totalUnits - firstWaveKills})");
                }

                // 생존자 중 하나는 서브리썰 피격(죽지 않음), 다른 하나는 위치를 임의로 옮겨둔다 -
                // 모달 전환 복귀 시 각각 체력/위치가 복원되는지 확인하기 위함(section FF)
                GameObject damagedSurvivor = spawned[totalUnits - 1];
                damagedSurvivor.GetComponent<Health>().TakeDamage(60f);

                GameObject movedSurvivor = spawned[firstWaveKills];
                movedSurvivor.transform.position = new Vector3(999f, 999f, 999f);

                // 모달 전환 1회차: 던전 팝업 열림 (생존자 전원 비활성화, 죽음/보상 없음)
                tracker.SetActiveAll(false);

                for (int i = firstWaveKills; i < totalUnits; i++)
                {
                    if (spawned[i].activeSelf)
                    {
                        throw new Exception($"모달 전환(닫힘) 후에도 생존자 인덱스 {i}가 활성 상태로 남음");
                    }
                }

                // 모달 전환 2회차: 던전 팝업 닫힘 (위치/체력 복원 후 재활성화)
                tracker.SetActiveAll(true);

                for (int i = firstWaveKills; i < totalUnits; i++)
                {
                    if (!spawned[i].activeSelf)
                    {
                        throw new Exception($"모달 전환(복귀) 후에도 생존자 인덱스 {i}가 비활성 상태로 남음");
                    }
                }

                if (movedSurvivor.transform.position != spawnPositions[firstWaveKills])
                {
                    throw new Exception("모달 전환 복귀 후 위치가 스폰 당시 좌표로 복원되지 않음");
                }

                Health damagedHealth = damagedSurvivor.GetComponent<Health>();
                AssertApprox(damagedHealth.MaxHealth, damagedHealth.Current, "모달 전환 복귀 후 서브리썰 피격 생존자의 체력");

                // 모달 전환을 거친 뒤에도 이중 반납 불변조건이 여전히 유효한지 재확인
                // (1파에서 이미 반납된 인스턴스를 다시 반납 시도)
                if (pool.Release(spawned[0]))
                {
                    throw new Exception("모달 전환 이후에도 이중 반납이 거부되지 않음");
                }

                // 2파: 남은 생존자 전원 처치 → 스테이지 클리어
                for (int i = firstWaveKills; i < totalUnits; i++)
                {
                    KillUnit(spawned[i], events, pool);
                }

                if (stageClearedCount != 1)
                {
                    throw new Exception($"StageClearedEvent 발행 횟수가 {stageClearedCount}(기대=1)");
                }

                if (objectPool.CountActive != 0)
                {
                    throw new Exception($"전멸 후 대여 개수가 {objectPool.CountActive}(기대=0)");
                }

                if (objectPool.CountInactive != totalUnits)
                {
                    throw new Exception($"전멸 후 유휴 개수가 {objectPool.CountInactive}(기대={totalUnits})");
                }
            }
            finally
            {
                events.Unsubscribe(onStageCleared);
                tracker?.Dispose();

                if (prefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(prefab);
                }

                if (stage != null)
                {
                    UnityEngine.Object.DestroyImmediate(stage);
                }

                if (baseStats != null)
                {
                    UnityEngine.Object.DestroyImmediate(baseStats);
                }

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        /// <summary>
        /// CheckDenseCombatWithModalTransitionInvariants 전용 헬퍼 - 실전투에서 처치 하나가
        /// 거치는 세 단계(체력 소진 → 사망 이벤트 발행 → 풀 반납)를 그대로 재현한다. 실제
        /// 게임에서는 Character.PoolReleaseOnDeath가 GameBootstrapper.Events(전역 버스)의
        /// CharacterDiedEvent를 구독해 반납을 대신하지만, 이 검사는 격리된 로컬 EventBus를
        /// 쓰므로 그 역할을 직접 수행한다.
        /// </summary>
        private static void KillUnit(GameObject instance, EventBus events, Managers.PoolManager pool)
        {
            instance.GetComponent<Health>().TakeDamage(9999f);
            events.Publish(new CharacterDiedEvent(instance));
            pool.Release(instance);
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

        /// <summary>
        /// GitHub 이슈 #25 - 최근에 등록한 항목부터 닫히는 LIFO 순서를 확인한다(중첩 팝업에서
        /// Back 한 번이 최상위 팝업 하나만 닫아야 한다는 완료 조건과 직결).
        /// </summary>
        private static void CheckBackNavigationServiceLifoOrder()
        {
            var service = new BackNavigationService();
            var first = new TestDismissible();
            var second = new TestDismissible();

            service.Register(first);
            service.Register(second);

            if (!service.TryDismissTop())
            {
                throw new Exception("스택에 항목이 있는데도 TryDismissTop이 false를 반환함");
            }

            if (!second.WasDismissed || first.WasDismissed)
            {
                throw new Exception($"최근 등록(second)이 먼저 닫혀야 함 - second={second.WasDismissed}, first={first.WasDismissed}");
            }

            if (!service.TryDismissTop())
            {
                throw new Exception("두 번째 TryDismissTop이 false를 반환함 - first가 아직 스택에 남아있어야 함");
            }

            if (!first.WasDismissed)
            {
                throw new Exception("first가 두 번째 Back에서 닫혀야 함");
            }

            if (service.TryDismissTop())
            {
                throw new Exception("스택이 빈 뒤에도 TryDismissTop이 true를 반환함");
            }
        }

        /// <summary>
        /// 같은 인스턴스를 실수로 두 번 Register해도 스택에 중복으로 쌓이지 않아, 단 한 번의
        /// TryDismissTop으로 완전히 비워지는지 확인한다(GitHub 이슈 #25).
        /// </summary>
        private static void CheckBackNavigationServiceDuplicateRegistration()
        {
            var service = new BackNavigationService();
            var dismissible = new TestDismissible();

            service.Register(dismissible);
            service.Register(dismissible);

            if (!service.TryDismissTop())
            {
                throw new Exception("등록된 항목이 있는데도 TryDismissTop이 false를 반환함");
            }

            if (service.TryDismissTop())
            {
                throw new Exception("중복 등록으로 인해 같은 인스턴스가 스택에 두 번 남아있음");
            }
        }

        /// <summary>
        /// Close() 호출 없이 GameObject가 통째로 파괴된 IDismissible 항목(가짜 null)이 스택
        /// 최상단에 있어도 TryDismissTop이 예외 없이 건너뛰고, 그 아래 살아있는 항목을 대신
        /// 닫는지 확인한다(GitHub 이슈 #25 - "비활성·파괴된 팝업 참조가 스택에 남지 않음").
        /// UI.SimplePopupUI를 실제 MonoBehaviour 기반 IDismissible로 재사용한다 - Awake가
        /// 실행되지 않도록 비활성 상태에서 AddComponent한다(AssertTrySpawnBossReturnsFalse와
        /// 동일한 이유).
        /// </summary>
        private static void CheckBackNavigationServicePrunesDestroyedEntry()
        {
            var service = new BackNavigationService();
            var liveBelow = new TestDismissible();

            var go = new GameObject("RegressionCheck_BackNav_DestroyedPopup");
            go.SetActive(false);
            var popup = go.AddComponent<SimplePopupUI>();

            service.Register(liveBelow);
            service.Register(popup);

            UnityEngine.Object.DestroyImmediate(go);

            if (!service.TryDismissTop())
            {
                throw new Exception("파괴된 최상단 항목을 건너뛰고 그 아래 살아있는 항목을 닫아야 하는데 false를 반환함");
            }

            if (!liveBelow.WasDismissed)
            {
                throw new Exception("파괴된 항목 아래의 살아있는 항목이 대신 닫혀야 함");
            }
        }

        /// <summary>
        /// BackInputRouter.TryExitWaitingDungeon(private static)의 세 분기를 직접 검증한다
        /// (GitHub 이슈 #25) - isActive=false면 해당 없음(false, 액션 미호출), isActive&&isFighting이면
        /// 아직 자발적 이탈 기능이 없어 조용히 소비만(true, 액션 미호출), isActive&&!isFighting(실패
        /// 대기 상태)이면 실제로 나가기 액션을 호출(true, 액션 호출).
        /// </summary>
        private static void CheckBackInputRouterTryExitWaitingDungeon()
        {
            MethodInfo method = typeof(BackInputRouter).GetMethod(
                "TryExitWaitingDungeon", BindingFlags.NonPublic | BindingFlags.Static);

            if (method == null)
            {
                throw new Exception("BackInputRouter.TryExitWaitingDungeon 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            int callCount = 0;
            Action exitAction = () => callCount++;

            bool notActiveResult = (bool)method.Invoke(null, new object[] { false, false, exitAction });
            if (notActiveResult || callCount != 0)
            {
                throw new Exception($"isActive=false인데 결과={notActiveResult}, 호출횟수={callCount}(기대: false/0)");
            }

            bool fightingResult = (bool)method.Invoke(null, new object[] { true, true, exitAction });
            if (!fightingResult || callCount != 0)
            {
                throw new Exception($"전투 중인데 결과={fightingResult}, 호출횟수={callCount}(기대: true/0 - 소비만 하고 이탈 액션은 호출 안 함)");
            }

            bool waitingResult = (bool)method.Invoke(null, new object[] { true, false, exitAction });
            if (!waitingResult || callCount != 1)
            {
                throw new Exception($"실패 대기 상태인데 결과={waitingResult}, 호출횟수={callCount}(기대: true/1 - 나가기 액션을 실제로 호출)");
            }
        }

        /// <summary>
        /// GitHub 이슈 #25 대응으로 IDismissible을 구현하도록 일괄 수정한 26개 팝업 클래스(부대
        /// 여닫기 셸 SimplePopupUI 포함, "실패" 5종은 컨트롤러 레벨 정책으로 처리해 의도적으로
        /// 제외)가 실제로 전부 인터페이스를 구현하는지 리플렉션으로 스윕한다 - 대규모 기계적 편집
        /// 중 파일 하나를 빠뜨리는 실수를 잡기 위함.
        /// </summary>
        private static void CheckPopupClassesImplementIDismissible()
        {
            string[] expectedDismissibleClassNames =
            {
                "BossDungeonClearPopupUI", "BossDungeonSelectPopupUI", "ConfirmationPopupUI",
                "EquipmentDetailPopupUI", "EquipmentEnhancementPopupUI", "EquipmentPulledPopupUI",
                "EquipmentSlotPopupUI", "GachaPopupUI", "GoldDungeonClearPopupUI",
                "NotificationSettingsPopupUI", "OfflineProgressPopupUI", "RankInfoPopupUI",
                "RankUpPopupUI", "ResetDataConfirmPopupUI", "SimplePopupUI", "SkillDetailPopupUI",
                "SkillDungeonClearPopupUI", "SkillPulledPopupUI", "SoldierBehaviorProfilePopupUI",
                "SoldierDeploymentPopupUI", "SoldierDetailPopupUI", "SoldierPulledPopupUI",
                "SoldierRescueDungeonClearPopupUI", "SquadTacticOptionPopupUI",
                "StageRepeatPickerPopupUI", "StoneDungeonClearPopupUI",
            };

            foreach (string className in expectedDismissibleClassNames)
            {
                Type type = Type.GetType($"UI.{className}, Assembly-CSharp");

                if (type == null)
                {
                    throw new Exception($"타입 'UI.{className}'을 찾지 못함 - 클래스 이름이 바뀌었는지 확인");
                }

                if (!typeof(IDismissible).IsAssignableFrom(type))
                {
                    throw new Exception($"'{className}'이 IDismissible을 구현하지 않음");
                }
            }
        }

        /// <summary>
        /// UnityEngine.Object가 아닌 순수 C# IDismissible 테스트 더블 - BackNavigationService의
        /// LIFO/중복등록 로직만 격리해서 확인할 때 쓴다.
        /// </summary>
        private sealed class TestDismissible : IDismissible
        {
            public bool WasDismissed { get; private set; }

            public bool TryDismiss()
            {
                WasDismissed = true;
                return true;
            }
        }

        private static SoldierSO CreateSoldierDefinition(string stableId, int cost = 1)
        {
            var definition = ScriptableObject.CreateInstance<SoldierSO>();
            SetPrivateField(definition, "stableId", stableId);
            SetPrivateField(definition, "cost", cost);
            return definition;
        }

        private static SoldierCatalogSO CreateSoldierCatalog(SoldierSO[] soldiers)
        {
            var catalog = ScriptableObject.CreateInstance<SoldierCatalogSO>();
            SetPrivateField(catalog, "soldiers", soldiers);
            return catalog;
        }

        /// <summary>
        /// GitHub 이슈 #26 재현 절차 A - nextInstanceId 충돌. InstanceId 0인 병사 한 명과
        /// nextInstanceId=0을 RestoreSnapshot에 전달한 뒤 AddSoldier로 새 병사를 추가하면,
        /// 수정 전에는 새 병사가 같은 ID 0을 재발급받아 기존 항목을 덮어썼다(로스터 수가 1로
        /// 유지). 수정 후에는 nextInstanceId가 max(저장값, 복원된 최대 InstanceId+1)로 보정돼
        /// 새 병사가 ID 1을 받고 로스터 수가 2가 된다.
        /// </summary>
        private static void CheckSoldierRosterRestoreNormalizesNextInstanceId()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            SoldierSO definitionA = null;
            SoldierSO definitionB = null;
            SoldierCatalogSO catalog = null;
            BehaviorProfileCatalogSO behaviorCatalog = null;

            try
            {
                definitionA = CreateSoldierDefinition("stable-a");
                definitionB = CreateSoldierDefinition("stable-b");
                catalog = CreateSoldierCatalog(new[] { definitionA, definitionB });
                behaviorCatalog = ScriptableObject.CreateInstance<BehaviorProfileCatalogSO>();

                var snapshot = new[]
                {
                    new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-a", InstanceId = 0, BehaviorProfileStableId = null },
                };

                roster.RestoreSnapshot(snapshot, catalog, behaviorCatalog, nextInstanceId: 0);

                OwnedSoldier added = roster.AddSoldier(definitionB);

                if (added.InstanceId == 0)
                {
                    throw new Exception("새 병사가 기존 InstanceId 0과 충돌함(nextInstanceId가 보정되지 않음)");
                }

                if (roster.Roster.Count != 2)
                {
                    throw new Exception($"로스터 수가 {roster.Roster.Count}(기대=2) - 덮어쓰기로 하나를 잃음");
                }

                if (!roster.TryGet(0, out OwnedSoldier original) || original.Definition != definitionA)
                {
                    throw new Exception("InstanceId 0이 가리키는 정의가 원래 병사(definitionA)가 아님");
                }
            }
            finally
            {
                if (definitionA != null) UnityEngine.Object.DestroyImmediate(definitionA);
                if (definitionB != null) UnityEngine.Object.DestroyImmediate(definitionB);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                if (behaviorCatalog != null) UnityEngine.Object.DestroyImmediate(behaviorCatalog);
            }
        }

        /// <summary>
        /// 음수 InstanceId와 중복 InstanceId(먼저 나온 항목 우선) 둘 다 폐기되고, RestoreResult의
        /// 폐기 건수에 정확히 반영되는지 확인한다(GitHub 이슈 #26).
        /// </summary>
        private static void CheckSoldierRosterRestoreRejectsNegativeAndDuplicateIds()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            SoldierSO definition = null;
            SoldierCatalogSO catalog = null;
            BehaviorProfileCatalogSO behaviorCatalog = null;

            try
            {
                definition = CreateSoldierDefinition("stable-a");
                catalog = CreateSoldierCatalog(new[] { definition });
                behaviorCatalog = ScriptableObject.CreateInstance<BehaviorProfileCatalogSO>();

                var snapshot = new[]
                {
                    new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-a", InstanceId = -1, BehaviorProfileStableId = null },
                    new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-a", InstanceId = 5, BehaviorProfileStableId = null },
                    new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-a", InstanceId = 5, BehaviorProfileStableId = null },
                };

                SoldierRosterService.RestoreResult result = roster.RestoreSnapshot(snapshot, catalog, behaviorCatalog, nextInstanceId: 0);

                if (result.RestoredCount != 1 || result.DiscardedNegativeInstanceId != 1 || result.DiscardedDuplicateInstanceId != 1)
                {
                    throw new Exception($"복원={result.RestoredCount}(기대=1), 음수폐기={result.DiscardedNegativeInstanceId}(기대=1), 중복폐기={result.DiscardedDuplicateInstanceId}(기대=1)");
                }

                if (roster.Roster.Count != 1)
                {
                    throw new Exception($"최종 로스터 수가 {roster.Roster.Count}(기대=1)");
                }
            }
            finally
            {
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                if (behaviorCatalog != null) UnityEngine.Object.DestroyImmediate(behaviorCatalog);
            }
        }

        /// <summary>
        /// 같은 서비스 인스턴스에 RestoreSnapshot을 두 번 호출하면(재로그인/재로드 시나리오),
        /// 첫 번째 호출로 들어온 항목이 두 번째 호출 이후에도 잔존하지 않아야 한다(GitHub 이슈 #26).
        /// </summary>
        private static void CheckSoldierRosterRestoreClearsOnReRestore()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            SoldierSO definitionA = null;
            SoldierSO definitionB = null;
            SoldierCatalogSO catalog = null;
            BehaviorProfileCatalogSO behaviorCatalog = null;

            try
            {
                definitionA = CreateSoldierDefinition("stable-a");
                definitionB = CreateSoldierDefinition("stable-b");
                catalog = CreateSoldierCatalog(new[] { definitionA, definitionB });
                behaviorCatalog = ScriptableObject.CreateInstance<BehaviorProfileCatalogSO>();

                roster.RestoreSnapshot(
                    new[] { new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-a", InstanceId = 0, BehaviorProfileStableId = null } },
                    catalog, behaviorCatalog, nextInstanceId: 1);

                roster.RestoreSnapshot(
                    new[] { new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-b", InstanceId = 10, BehaviorProfileStableId = null } },
                    catalog, behaviorCatalog, nextInstanceId: 11);

                if (roster.Roster.Count != 1)
                {
                    throw new Exception($"두 번째 복원 후 로스터 수가 {roster.Roster.Count}(기대=1) - 첫 번째 복원분(InstanceId 0)이 잔존함");
                }

                if (roster.TryGet(0, out _))
                {
                    throw new Exception("첫 번째 복원의 InstanceId 0이 두 번째 복원 후에도 남아있음");
                }
            }
            finally
            {
                if (definitionA != null) UnityEngine.Object.DestroyImmediate(definitionA);
                if (definitionB != null) UnityEngine.Object.DestroyImmediate(definitionB);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                if (behaviorCatalog != null) UnityEngine.Object.DestroyImmediate(behaviorCatalog);
            }
        }

        /// <summary>
        /// 복원된 최대 InstanceId가 int.MaxValue에 가까워도 nextInstanceId 계산(long 중간 연산)이
        /// 오버플로 없이 int.MaxValue로 saturate하는지 확인한다(GitHub 이슈 #26 - "오버플로 처리").
        /// </summary>
        private static void CheckSoldierRosterRestoreSaturatesOnOverflow()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            SoldierSO definition = null;
            SoldierCatalogSO catalog = null;
            BehaviorProfileCatalogSO behaviorCatalog = null;

            try
            {
                definition = CreateSoldierDefinition("stable-a");
                catalog = CreateSoldierCatalog(new[] { definition });
                behaviorCatalog = ScriptableObject.CreateInstance<BehaviorProfileCatalogSO>();

                var snapshot = new[]
                {
                    new SoldierRosterService.OwnedSoldierSnapshot { StableId = "stable-a", InstanceId = int.MaxValue, BehaviorProfileStableId = null },
                };

                roster.RestoreSnapshot(snapshot, catalog, behaviorCatalog, nextInstanceId: 0);

                int nextInstanceIdField = (int)GetPrivateFieldOnPlainObject(roster, "_nextInstanceId");

                if (nextInstanceIdField != int.MaxValue)
                {
                    throw new Exception($"_nextInstanceId={nextInstanceIdField}(기대=int.MaxValue, saturate 실패)");
                }
            }
            finally
            {
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                if (behaviorCatalog != null) UnityEngine.Object.DestroyImmediate(behaviorCatalog);
            }
        }

        /// <summary>
        /// GitHub 이슈 #26 재현 절차 B - 존재하지 않는 병사(InstanceId 999999)로 채워진 슬롯이
        /// RestoreSnapshot 시점에 폐기되어, TryAssign으로 실제 유효한 병사를 그 슬롯에 배치할 수
        /// 있는지 확인한다. 수정 전에는 유령 슬롯이 Dictionary를 그대로 점유해 TryAssign조차 "이미
        /// 배정됨"으로 남아있는 게 아니라 그 슬롯 자체가 다른 유효한 배정으로 덮어써질 수는 있었지만
        /// (TryAssign은 슬롯 인덱스만 보고 덮어씀), 문제의 핵심은 TryDeploy의 빈 슬롯 탐색이
        /// ContainsKey만 확인해 유령 슬롯도 "찬 것"으로 셌다는 것 - 여기서는 그 탐색 로직과 동일한
        /// 방식으로 슬롯이 실제로 비어있는지(_slotToInstanceId에 없는지) 직접 확인한다.
        /// </summary>
        private static void CheckSoldierDeploymentRestoreDiscardsGhostAndOutOfRangeSlots()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            var deployment = new SoldierDeploymentService(events, roster, null);
            SoldierSO definition = null;

            try
            {
                definition = CreateSoldierDefinition("stable-a");
                OwnedSoldier owned = roster.AddSoldier(definition);

                var snapshot = new SoldierDeploymentService.DeploymentSnapshotEntry[SoldierDeploymentService.TotalSlotCount + 2];

                for (int i = 0; i < SoldierDeploymentService.TotalSlotCount; i++)
                {
                    snapshot[i] = new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = i, InstanceId = 999999 };
                }

                snapshot[SoldierDeploymentService.TotalSlotCount] = new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = -1, InstanceId = owned.InstanceId };
                snapshot[SoldierDeploymentService.TotalSlotCount + 1] = new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = SoldierDeploymentService.TotalSlotCount, InstanceId = owned.InstanceId };

                SoldierDeploymentService.RestoreResult result = deployment.RestoreSnapshot(snapshot);

                if (result.RestoredCount != 0)
                {
                    throw new Exception($"복원된 슬롯 수가 {result.RestoredCount}(기대=0) - 전부 유령/범위 밖이어야 함");
                }

                if (result.DiscardedMissingRosterEntry != SoldierDeploymentService.TotalSlotCount)
                {
                    throw new Exception($"유령 슬롯 폐기 건수={result.DiscardedMissingRosterEntry}(기대={SoldierDeploymentService.TotalSlotCount})");
                }

                if (result.DiscardedOutOfRangeSlot != 2)
                {
                    throw new Exception($"범위 밖 슬롯 폐기 건수={result.DiscardedOutOfRangeSlot}(기대=2)");
                }

                if (deployment.GetDeployedSoldiers().GetEnumerator().MoveNext())
                {
                    throw new Exception("복원 직후 GetDeployedSoldiers가 비어있지 않음");
                }

                if (!deployment.TryAssign(0, owned.InstanceId))
                {
                    throw new Exception("유령 슬롯이 제거된 뒤에도 슬롯 0에 유효한 병사를 배치하지 못함");
                }
            }
            finally
            {
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                deployment.Shutdown();
            }
        }

        /// <summary>
        /// GitHub 이슈 #26 재현 절차 C - 같은 InstanceId가 여러 슬롯을 가리키면 SlotIndex가 가장
        /// 낮은 슬롯 하나만 유지되고, 나머지는 중복으로 폐기되는지 확인한다(GetDeployedSoldiers/
        /// GetTotalDeployedCost가 중복 집계하지 않아야 함).
        /// </summary>
        private static void CheckSoldierDeploymentRestoreDedupesInstanceId()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            var deployment = new SoldierDeploymentService(events, roster, null);
            SoldierSO definition = null;

            try
            {
                definition = CreateSoldierDefinition("stable-a", cost: 3);
                OwnedSoldier owned = roster.AddSoldier(definition);

                var snapshot = new[]
                {
                    new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = 1, InstanceId = owned.InstanceId },
                    new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = 0, InstanceId = owned.InstanceId },
                };

                SoldierDeploymentService.RestoreResult result = deployment.RestoreSnapshot(snapshot);

                if (result.RestoredCount != 1 || result.DiscardedDuplicateInstanceId != 1)
                {
                    throw new Exception($"복원={result.RestoredCount}(기대=1), 중복폐기={result.DiscardedDuplicateInstanceId}(기대=1)");
                }

                if (!deployment.TryGetSlotOf(owned.InstanceId, out int slotIndex) || slotIndex != 0)
                {
                    throw new Exception($"유지된 슬롯이 {slotIndex}(기대=0, 더 낮은 슬롯이 우선해야 함)");
                }

                int deployedCount = 0;
                foreach (OwnedSoldier _ in deployment.GetDeployedSoldiers())
                {
                    deployedCount++;
                }

                if (deployedCount != 1)
                {
                    throw new Exception($"GetDeployedSoldiers 열거 수={deployedCount}(기대=1, 중복 집계됨)");
                }

                if (deployment.GetTotalDeployedCost() != 3)
                {
                    throw new Exception($"GetTotalDeployedCost={deployment.GetTotalDeployedCost()}(기대=3, 중복 집계됨)");
                }
            }
            finally
            {
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                deployment.Shutdown();
            }
        }

        /// <summary>
        /// SoldierDeploymentService도 재복원 시 이전 배정이 잔존하지 않는지 확인한다(GitHub 이슈 #26).
        /// </summary>
        private static void CheckSoldierDeploymentRestoreClearsOnReRestore()
        {
            var events = new EventBus();
            var roster = new SoldierRosterService(events);
            var deployment = new SoldierDeploymentService(events, roster, null);
            SoldierSO definitionA = null;
            SoldierSO definitionB = null;

            try
            {
                definitionA = CreateSoldierDefinition("stable-a");
                definitionB = CreateSoldierDefinition("stable-b");
                OwnedSoldier ownedA = roster.AddSoldier(definitionA);
                OwnedSoldier ownedB = roster.AddSoldier(definitionB);

                deployment.RestoreSnapshot(new[] { new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = 0, InstanceId = ownedA.InstanceId } });
                deployment.RestoreSnapshot(new[] { new SoldierDeploymentService.DeploymentSnapshotEntry { SlotIndex = 1, InstanceId = ownedB.InstanceId } });

                if (deployment.TryGetSlotOf(ownedA.InstanceId, out _))
                {
                    throw new Exception("첫 번째 복원의 배정(ownedA→슬롯0)이 두 번째 복원 후에도 남아있음");
                }

                if (!deployment.TryGetSlotOf(ownedB.InstanceId, out int slotIndex) || slotIndex != 1)
                {
                    throw new Exception("두 번째 복원의 배정(ownedB→슬롯1)이 반영되지 않음");
                }
            }
            finally
            {
                if (definitionA != null) UnityEngine.Object.DestroyImmediate(definitionA);
                if (definitionB != null) UnityEngine.Object.DestroyImmediate(definitionB);
                deployment.Shutdown();
            }
        }

        /// <summary>
        /// SquadTacticService.SetTactic(공개 진입점)이 범위 밖 squadIndex(-1, SquadCount)와
        /// 정의되지 않은 enum 값(99) 둘 다 저장도 이벤트 발행도 하지 않는지 확인한다(GitHub 이슈
        /// #26 코멘트 - "공개 SetTactic()도 잘못된 인덱스/enum 값을 저장하거나 이벤트로 발행하지 않음").
        /// </summary>
        private static void CheckSquadTacticServiceSetTacticRejectsInvalid()
        {
            var events = new EventBus();
            var service = new SquadTacticService(events);

            int publishCount = 0;
            events.Subscribe<SquadTacticChangedEvent>(_ => publishCount++);

            service.SetTactic(-1, SquadTacticType.LeftRightRaid);
            service.SetTactic(SoldierDeploymentService.SquadCount, SquadTacticType.LeftRightRaid);
            service.SetTactic(0, (SquadTacticType)99);

            if (publishCount != 0)
            {
                throw new Exception($"잘못된 SetTactic 호출 {publishCount}건이 이벤트를 발행함(기대=0)");
            }

            if (service.GetTactic(-1) != SquadTacticType.None || service.GetTactic(0) != SquadTacticType.None)
            {
                throw new Exception("잘못된 값이 실제로 저장됨");
            }
        }

        /// <summary>
        /// RestoreSnapshot에서 손상된 항목(범위 밖 SquadIndex) 하나가 있어도 나머지 유효한 항목은
        /// 그대로 복원되는지 확인한다(GitHub 이슈 #26 코멘트).
        /// </summary>
        private static void CheckSquadTacticServiceRestoreSkipsInvalidEntry()
        {
            var events = new EventBus();
            var service = new SquadTacticService(events);

            var snapshot = new[]
            {
                new SquadTacticService.SquadTacticSnapshotEntry { SquadIndex = -1, Tactic = SquadTacticType.LeftRightRaid },
                new SquadTacticService.SquadTacticSnapshotEntry { SquadIndex = 2, Tactic = SquadTacticType.ShieldWall },
            };

            SquadTacticService.RestoreResult result = service.RestoreSnapshot(snapshot);

            if (result.RestoredCount != 1 || result.DiscardedInvalidEntry != 1)
            {
                throw new Exception($"복원={result.RestoredCount}(기대=1), 폐기={result.DiscardedInvalidEntry}(기대=1)");
            }

            if (service.GetTactic(2) != SquadTacticType.ShieldWall)
            {
                throw new Exception("유효한 항목(부대 2 → ShieldWall)이 복원되지 않음");
            }
        }

        /// <summary>
        /// GitHub 이슈 #26 코멘트 재현 - SquadRaidCoordinator.OnTacticChanged가 범위 밖
        /// SquadIndex(-1)를 가진 이벤트를 받아도 IndexOutOfRangeException 없이 조용히 무시하는지
        /// 확인한다. Awake가 실행되지 않도록 비활성 상태에서 AddComponent한다(section GY와 동일한
        /// 이유).
        /// </summary>
        private static void CheckSquadRaidCoordinatorOnTacticChangedOutOfRangeIndex()
        {
            var go = new GameObject("RegressionCheck_SquadRaidCoordinator");
            go.SetActive(false);

            try
            {
                var coordinator = go.AddComponent<SquadRaidCoordinator>();

                MethodInfo method = typeof(SquadRaidCoordinator).GetMethod("OnTacticChanged", BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null)
                {
                    throw new Exception("SquadRaidCoordinator.OnTacticChanged 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
                }

                var evt = new SquadTacticChangedEvent(-1, SquadTacticType.None);
                method.Invoke(coordinator, new object[] { evt });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// GitHub 이슈 #27 재현 절차 - Count=3, SpawnInterval=999초인 일반 웨이브 하나를
        /// MonsterSpawner.Tick(0.001초)로 한 번만 틱해도 3마리 전부가 즉시 스폰되고 스포너가
        /// 완료 상태가 되는지 확인한다(실전이 SpawnInterval을 무시하고 즉시 스폰한다는 확정된
        /// 정책을 그대로 잠그는 회귀 방지 검사).
        /// </summary>
        private static void CheckMonsterSpawnerIgnoresSpawnIntervalRealCombat()
        {
            var events = new EventBus();
            var pool = new Managers.PoolManager();
            pool.Initialize();

            GameObject prefab = null;
            StageSO stage = null;
            CharacterStatsSO baseStats = null;
            StageProgressTracker tracker = null;
            GameObject playerTargetGo = null;
            MonsterSpawner spawner = null;

            try
            {
                baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
                SetPrivateFloat(baseStats, "maxHealth", 100f);

                prefab = new GameObject("RegressionCheck_SpawnInterval_Prefab");
                prefab.SetActive(false);
                CharacterStatsProvider provider = prefab.AddComponent<CharacterStatsProvider>();
                SetPrivateField(provider, "baseStats", baseStats);
                prefab.AddComponent<Health>();

                var entry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "monsterPrefab", prefab);
                SetPrivateFieldOnPlainObject(entry, "count", 3);
                SetPrivateFieldOnPlainObject(entry, "spawnInterval", 999f);

                stage = ScriptableObject.CreateInstance<StageSO>();
                SetPrivateField(stage, "spawnEntries", new[] { entry });

                pool.EnsurePool(prefab, 3, 3);

                FieldInfo poolsField = typeof(Managers.PoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
                var pools = (Dictionary<GameObject, ObjectPool<GameObject>>)poolsField.GetValue(pool);
                ObjectPool<GameObject> objectPool = pools[prefab];

                // Edit Mode에서는 Health.Awake가 AddComponent 시점에만 실행되므로(section GY),
                // EnsurePool이 prewarm해둔 인스턴스에 미리 Awake를 리플렉션으로 실행해둔다.
                MethodInfo healthAwake = typeof(Health).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo poolStackField = typeof(ObjectPool<GameObject>).GetField("_pool", BindingFlags.NonPublic | BindingFlags.Instance);
                var prewarmedStack = (Stack<GameObject>)poolStackField.GetValue(objectPool);

                foreach (GameObject prewarmed in prewarmedStack)
                {
                    healthAwake.Invoke(prewarmed.GetComponent<Health>(), null);
                }

                tracker = new StageProgressTracker(stage, events);
                playerTargetGo = new GameObject("RegressionCheck_SpawnInterval_PlayerTarget");

                spawner = new MonsterSpawner(stage, pool, playerTargetGo.transform, tracker, null, 1f);
                spawner.Tick(0.001f);

                if (objectPool.CountActive != 3)
                {
                    throw new Exception($"0.001초 뒤 대여된 인스턴스 수={objectPool.CountActive}(기대=3) - SpawnInterval=999가 실전에 영향을 준 것으로 보임");
                }

                if (!spawner.IsFinished)
                {
                    throw new Exception("한 틱 뒤 스포너가 완료 상태가 아님(SpawnInterval을 실제로 지키려 한 것으로 보임)");
                }
            }
            finally
            {
                spawner?.Dispose();
                tracker?.Dispose();

                if (playerTargetGo != null) UnityEngine.Object.DestroyImmediate(playerTargetGo);
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (baseStats != null) UnityEngine.Object.DestroyImmediate(baseStats);

                FieldInfo poolRootField = typeof(Managers.PoolManager).GetField("_poolRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                var poolRoot = (Transform)poolRootField?.GetValue(pool);

                if (poolRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(poolRoot.gameObject);
                }
            }
        }

        private static StageSO CreateOfflineTestStage(float[] spawnIntervals, bool[] spawnWithTactics, out GameObject prefab, out CharacterStatsSO baseStats)
        {
            baseStats = ScriptableObject.CreateInstance<CharacterStatsSO>();
            SetPrivateFloat(baseStats, "maxHealth", 100f);

            prefab = new GameObject("RegressionCheck_OfflineSim_Prefab");
            prefab.SetActive(false);
            CharacterStatsProvider provider = prefab.AddComponent<CharacterStatsProvider>();
            SetPrivateField(provider, "baseStats", baseStats);

            var entries = new MonsterSpawnEntry[spawnIntervals.Length];

            for (int i = 0; i < spawnIntervals.Length; i++)
            {
                var entry = new MonsterSpawnEntry();
                SetPrivateFieldOnPlainObject(entry, "monsterPrefab", prefab);
                SetPrivateFieldOnPlainObject(entry, "count", 10);
                SetPrivateFieldOnPlainObject(entry, "spawnInterval", spawnIntervals[i]);
                SetPrivateFieldOnPlainObject(entry, "spawnWithTactics", spawnWithTactics != null && spawnWithTactics[i]);
                entries[i] = entry;
            }

            StageSO stage = ScriptableObject.CreateInstance<StageSO>();
            SetPrivateField(stage, "spawnEntries", entries);

            return stage;
        }

        /// <summary>
        /// SpawnInterval 값만 다른(0 vs 999999) 동일한 스테이지를 같은 DPS/예산으로 시뮬레이션하면
        /// 완전히 동일한 TotalMonstersKilled/TimesCleared가 나오는지 확인한다(GitHub 이슈 #27 -
        /// "실전과 오프라인이 동일한 유효 스폰 시간 규칙을 공유함" 및 간격 0/매우 큰 값 경계).
        /// 골드/장비는 LootRoller의 RNG가 섞여 정확히 같음을 보장할 수 없어(그리고 이 합성
        /// 프리팹엔 MonsterLootProvider 자체가 없어 항상 0이지만) 비교 대상에서 제외한다.
        /// </summary>
        private static void CheckOfflineStageSimulatorResultIndependentOfSpawnInterval()
        {
            var simulator = new OfflineStageSimulator(null, null, 1f);
            StageSO stageZero = null;
            StageSO stageHuge = null;
            GameObject prefabZero = null;
            GameObject prefabHuge = null;
            CharacterStatsSO baseStatsZero = null;
            CharacterStatsSO baseStatsHuge = null;

            try
            {
                stageZero = CreateOfflineTestStage(new[] { 0f, 0f }, null, out prefabZero, out baseStatsZero);
                stageHuge = CreateOfflineTestStage(new[] { 999999f, 0.0001f }, null, out prefabHuge, out baseStatsHuge);

                OfflineStageSimulator.Result resultZero = simulator.Simulate(stageZero, totalDps: 50f, budget: 3600f);
                OfflineStageSimulator.Result resultHuge = simulator.Simulate(stageHuge, totalDps: 50f, budget: 3600f);

                if (!resultZero.Success || !resultHuge.Success)
                {
                    throw new Exception($"두 시뮬레이션 모두 성공해야 함 - zero.Success={resultZero.Success}, huge.Success={resultHuge.Success}");
                }

                if (resultZero.TotalMonstersKilled != resultHuge.TotalMonstersKilled)
                {
                    throw new Exception($"TotalMonstersKilled가 SpawnInterval에 따라 달라짐 - zero={resultZero.TotalMonstersKilled}, huge={resultHuge.TotalMonstersKilled}(기대: 동일)");
                }

                if (resultZero.TimesCleared != resultHuge.TimesCleared)
                {
                    throw new Exception($"TimesCleared가 SpawnInterval에 따라 달라짐 - zero={resultZero.TimesCleared}, huge={resultHuge.TimesCleared}(기대: 동일)");
                }
            }
            finally
            {
                if (prefabZero != null) UnityEngine.Object.DestroyImmediate(prefabZero);
                if (prefabHuge != null) UnityEngine.Object.DestroyImmediate(prefabHuge);
                if (stageZero != null) UnityEngine.Object.DestroyImmediate(stageZero);
                if (stageHuge != null) UnityEngine.Object.DestroyImmediate(stageHuge);
                if (baseStatsZero != null) UnityEngine.Object.DestroyImmediate(baseStatsZero);
                if (baseStatsHuge != null) UnityEngine.Object.DestroyImmediate(baseStatsHuge);
            }
        }

        /// <summary>
        /// 모든 엔트리의 SpawnInterval이 0인 스테이지도 시뮬레이션이 실패하지 않는지 확인한다
        /// (GitHub 이슈 #27 - 수정 전에는 totalSpawnDuration이 0이 되어 TryBuildStageInfo가
        /// 몬스터가 있어도 무조건 false를 반환, 시뮬레이션 자체가 통째로 실패했다).
        /// </summary>
        private static void CheckOfflineStageSimulatorAllZeroSpawnIntervalSucceeds()
        {
            var simulator = new OfflineStageSimulator(null, null, 1f);
            StageSO stage = null;
            GameObject prefab = null;
            CharacterStatsSO baseStats = null;

            try
            {
                stage = CreateOfflineTestStage(new[] { 0f, 0f, 0f }, null, out prefab, out baseStats);

                OfflineStageSimulator.Result result = simulator.Simulate(stage, totalDps: 50f, budget: 60f);

                if (!result.Success)
                {
                    throw new Exception("SpawnInterval이 전부 0인 스테이지에서 시뮬레이션이 실패함(수정 전 버그 재현)");
                }

                if (result.TotalMonstersKilled <= 0)
                {
                    throw new Exception($"TotalMonstersKilled={result.TotalMonstersKilled}(기대: 0 초과)");
                }
            }
            finally
            {
                if (prefab != null) UnityEngine.Object.DestroyImmediate(prefab);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (baseStats != null) UnityEngine.Object.DestroyImmediate(baseStats);
            }
        }

        /// <summary>
        /// SpawnWithTactics=true/false가 섞이고 SpawnInterval도 제각각인 배열이, 전부 SpawnInterval=0
        /// (SpawnWithTactics도 전부 false)인 기준 스테이지와 정확히 같은 결과를 내는지 확인한다
        /// (GitHub 이슈 #27 - "SpawnWithTactics=true/false 혼합 배열에서도 순서와 시간이 일치함").
        /// 실전은 두 플래그 조합 모두 웨이브를 즉시 스폰하므로, 오프라인 결과도 이 조합에 전혀
        /// 영향받지 않아야 한다.
        /// </summary>
        private static void CheckOfflineStageSimulatorMixedSpawnWithTacticsConsistent()
        {
            var simulator = new OfflineStageSimulator(null, null, 1f);
            StageSO mixedStage = null;
            StageSO baselineStage = null;
            GameObject prefabMixed = null;
            GameObject prefabBaseline = null;
            CharacterStatsSO baseStatsMixed = null;
            CharacterStatsSO baseStatsBaseline = null;

            try
            {
                mixedStage = CreateOfflineTestStage(
                    new[] { 0.001f, 500f, 0f, 999999f },
                    new[] { true, false, true, false },
                    out prefabMixed, out baseStatsMixed);

                baselineStage = CreateOfflineTestStage(
                    new[] { 0f, 0f, 0f, 0f },
                    new[] { false, false, false, false },
                    out prefabBaseline, out baseStatsBaseline);

                OfflineStageSimulator.Result mixedResult = simulator.Simulate(mixedStage, totalDps: 80f, budget: 7200f);
                OfflineStageSimulator.Result baselineResult = simulator.Simulate(baselineStage, totalDps: 80f, budget: 7200f);

                if (!mixedResult.Success || !baselineResult.Success)
                {
                    throw new Exception($"두 시뮬레이션 모두 성공해야 함 - mixed.Success={mixedResult.Success}, baseline.Success={baselineResult.Success}");
                }

                if (mixedResult.TotalMonstersKilled != baselineResult.TotalMonstersKilled || mixedResult.TimesCleared != baselineResult.TimesCleared)
                {
                    throw new Exception($"SpawnWithTactics/SpawnInterval 혼합 배열 결과가 기준과 다름 - mixed(killed={mixedResult.TotalMonstersKilled}, cleared={mixedResult.TimesCleared}) vs baseline(killed={baselineResult.TotalMonstersKilled}, cleared={baselineResult.TimesCleared})");
                }
            }
            finally
            {
                if (prefabMixed != null) UnityEngine.Object.DestroyImmediate(prefabMixed);
                if (prefabBaseline != null) UnityEngine.Object.DestroyImmediate(prefabBaseline);
                if (mixedStage != null) UnityEngine.Object.DestroyImmediate(mixedStage);
                if (baselineStage != null) UnityEngine.Object.DestroyImmediate(baselineStage);
                if (baseStatsMixed != null) UnityEngine.Object.DestroyImmediate(baseStatsMixed);
                if (baseStatsBaseline != null) UnityEngine.Object.DestroyImmediate(baseStatsBaseline);
            }
        }

        /// <summary>
        /// SaveService의 4개 컬렉션 더티 플래그를 전부 켜고 캐시를 구분 가능한 sentinel 값으로
        /// 채운 뒤 반환한다(GitHub 이슈 #28 재현 절차와 동일한 "컬렉션은 바뀌었지만 재구축 전"
        /// 상태). _isDirty는 의도적으로 건드리지 않는다 - 호출부가 안전 요구에 맞춰 별도로 설정한다.
        /// </summary>
        private static void SeedSaveServiceStaleDirtySnapshots(SaveService saveService, string sentinel)
        {
            SetPrivateFieldOnPlainObject(saveService, "_inventoryJson", sentinel);
            SetPrivateFieldOnPlainObject(saveService, "_soldierRosterJson", sentinel);
            SetPrivateFieldOnPlainObject(saveService, "_skillLevelsJson", sentinel);
            SetPrivateFieldOnPlainObject(saveService, "_skillCountsJson", sentinel);
            SetPrivateFieldOnPlainObject(saveService, "_isInventorySnapshotDirty", true);
            SetPrivateFieldOnPlainObject(saveService, "_isSoldierRosterSnapshotDirty", true);
            SetPrivateFieldOnPlainObject(saveService, "_isSkillLevelsSnapshotDirty", true);
            SetPrivateFieldOnPlainObject(saveService, "_isSkillCountsSnapshotDirty", true);
        }

        /// <summary>
        /// GitHub 이슈 #28 재현 절차의 핵심 - "컬렉션은 바뀌었지만 Tick 전" 상태에서
        /// FlushPendingChanges()를 호출하면 4개 캐시가 전부 sentinel에서 실제로 재구축되고,
        /// 4개 스냅샷 더티 플래그도 전부 꺼지는지 확인한다. _isDirty는 의도적으로 false로
        /// 둬(실제 코드에서는 MarkDirty()와 항상 함께 세워지지만, 이 검사는 오직 "재구축이
        /// 일어나는가"만 격리해서 확인하려는 것이라, false로 묶어두면 아래 Save()가 실행되지
        /// 않아 실제 PlayerPrefs가 전혀 위험해지지 않는다 - Save() 자체의 안전한 검증은
        /// CheckSaveServiceFlushActuallyPersistsSafely가 별도로 맡는다).
        /// InventoryService/SoldierRosterService/SkillService가 전부 빈 상태이므로 재구축된
        /// JSON은 빈 컬렉션 블롭이 된다 - "정확히 어떤 값인지"가 아니라 "sentinel에서 실제로
        /// 바뀌었는가"만 확인한다(수정 전에는 Save()가 이 값들을 전혀 재구축하지 않아 sentinel이
        /// 그대로 남아있었다).
        /// </summary>
        private static void CheckSaveServiceFlushRebuildsAllDirtySnapshots()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            var equippedGear = new EquippedGearService(events);
            var soldierRoster = new SoldierRosterService(events);
            var soldierDeployment = new SoldierDeploymentService(events, soldierRoster, null);
            var skill = new SkillService(events);
            var skillLoadout = new SkillLoadoutService(events, skill);
            var squadTactic = new SquadTacticService(events);

            var saveService = new SaveService(
                events, inventory, equippedGear, null, soldierRoster, null, soldierDeployment,
                null, skill, null, skillLoadout, squadTactic);

            const string sentinel = "REGRESSION_CHECK_STALE_CACHE";
            SeedSaveServiceStaleDirtySnapshots(saveService, sentinel);
            SetPrivateFieldOnPlainObject(saveService, "_isDirty", false);

            saveService.FlushPendingChanges();

            string[] jsonFields = { "_inventoryJson", "_soldierRosterJson", "_skillLevelsJson", "_skillCountsJson" };
            string[] dirtyFields = { "_isInventorySnapshotDirty", "_isSoldierRosterSnapshotDirty", "_isSkillLevelsSnapshotDirty", "_isSkillCountsSnapshotDirty" };

            for (int i = 0; i < jsonFields.Length; i++)
            {
                string value = (string)GetPrivateFieldOnPlainObject(saveService, jsonFields[i]);

                if (value == sentinel)
                {
                    throw new Exception($"{jsonFields[i]}이 FlushPendingChanges() 이후에도 sentinel 그대로임 - 재구축되지 않음(GitHub 이슈 #28 재현)");
                }

                bool stillDirty = (bool)GetPrivateFieldOnPlainObject(saveService, dirtyFields[i]);

                if (stillDirty)
                {
                    throw new Exception($"{dirtyFields[i]}가 FlushPendingChanges() 이후에도 true로 남아있음");
                }
            }
        }

        /// <summary>
        /// FlushPendingChanges()가 실제로 Save()(PlayerPrefs 기록)까지 도달해 _isDirty를 정상
        /// 해제하는지 "안전하게" 확인한다(GitHub 이슈 #28 - "저장 성공 뒤에만 _isDirty 해제").
        /// Initialize()로 이 개발 세션의 실제 저장값을 먼저 전부 로드해두고(4개 컬렉션 JSON도
        /// 포함 - 아무 것도 안 건드림) 오직 MarkDirty()(스칼라 필드는 전혀 안 바꿈)로만 _isDirty를
        /// 세우므로, Save()가 실제로 실행돼도 로드된 것과 정확히 같은 값을 도로 쓸 뿐이라 실제
        /// PlayerPrefs 세이브 데이터가 조금도 바뀌지 않는다(LastActiveUnixTime만 "지금"으로
        /// 자연스럽게 갱신되는데, 이는 정상적인 Save() 동작 그 자체다). 골드 키를 앞뒤로 비교해
        /// 실제로 변형이 없었음을 이중 확인한다.
        /// </summary>
        private static void CheckSaveServiceFlushActuallyPersistsSafely()
        {
            var events = new EventBus();
            var saveService = new SaveService(events, null, null, null, null, null, null, null, null, null, null, null);

            FieldInfo goldKeyField = typeof(SaveService).GetField("GoldBigKey", BindingFlags.NonPublic | BindingFlags.Static);
            var goldKey = (string)goldKeyField.GetValue(null);
            string goldBefore = PlayerPrefs.GetString(goldKey, "");

            try
            {
                saveService.Initialize();

                InvokeVoidHandler(saveService, "MarkDirty");

                if (!(bool)GetPrivateFieldOnPlainObject(saveService, "_isDirty"))
                {
                    throw new Exception("MarkDirty() 호출 후 _isDirty가 true가 아님");
                }

                saveService.FlushPendingChanges();

                if ((bool)GetPrivateFieldOnPlainObject(saveService, "_isDirty"))
                {
                    throw new Exception("_isDirty만 세워진 상태에서 FlushPendingChanges()를 호출했는데도 Save()가 실행되지 않음(_isDirty가 여전히 true)");
                }

                string goldAfter = PlayerPrefs.GetString(goldKey, "");

                if (goldAfter != goldBefore)
                {
                    throw new Exception($"Save()가 실행되며 골드 값이 바뀜(before={goldBefore}, after={goldAfter}) - Initialize()로 로드한 값과 다른 값을 씀");
                }
            }
            finally
            {
                saveService.Shutdown();
            }
        }

        /// <summary>
        /// GitHub 이슈 #28의 실제 회귀 지점 - GameBootstrapper.OnApplicationPause/OnApplicationQuit이
        /// SaveService.Save()를 직접 부르지 않고 FlushPendingChanges()를 통해서만 호출하는지,
        /// 실제 메서드를 리플렉션으로 직접 실행해 확인한다. GameObject를 비활성 상태로 유지해
        /// GameBootstrapper.Awake()(전체 게임 부트스트랩)가 실행되지 않게 하고, 정적 Services를
        /// 이 검사만의 격리된 SaveService를 담은 합성 ServiceLocator로 임시 교체한다(WithNullServices와
        /// 같은 안전한 백업/복원 패턴). SaveService의 _isDirty를 false로 고정해뒀으므로(위
        /// CheckSaveServiceFlushRebuildsAllDirtySnapshots와 동일한 이유) 이 경로를 타도 실제
        /// PlayerPrefs.Save()는 절대 실행되지 않는다 - 오직 "4개 스냅샷이 재구축되는가"만으로
        /// FlushPendingChanges() 경로를 탔는지 확인한다(수정 전이었다면 Save()만 불려 캐시가
        /// sentinel 그대로 남았을 것).
        /// </summary>
        private static void CheckGameBootstrapperLifecycleUsesFlushPendingChanges()
        {
            var events = new EventBus();
            var inventory = new InventoryService(events);
            var equippedGear = new EquippedGearService(events);
            var soldierRoster = new SoldierRosterService(events);
            var soldierDeployment = new SoldierDeploymentService(events, soldierRoster, null);
            var skill = new SkillService(events);
            var skillLoadout = new SkillLoadoutService(events, skill);
            var squadTactic = new SquadTacticService(events);

            var saveService = new SaveService(
                events, inventory, equippedGear, null, soldierRoster, null, soldierDeployment,
                null, skill, null, skillLoadout, squadTactic);

            const string sentinel = "REGRESSION_CHECK_LIFECYCLE_STALE_CACHE";
            SeedSaveServiceStaleDirtySnapshots(saveService, sentinel);
            SetPrivateFieldOnPlainObject(saveService, "_isDirty", false);

            var locator = new ServiceLocator();
            locator.Register(saveService);

            PropertyInfo servicesProperty = typeof(GameBootstrapper).GetProperty("Services", BindingFlags.Public | BindingFlags.Static);

            if (servicesProperty == null)
            {
                throw new Exception("GameBootstrapper.Services 프로퍼티를 찾지 못함 - 이름이 바뀌었는지 확인");
            }

            object originalServices = servicesProperty.GetValue(null);
            GameObject go = null;

            try
            {
                servicesProperty.SetValue(null, locator);

                go = new GameObject("RegressionCheck_GameBootstrapper_Lifecycle");
                go.SetActive(false);
                GameBootstrapper bootstrapper = go.AddComponent<GameBootstrapper>();

                MethodInfo pauseMethod = typeof(GameBootstrapper).GetMethod("OnApplicationPause", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo quitMethod = typeof(GameBootstrapper).GetMethod("OnApplicationQuit", BindingFlags.NonPublic | BindingFlags.Instance);

                if (pauseMethod == null || quitMethod == null)
                {
                    throw new Exception("GameBootstrapper.OnApplicationPause/OnApplicationQuit 메서드를 찾지 못함 - 이름이 바뀌었는지 확인");
                }

                pauseMethod.Invoke(bootstrapper, new object[] { true });

                if ((string)GetPrivateFieldOnPlainObject(saveService, "_inventoryJson") == sentinel)
                {
                    throw new Exception("OnApplicationPause(true) 이후에도 _inventoryJson이 sentinel 그대로임 - Save()를 직접 불러 재구축을 건너뛴 것으로 보임(GitHub 이슈 #28 재현)");
                }

                // OnApplicationQuit도 동일 경로를 타는지 별도로 확인 - sentinel을 다시 채운다.
                SeedSaveServiceStaleDirtySnapshots(saveService, sentinel);
                SetPrivateFieldOnPlainObject(saveService, "_isDirty", false);

                quitMethod.Invoke(bootstrapper, null);

                if ((string)GetPrivateFieldOnPlainObject(saveService, "_soldierRosterJson") == sentinel)
                {
                    throw new Exception("OnApplicationQuit() 이후에도 _soldierRosterJson이 sentinel 그대로임 - Save()를 직접 불러 재구축을 건너뛴 것으로 보임(GitHub 이슈 #28 재현)");
                }
            }
            finally
            {
                servicesProperty.SetValue(null, originalServices);

                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }

                saveService.Shutdown();
            }
        }

        /// <summary>
        /// 100% 확률 골드(minGold=maxGold=100)/100% 확률 장비 드롭을 가진 합성 일반 스테이지를
        /// 만들고, StageChangedEvent를 발행해 LootDropper의 _currentStage로 추적시킨다
        /// (GitHub 이슈 #29 검사 전용 헬퍼) - 이슈의 실제 재현 절차와 동일하게 "던전 진입 전
        /// 마지막 일반 스테이지"를 확률 0/1이 아닌 결정론적 값으로 구성해, 우연한 난수 결과에
        /// 기대지 않고 매번 같은 결과를 확인할 수 있게 한다.
        /// </summary>
        private static StageSO CreateGuaranteedDropStage(EventBus events, out EquipmentSO equipment)
        {
            equipment = ScriptableObject.CreateInstance<EquipmentSO>();

            var dropEntry = new EquipmentDropEntry();
            SetPrivateFieldOnPlainObject(dropEntry, "equipment", equipment);
            SetPrivateFieldOnPlainObject(dropEntry, "dropChance", 1f);

            var stage = ScriptableObject.CreateInstance<StageSO>();
            SetPrivateFieldOnPlainObject(stage, "chapter", 6);
            SetPrivateFieldOnPlainObject(stage, "stageNumber", 1);
            SetPrivateFieldOnPlainObject(stage, "equipmentDrops", new[] { dropEntry });

            events.Publish(new StageChangedEvent(6, 1, true));

            return stage;
        }

        /// <summary>
        /// GitHub 이슈 #29 재현 절차 - 던전 진입 전 마지막 일반 스테이지(100% 골드+장비 드롭)를
        /// 추적해둔 상태에서, StageController.IsOverlayActive가 true인 동안 "던전 보스"
        /// (MonsterLootProvider 보유)가 100번 죽어도 일반 GoldEarnedEvent/ItemDroppedEvent가
        /// 전혀 발행되지 않는지 확인한다(수정 전에는 100번 모두 골드가 추가 발행됐다 - 이슈의
        /// 실제 로그와 동일한 시나리오).
        /// </summary>
        private static void CheckLootDropperSkipsNormalDropsDuringOverlay()
        {
            var events = new EventBus();
            StageCatalogSO catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            StageSO stage = CreateGuaranteedDropStage(events, out EquipmentSO equipment);
            SetPrivateFieldOnPlainObject(catalog, "stages", new[] { stage });

            GameObject stageControllerGo = null;
            GameObject dungeonBossGo = null;
            MonsterLootSO monsterLoot = null;

            int goldEvents = 0;
            int equipmentEvents = 0;
            Action<GoldEarnedEvent> onGold = _ => goldEvents++;
            Action<ItemDroppedEvent> onItem = _ => equipmentEvents++;

            LootDropper dropper = null;

            try
            {
                // LootDropper의 생성자가 events.Subscribe<StageChangedEvent>를 걸기 전에 이미
                // 발행된 CreateGuaranteedDropStage의 StageChangedEvent는 못 받으므로, catalog를
                // 만든 뒤 LootDropper를 생성하고 나서 다시 한 번 발행해 _currentStage를 확정한다.
                stageControllerGo = new GameObject("RegressionCheck_LootDropper_StageController");
                stageControllerGo.SetActive(false);
                StageController stageController = stageControllerGo.AddComponent<StageController>();

                FieldInfo isOverlayActiveBackingField = typeof(StageController).GetField(
                    "<IsOverlayActive>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

                if (isOverlayActiveBackingField == null)
                {
                    throw new Exception("StageController.IsOverlayActive의 백킹 필드를 찾지 못함 - 자동 프로퍼티 구현이 바뀌었는지 확인");
                }

                isOverlayActiveBackingField.SetValue(stageController, true);

                dropper = new LootDropper(events, catalog, null, stageController);
                events.Publish(new StageChangedEvent(6, 1, true));

                events.Subscribe(onGold);
                events.Subscribe(onItem);

                monsterLoot = ScriptableObject.CreateInstance<MonsterLootSO>();
                SetPrivateFieldOnPlainObject(monsterLoot, "minGold", 100);
                SetPrivateFieldOnPlainObject(monsterLoot, "maxGold", 100);
                SetPrivateFieldOnPlainObject(monsterLoot, "dropChance", 1f);

                dungeonBossGo = new GameObject("RegressionCheck_LootDropper_DungeonBoss");
                MonsterLootProvider provider = dungeonBossGo.AddComponent<MonsterLootProvider>();
                SetPrivateField(provider, "loot", monsterLoot);

                for (int i = 0; i < 100; i++)
                {
                    events.Publish(new CharacterDiedEvent(dungeonBossGo));
                }

                if (goldEvents != 0)
                {
                    throw new Exception($"IsOverlayActive=true인데도 던전 보스 사망 100회가 GoldEarnedEvent를 {goldEvents}회 발행함(기대=0, GitHub 이슈 #29 재현)");
                }

                if (equipmentEvents != 0)
                {
                    throw new Exception($"IsOverlayActive=true인데도 던전 보스 사망 100회가 ItemDroppedEvent를 {equipmentEvents}회 발행함(기대=0)");
                }
            }
            finally
            {
                events.Unsubscribe(onGold);
                events.Unsubscribe(onItem);
                dropper?.Dispose();

                if (dungeonBossGo != null) UnityEngine.Object.DestroyImmediate(dungeonBossGo);
                if (stageControllerGo != null) UnityEngine.Object.DestroyImmediate(stageControllerGo);
                if (monsterLoot != null) UnityEngine.Object.DestroyImmediate(monsterLoot);
                if (equipment != null) UnityEngine.Object.DestroyImmediate(equipment);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        /// <summary>
        /// GitHub 이슈 #29 완료 조건 - "던전 종료 후 일반 스테이지 몬스터 드롭은 정상 복구됨".
        /// IsOverlayActive가 false인 평범한 상황에서는 MonsterLootProvider를 가진 몬스터가 죽으면
        /// 여전히 정상적으로 골드/장비 이벤트가 발행되는지 확인한다 - 이번 수정이 정상 드롭 자체를
        /// 망가뜨리지 않았는지 확인하는 회귀 방지 검사.
        /// </summary>
        private static void CheckLootDropperResumesNormalDropsAfterOverlay()
        {
            var events = new EventBus();
            StageCatalogSO catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            StageSO stage = CreateGuaranteedDropStage(events, out EquipmentSO equipment);
            SetPrivateFieldOnPlainObject(catalog, "stages", new[] { stage });

            GameObject stageControllerGo = null;
            GameObject monsterGo = null;
            MonsterLootSO monsterLoot = null;

            int goldEvents = 0;
            int equipmentEvents = 0;
            Action<GoldEarnedEvent> onGold = _ => goldEvents++;
            Action<ItemDroppedEvent> onItem = _ => equipmentEvents++;

            LootDropper dropper = null;

            try
            {
                stageControllerGo = new GameObject("RegressionCheck_LootDropper_StageController_Normal");
                stageControllerGo.SetActive(false);
                StageController stageController = stageControllerGo.AddComponent<StageController>();
                // IsOverlayActive는 자동 프로퍼티 기본값(false) 그대로 - 평범한 실전투 상황.

                dropper = new LootDropper(events, catalog, null, stageController);
                events.Publish(new StageChangedEvent(6, 1, true));

                events.Subscribe(onGold);
                events.Subscribe(onItem);

                monsterLoot = ScriptableObject.CreateInstance<MonsterLootSO>();
                SetPrivateFieldOnPlainObject(monsterLoot, "minGold", 100);
                SetPrivateFieldOnPlainObject(monsterLoot, "maxGold", 100);
                SetPrivateFieldOnPlainObject(monsterLoot, "dropChance", 1f);

                monsterGo = new GameObject("RegressionCheck_LootDropper_NormalMonster");
                MonsterLootProvider provider = monsterGo.AddComponent<MonsterLootProvider>();
                SetPrivateField(provider, "loot", monsterLoot);

                events.Publish(new CharacterDiedEvent(monsterGo));

                if (goldEvents != 1)
                {
                    throw new Exception($"IsOverlayActive=false인 평범한 처치인데 GoldEarnedEvent가 {goldEvents}회 발행됨(기대=1) - 정상 드롭 자체가 망가짐");
                }

                if (equipmentEvents != 1)
                {
                    throw new Exception($"IsOverlayActive=false인 평범한 처치인데 ItemDroppedEvent가 {equipmentEvents}회 발행됨(기대=1, 100% 확률 드롭 테이블) - 정상 드롭 자체가 망가짐");
                }
            }
            finally
            {
                events.Unsubscribe(onGold);
                events.Unsubscribe(onItem);
                dropper?.Dispose();

                if (monsterGo != null) UnityEngine.Object.DestroyImmediate(monsterGo);
                if (stageControllerGo != null) UnityEngine.Object.DestroyImmediate(stageControllerGo);
                if (monsterLoot != null) UnityEngine.Object.DestroyImmediate(monsterLoot);
                if (equipment != null) UnityEngine.Object.DestroyImmediate(equipment);
                if (stage != null) UnityEngine.Object.DestroyImmediate(stage);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
            }
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
