using System;
using Core;
using Loot.Events;
using Offline.Events;
using Save;
using Stage;
using Stage.Events;
using UnityEngine;

namespace Offline
{
    /// <summary>
    /// 마지막 저장 시각 대비 경과 시간과 "세이브 복원이 완료된 시점의 유효 전투력 스냅샷"을
    /// 조합해 오프라인 보상(골드/장비)을 계산하고 적용하는 오케스트레이터. 실제 전투력 계산은
    /// Offline.OfflineCombatPowerCalculator, 스테이지 반복 시뮬레이션/루팅은
    /// Offline.OfflineStageSimulator에 위임한다 — 이 클래스 자신은 "언제(2단계 실행 타이밍)"와
    /// "결과를 어떻게 게임 상태에 반영하는지(이벤트 발행)"만 책임진다.
    ///
    /// **2단계 실행(CaptureBudget → ApplyCapturedReward)인 이유:** 경과 시간은 GameBootstrapper.
    /// Start()의 가장 첫 줄에서 즉시 확정해야 한다 — 그 뒤에 이어지는 세이브 복원 호출들(Enhancement/
    /// Rank의 RestoreLevel)이 발행하는 이벤트를 SaveService가 구독해 즉시 Save()를 호출하는데, Save()는
    /// LastActiveUnixTime을 항상 "지금"으로 덮어써서 그 뒤에 읽으면 경과 시간이 0이 되어버린다(실제로
    /// 발생했던 버그). 반면 전투력 계산은 "세이브 복원이 완료된 시점의 유효 전투력 스냅샷"을 써야
    /// 정확하므로 — 반드시 모든 성장 시스템의 복원이 끝난 뒤(Start()의 마지막)에 수행해야 한다. 이
    /// 두 요구가 서로 상충해서(경과 시간=제일 먼저, 전투력=제일 나중) 하나의 메서드로 합칠 수 없다.
    /// CaptureBudget()이 경과 시간/반복 스테이지만 먼저 확정해두고, ApplyCapturedReward()가 모든
    /// 복원이 끝난 뒤 그 스냅샷 시점의 실제 전투력(OfflineCombatPowerCalculator)으로 나머지
    /// (OfflineStageSimulator 호출 이후)를 수행한다.
    ///
    /// 오프라인 보상이 반영하는 성장 시스템의 밸런스 정책은 OfflineCombatPowerCalculator의 클래스
    /// doc에 있다.
    ///
    /// **경과 시간 자체는 벽시계만 무조건 신뢰하지 않는다(GitHub 이슈 #71):** 서버가 없어 완전한
    /// 방지는 불가능하지만, CaptureBudget()이 Offline.OfflineElapsedTimeCalculator를 통해 벽시계
    /// 차이와 (있으면) Core.DeviceUptime의 기기 부팅-이후 경과시간 중 더 작은 쪽만 신뢰한다 -
    /// 시계를 반복적으로 미래로 돌려 같은 보상을 재수령하는 걸 막는다. SaveData.LastActiveUnixTime
    /// 자체도 SaveService.Load()가 세이브 파일 직접 편집으로 인한 롤백을 하이워터마크로 정화한
    /// 값이다.
    /// </summary>
    public sealed class OfflineProgressService
    {
        private readonly EventBus _events;
        private readonly SaveService _saveService;
        private readonly OfflineCombatPowerCalculator _combatPowerCalculator;
        private readonly OfflineStageSimulator _stageSimulator;
        private readonly float _maxOfflineSeconds;

        private bool _hasPendingReward;
        private float _pendingElapsedSeconds;
        private float _pendingBudget;
        private StageSO _pendingRepeatStage;

        public OfflineProgressService(
            EventBus events,
            SaveService saveService,
            OfflineCombatPowerCalculator combatPowerCalculator,
            OfflineStageSimulator stageSimulator,
            float maxOfflineSeconds)
        {
            _events = events;
            _saveService = saveService;
            _combatPowerCalculator = combatPowerCalculator;
            _stageSimulator = stageSimulator;
            _maxOfflineSeconds = maxOfflineSeconds;
        }

