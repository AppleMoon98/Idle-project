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
using Loot;
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

            // --- 이슈 #40: 같은 병사(풀 인스턴스)가 다른 부대 슬롯으로 재등록돼도 이전 부대
            // 목록에서 제거되지 않아 한 GameObject가 두 부대에 동시에 소속되던 문제
            // (추적 딕셔너리 불변조건) ---
            Check("SquadMovementSyncService_ReRegisterToDifferentSquad_RemovesFromPreviousSquad",
                CheckSquadMovementSyncServiceReRegistrationInvariant);

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

        private static void AssertApprox(float expected, float actual, string context)
        {
            if (Mathf.Abs(expected - actual) > 0.0005f)
            {
                throw new Exception($"{context}: 기대={expected}, 실제={actual}");
            }
        }
    }
}
