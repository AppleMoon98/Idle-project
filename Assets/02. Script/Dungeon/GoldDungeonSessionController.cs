using System.Collections.Generic;
using Character;
using Character.Events;
using Core;
using Dungeon.Events;
using Loot.Events;
using Managers;
using Rank;
using Stage;
using UI.Events;
using UnityEngine;

namespace Dungeon
{
    /// <summary>
    /// 골드 던전 한 판의 진행을 관리한다. 화면 안 랜덤 위치에 비공격 몬스터를 스폰하고, 처치할
    /// 때마다 기준 스테이지 클리어 총 골드(config.CalculateGoldRange, Enter 시점에 한 번만 계산해
    /// 캐싱) 범위 안에서 랜덤 골드를 지급하며, 전멸하거나 제한시간이 끝나면 원래 스테이지로
    /// 복귀시킨다. StageSO/StageProgression 파이프라인은 전혀 건드리지 않는다 —
    /// StageController.PauseForOverlay/ResumeAfterOverlay로 기존 스테이지를 잠깐 숨기고 되돌릴
    /// 뿐이다. 죽은 몬스터 자신의 반납은 PoolReleaseOnDeath가 처리하므로, 여기서는 시간 종료로
    /// 남아있는 몬스터만 직접 반납한다.
    /// </summary>
    public sealed class GoldDungeonSessionController : MonoBehaviour, ITickable
    {
        [SerializeField]
        private GoldDungeonConfigSO config;

        [SerializeField]
        private StageController stageController;

        private readonly HashSet<GameObject> _aliveMonsters = new();

        private int _stageNumber;
        private int _highestClearedIndex;
        private int _goldPerKillMin;
        private int _goldPerKillMax;
        private StageSO _referenceStage;
        private int _totalGoldEarned;
        private float _remainingTime;
        private bool _isActive;

        /// <summary>
        /// 현재 던전이 진행 중인지 여부.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// UI(GoldDungeonEntryUI)가 스테퍼의 최대 선택 가능 단계를 읽어가기 위한 접근자. "카탈로그에
        /// 존재하는 콘텐츠 양"이 아니라 "플레이어가 실제로 클리어한 챕터"를 기준으로 삼는다 — 아직
        /// 클리어하지 못한 챕터의 몹 체력을 미리 farming하는 것을 막기 위함. 랭크 서비스가 아직 없으면
        /// (극초반 등) 최소 1단계는 항상 선택 가능해야 하므로 1로 대체한다.
        /// </summary>
        public int MaxStageNumber
        {
            get
            {
                if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService))
                {
                    return Mathf.Max(1, rankService.HighestClearedChapter);
                }