        /// <summary>
        /// 저장된 마지막 접속 시각을 기준으로 오프라인 인정 시간(budget)과 반복할 스테이지를
        /// 미리 확정해둔다 — GameBootstrapper.Start()의 가장 첫 줄에서, 뒤이은 세이브 복원 호출들이
        /// LastActiveUnixTime을 덮어쓰기 전에 반드시 호출해야 한다(클래스 doc 참고). 인정 시간이
        /// 없거나(최초 실행) 0 이하, 또는 반복할 스테이지가 없으면 대기 상태를 비우고 끝낸다 —
        /// 이 경우 ApplyCapturedReward()는 아무 일도 하지 않는다.
        /// </summary>
        public void CaptureBudget()
        {
            _hasPendingReward = false;

            SaveData save = _saveService.Load();

            if (save.LastActiveUnixTime <= 0)
            {
                return;
            }

            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // GitHub 이슈 #71 - 벽시계 차이만으로 경과 시간을 계산하면 시계를 반복적으로 미래로
            // 돌려 같은 오프라인 보상을 무한히 재수령할 수 있다. Core.DeviceUptime(Android에서만
            // 유효, 그 외 플랫폼은 항상 실패 반환)의 벽시계-무관 신호와 저장된 이전 관측값을
            // Offline.OfflineElapsedTimeCalculator에 넘겨, 둘 중 더 작은 쪽만 신뢰한다.
            long? currentElapsedRealtimeSeconds = DeviceUptime.TryGetElapsedRealtimeSeconds(out long liveElapsedRealtime)
                ? (long?)liveElapsedRealtime
                : null;
            long? previousElapsedRealtimeSeconds = save.LastElapsedRealtimeSeconds > 0
                ? (long?)save.LastElapsedRealtimeSeconds
                : null;

            float elapsedSeconds = OfflineElapsedTimeCalculator.CalculateTrustedElapsedSeconds(
                nowUnix, save.LastActiveUnixTime, currentElapsedRealtimeSeconds, previousElapsedRealtimeSeconds);
            float budget = Mathf.Min(elapsedSeconds, _maxOfflineSeconds);

            if (budget <= 0f)
            {
                return;
            }

            StageSO repeatStage = _stageSimulator.ResolveRepeatStage(save);

            if (repeatStage == null)
            {
                return;
            }

            _pendingElapsedSeconds = elapsedSeconds;
            _pendingBudget = budget;
            _pendingRepeatStage = repeatStage;
            _hasPendingReward = true;
        }

        /// <summary>
        /// CaptureBudget()이 확정해둔 경과 시간/반복 스테이지를 바탕으로, "지금 이 순간"(모든
        /// 세이브 복원이 끝난 뒤)의 유효 전투력 스냅샷으로 실제 보상을 계산해 적용하고 결과
        /// 이벤트를 발행한다. GameBootstrapper.Start()의 가장 마지막(모든 RestoreLevel 호출 뒤)에
        /// 호출해야 한다. CaptureBudget()이 대기 상태를 비워뒀으면(인정 시간 없음 등) 아무 일도
        /// 하지 않는다.
        /// </summary>
        public void ApplyCapturedReward()
        {
            if (!_hasPendingReward)
            {
                return;
            }

            _hasPendingReward = false;

            float elapsedSeconds = _pendingElapsedSeconds;
            float budget = _pendingBudget;
            StageSO repeatStage = _pendingRepeatStage;

            float totalDps = _combatPowerCalculator.ComputeTotalDps();
            OfflineStageSimulator.Result result = _stageSimulator.Simulate(repeatStage, totalDps, budget);

            if (!result.Success)
            {
                return;
            }

            if (result.TotalGold > 0)
            {
                _events.Publish(new GoldEarnedEvent(result.TotalGold));
            }

            foreach (Equipment.EquipmentSO equipment in result.EquipmentEarned)
            {
                _events.Publish(new ItemDroppedEvent(equipment));
            }

            // 반복 모드이므로 역대 최고 기록 자체는 갱신되지 않는다(HighestStageClearedEvent 발행 없음) —
            // 항상 그 기록 스테이지로 복귀시킨다(사망으로 뒤로 밀려 있던 현재 위치는 무시하고, 오프라인은
            // "죽지 않고 최고 기록을 반복 클리어했다"는 낙관적 가정만 반영한다).
            _events.Publish(new StageChangedEvent(repeatStage.Chapter, repeatStage.StageNumber, isBreakthrough: false));

            _events.Publish(new OfflineProgressCalculatedEvent(
                Mathf.Min(elapsedSeconds, _maxOfflineSeconds),
                result.TotalGold,
                result.EquipmentEarned,
                result.TotalMonstersKilled,
                result.TimesCleared,
                repeatStage.Chapter,
                repeatStage.StageNumber));
        }
    }
}
