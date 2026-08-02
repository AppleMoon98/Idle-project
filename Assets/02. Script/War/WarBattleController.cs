using System;
using Character;
using Core;
using Stage;
using Stage.Events;
using UnityEngine;
using War.Events;
using War.Objectives;

namespace War
{
    /// <summary>
    /// 챕터 클라이맥스(스테이지 번호 == climaxStageNumber) 진입/이탈을 감지해, 챕터별로
    /// 배정된 War 목표(IWarObjective) 하나를 활성화하고 완료/실패를 기존 Stage 이벤트로
    /// 변환한다. Stage 도메인 코드는 전혀 수정하지 않는다 — 완료 시 StageClearedEvent를
    /// 직접 발행해 StageProgression이 평소처럼 반응하게 하고, 실패 시 플레이어 Health에
    /// 즉사 데미지를 줘 기존 사망→후퇴 파이프라인을 그대로 재사용한다.
    /// </summary>
    public sealed class WarBattleController : MonoBehaviour, ITickable
    {
        /// <summary>
        /// 챕터 하나에 배정된 War 목표 종류.
        /// </summary>
        [Serializable]
        private sealed class ChapterObjectiveEntry
        {
            [SerializeField]
            private int chapter;

            [SerializeField]
            private WarObjectiveType type;

            public int Chapter => chapter;
            public WarObjectiveType Type => type;
        }

        /// <summary>
        /// 수하물 보호 실패 등으로 스테이지를 후퇴시킬 때 플레이어에게 주는 데미지량.
        /// 정확한 수치는 의미 없고, 현재 체력과 무관하게 확실히 사망시키기 위한 값이다.
        /// </summary>
        private const float LethalDamageAmount = 999999f;

        [SerializeField]
        private int climaxStageNumber = 40;

        /// <summary>
        /// 클라이맥스 진입 후 실제 목표 판정을 시작하기까지의 워밍업(경고 카운트다운) 시간.
        /// </summary>
        [SerializeField]
        private float climaxWarmupDuration = 3f;

        [SerializeField]
        private ChapterObjectiveEntry[] chapterObjectives;

        [SerializeField]
        private StageCatalogSO stageCatalog;

        [SerializeField]
        private Health playerHealth;

        [SerializeField]
        private GameObject warZoneRoot;

        [SerializeField]
        private AnnihilationObjective annihilationObjective;

        [SerializeField]
        private StructureCaptureObjective structureCaptureObjective;

        [SerializeField]
        private BossDefeatObjective bossDefeatObjective;

        [SerializeField]
        private CargoProtectionObjective cargoProtectionObjective;

        private IWarObjective _activeObjective;
        private WarObjectiveType _activeType;
        private int _currentChapter;
        private int _currentStageNumber;
        private bool _isActive;
        private float _lastPublishedProgress;
        private bool _isWarmingUp;
        private float _warmupRemaining;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Register(this);
            }
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out GameTicker ticker))
            {
                ticker.Unregister(this);
            }
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            _currentChapter = evt.Chapter;
            _currentStageNumber = evt.StageNumber;

            DeactivateAll();
            _isWarmingUp = false;

            bool isClimax = evt.StageNumber == climaxStageNumber;

            if (warZoneRoot != null)
            {
                warZoneRoot.SetActive(isClimax);
            }

            if (!isClimax)
            {
                _isActive = false;
                GameBootstrapper.Events?.Publish(new WarClimaxStateChangedEvent(false, _activeType, evt.Chapter));
                return;
            }

            // 목표는 즉시 활성화하지 않고, climaxWarmupDuration만큼 경고 카운트다운을 먼저 보여준다.
            // 실제 활성화(ActivateObjective)는 Tick()에서 워밍업이 끝났을 때 수행한다.
            _activeType = ResolveObjectiveType(evt.Chapter);
            _isActive = false;
            _isWarmingUp = true;
            _warmupRemaining = climaxWarmupDuration;
            GameBootstrapper.Events?.Publish(new WarClimaxWarmupStartedEvent(_activeType, evt.Chapter, climaxWarmupDuration));
        }

        void ITickable.Tick(float deltaTime)
        {
            if (_isWarmingUp)
            {
                _warmupRemaining -= deltaTime;

                if (_warmupRemaining > 0f)
                {
                    return;
                }

                _isWarmingUp = false;
                _activeObjective = ActivateObjective(_activeType);
                _isActive = _activeObjective != null;
                _lastPublishedProgress = -1f;
                GameBootstrapper.Events?.Publish(new WarClimaxStateChangedEvent(true, _activeType, _currentChapter));
                return;
            }

            if (!_isActive || _activeObjective == null)
            {
                return;
            }

            if (_activeObjective.HasFailed)
            {
                _isActive = false;
                playerHealth.TakeDamage(LethalDamageAmount);
                return;
            }

            float progress = _activeObjective.Progress01;

            if (!Mathf.Approximately(progress, _lastPublishedProgress))
            {
                _lastPublishedProgress = progress;
                GameBootstrapper.Events?.Publish(new WarObjectiveProgressChangedEvent(progress));
            }

            // Annihilation은 StageProgressTracker의 자연스러운 클리어를 그대로 두고,
            // 나머지 세 목표만 조기 클리어로 StageClearedEvent를 직접 발행한다.
            if (_activeType != WarObjectiveType.Annihilation && _activeObjective.IsCompleted)
            {
                _isActive = false;
                StageSO clearedStage = stageCatalog.Find(_currentChapter, _currentStageNumber);

                if (clearedStage != null)
                {
                    GameBootstrapper.Events?.Publish(new StageClearedEvent(clearedStage));
                }
            }
        }

        private WarObjectiveType ResolveObjectiveType(int chapter)
        {
            if (chapterObjectives != null)
            {
                foreach (ChapterObjectiveEntry entry in chapterObjectives)
                {
                    if (entry.Chapter == chapter)
                    {
                        return entry.Type;
                    }
                }
            }

            return WarObjectiveType.Annihilation;
        }

        private IWarObjective ActivateObjective(WarObjectiveType type)
        {
            MonoBehaviour target = type switch
            {
                WarObjectiveType.StructureCapture => structureCaptureObjective,
                WarObjectiveType.BossDefeat => bossDefeatObjective,
                WarObjectiveType.CargoProtection => cargoProtectionObjective,
                _ => annihilationObjective
            };

            if (target == null)
            {
                return null;
            }

            target.gameObject.SetActive(true);
            var objective = (IWarObjective)target;
            objective.ResetForNewAttempt();
            return objective;
        }

        private void DeactivateAll()
        {
            SetActiveIfAssigned(annihilationObjective);
            SetActiveIfAssigned(structureCaptureObjective);
            SetActiveIfAssigned(bossDefeatObjective);
            SetActiveIfAssigned(cargoProtectionObjective);

            _activeObjective = null;
        }

        private static void SetActiveIfAssigned(MonoBehaviour target)
        {
            if (target != null)
            {
                target.gameObject.SetActive(false);
            }
        }
    }
}