                return 1;
            }
        }

        /// <summary>
        /// stageNumber 단계의 입장 조건 — 제거됨(항상 true). requiredStage는 UI 호환을 위해 계속
        /// 채워 반환하지만(계산 자체는 그대로 유지), 반환값 자체가 항상 true라 UI의 안내 메시지
        /// 분기는 더 이상 실행되지 않는다.
        /// </summary>
        public bool IsStageUnlocked(int stageNumber, out StageSO requiredStage)
        {
            requiredStage = null;

            if (config != null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                int maxStageNumber = Mathf.Max(1, rankService.HighestClearedChapter);
                requiredStage = config.GetReferenceStage(stageNumber, rankService.HighestClearedIndex, maxStageNumber);
            }

            return true;
        }

        /// <summary>
        /// 골드 던전을 시작한다. stageNumber는 보상 계산에 쓰인다. 이미 진행 중이거나(자기 자신)
        /// 다른 오버레이(다른 던전, 랭크 승급전 등)가 이미 켜져 있으면(stageController.
        /// IsOverlayActive) 무시하고 토스트로 안내한다 — 이게 없으면 던전 안에서 다른 던전 팝업의
        /// 입장 버튼을 눌러 중복 진입할 수 있었다(실사용 중 발견). MaxStageNumber(플레이어가 실제로
        /// 클리어한 챕터 기준)로 즉시 정규화해서 저장하므로, UI가 실수로(또는 스테퍼 상한 설정 전에)
        /// 아직 클리어하지 못한 단계를 넘겨도 몹 체력과 골드 보상이 항상 같은 유효 단계를 기준으로
        /// 계산된다 — 체력은 진행도에서 멈추는데 보상만 계속 커지는 문제를 여기서 막는다.
        /// </summary>
        public void Enter(int stageNumber)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive))
            {
                GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("이미 던전에 입장중입니다."));
                return;
            }

            if (config == null || config.MonsterPrefab == null)
            {
                return;
            }

            _isActive = true;
            _stageNumber = Mathf.Clamp(stageNumber, 1, MaxStageNumber);
            _remainingTime = config.TimeLimitSeconds;

            _highestClearedIndex = GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService)
                ? rankService.HighestClearedIndex
                : -1;

            config.CalculateGoldRange(_stageNumber, _highestClearedIndex, MaxStageNumber, out _goldPerKillMin, out _goldPerKillMax);
            _referenceStage = config.GetReferenceStage(_stageNumber, _highestClearedIndex, MaxStageNumber);
            _totalGoldEarned = 0;

            stageController?.PauseForOverlay($"골드 던전 {_stageNumber}층");
            stageController?.ResetCombatantsForRetry();
            stageController?.ResetSkillCooldowns();

            SpawnMonsters();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Register(this);

            GameBootstrapper.Events?.Publish(new GoldDungeonSessionStartedEvent(_stageNumber, _aliveMonsters.Count, _remainingTime));
            GameBootstrapper.Events?.Publish(new GoldDungeonProgressChangedEvent(_aliveMonsters.Count));
        }

        private void SpawnMonsters()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(config.MonsterPrefab, config.MonsterCount, config.MonsterCount);

            for (int i = 0; i < config.MonsterCount; i++)
            {
                Vector3 spawnPosition = DungeonSpawnUtility.RandomWithinPlayAreaPosition(config.SpawnViewportMargin);
                GameObject instance = pool.Get(config.MonsterPrefab, spawnPosition, Quaternion.identity);

                if (instance.TryGetComponent(out StageMonsterScaler scaler))
                {
                    scaler.ApplyScale(config.CalculateMonsterStatMultiplier(_stageNumber, _highestClearedIndex, MaxStageNumber));
                }

                _aliveMonsters.Add(instance);
            }
        }

        void ITickable.Tick(float deltaTime)
        {
            if (!_isActive)
            {
                return;
            }

            _remainingTime -= deltaTime;

            if (_remainingTime <= 0f)
            {
                EndSession(cleared: false);
            }
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!_aliveMonsters.Remove(evt.Character))
            {
                return;
            }

            int amount = Random.Range(_goldPerKillMin, _goldPerKillMax + 1);
            _totalGoldEarned += amount;
            GameBootstrapper.Events?.Publish(new GoldEarnedEvent(amount));
            GameBootstrapper.Events?.Publish(new GoldDungeonProgressChangedEvent(_aliveMonsters.Count));

            if (_aliveMonsters.Count <= 0)
            {
                EndSession(cleared: true);
            }
        }

        private void EndSession(bool cleared)
        {
            _isActive = false;

            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            TickerRegistration.Unregister(this);

            if (cleared)
            {
                PublishClearSummary();
            }

            ReleaseRemainingMonsters();

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new GoldDungeonSessionEndedEvent(cleared));
        }

        /// <summary>
        /// 전멸 클리어 시(시간 초과 실패는 제외) 기준 스테이지/소요시간/획득 골드를
        /// GoldDungeonClearedEvent로 발행한다 - 실제 화면 표시(팝업)는 UI.GoldDungeonClearPopupUI가
        /// 이 이벤트를 구독해 담당한다.
        /// </summary>
        private void PublishClearSummary()
        {
            float elapsed = Mathf.Max(0f, config.TimeLimitSeconds - _remainingTime);
            int chapter = _referenceStage != null ? _referenceStage.Chapter : 0;
            int stageNumber = _referenceStage != null ? _referenceStage.StageNumber : 0;

            GameBootstrapper.Events?.Publish(new GoldDungeonClearedEvent(chapter, stageNumber, elapsed, _totalGoldEarned));
        }

        private void ReleaseRemainingMonsters()
        {
            if (DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                foreach (GameObject monster in _aliveMonsters)
                {
                    if (monster != null)
                    {
                        pool.Release(monster);
                    }
                }
            }

            _aliveMonsters.Clear();
        }

        private void OnDestroy()
        {
            if (_isActive)
            {
                EndSession(cleared: false);
            }
        }
    }
}
