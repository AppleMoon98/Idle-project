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
        private StageController stageController;

        [SerializeField]
        private GameObject warZoneRoot;

        [SerializeField]
        private GameObject structureCaptureProps;

        [SerializeField]
        private GameObject cargoProtectionProps;

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
                SetWarZonePropsActive(WarObjectiveType.Annihilation);
                GameBootstrapper.Events?.Publish(new WarClimaxStateChangedEvent(false, _activeType, evt.Chapter));
                return;
            }

            // 목표는 즉시 활성화하지 않고, climaxWarmupDuration만큼 경고 카운트다운을 먼저 보여준다.
            // 실제 활성화(ActivateObjective)는 Tick()에서 워밍업이 끝났을 때 수행한다. 다만 구조물/
            // 수하물 같은 전장 소품은 워밍업 중에도 이미 보여야 하므로(warZoneRoot와 같은 타이밍),
            // _activeType이 정해지는 즉시 그에 맞는 소품만 SetWarZonePropsActive로 켠다.
            //
            // MonsterSpawner는 StageChangedEvent만으로 즉시 스폰/이동을 시작하므로(War 워밍업과
            // 완전히 무관), 워밍업 카운트다운 중에도 몹이 이미 움직이는 문제가 있었다(실사용 중
            // 발견). StageController.PauseForOverlay()(던전 오버레이가 쓰는 것과 같은 매커니즘)로
            // 스포너 틱을 묶어 카운트다운 동안은 아무것도 스폰/이동하지 않게 한다 - 이 시점은
            // LoadStage()가 아직 스포너를 한 번도 틱하지 않은 채라(StageChangedEvent 발행 직후,
            // 같은 프레임) 이미 스폰된 걸 되돌릴 필요 없이 깔끔하게 막힌다.
            _activeType = ResolveObjectiveType(evt.Chapter);
            SetWarZonePropsActive(_activeType);
            _isActive = false;
            _isWarmingUp = true;
            _warmupRemaining = climaxWarmupDuration;
            stageController?.PauseForOverlay();
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
                stageController?.ResumeAfterOverlay();
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

        /// <summary>
        /// warZoneRoot 밑의 구조물/수하물 소품은 실제로 그 목표가 활성화된 챕터에서만 보여야 한다 -
        /// warZoneRoot.SetActive(true)만으로는 안에 있는 모든 소품(다른 목표용까지)이 한꺼번에
        /// 켜져버린다(실제 발견된 문제: chapterObjectives가 비어 Annihilation으로 배정된 챕터의
        /// 클라이맥스에 들어가도 Cargo가 화면에 나타났고, Player 레이어라 몬스터의 실제 공격
        /// 대상이 되기까지 했다). Annihilation/BossDefeat처럼 전용 소품이 없는 목표는 둘 다 끈다.
        /// </summary>
        private void SetWarZonePropsActive(WarObjectiveType type)
        {
            if (structureCaptureProps != null)
            {
                structureCaptureProps.SetActive(type == WarObjectiveType.StructureCapture);
            }

            if (cargoProtectionProps != null)
            {
                cargoProtectionProps.SetActive(type == WarObjectiveType.CargoProtection);
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
