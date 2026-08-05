using Character;
using Character.Events;
using Core;
using Dungeon;
using Managers;
using Rank.Events;
using Stage;
using UnityEngine;

namespace Rank
{
    /// <summary>
    /// 랭크 승급전 한 판의 진행을 관리한다. targetRank.BossPrefab을 화면 안에 스폰해 실제로
    /// 전투하며, 처치하면 RankService.PromoteToNext를 호출하고 조용히 원래 스테이지로 복귀한다.
    /// 플레이어가 죽으면 원래 스테이지로 자동 복귀하지 않고 "승급 실패" 상태로 멈춰 재도전/나가기
    /// 선택을 기다린다 - Dungeon.StoneDungeonSessionController와 완전히 동일한 구조(제한시간만 없음).
    /// StageSO/StageProgression 파이프라인은 전혀 건드리지 않는다(StageController.PauseForOverlay).
    /// </summary>
    public sealed class RankPromotionBattleController : MonoBehaviour
    {
        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private Transform playerTransform;

        [SerializeField]
        private float spawnViewportMargin = 0.2f;

        private GameObject _bossInstance;
        private RankSO _targetRank;
        private bool _isActive;
        private bool _isFighting;

        /// <summary>
        /// 승급전이 진행 중인지(전투 중이든 실패 화면 대기 중이든) 여부.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// targetRank로의 승급전을 시작한다. targetRank나 그 보스 프리팹이 없으면 무시한다.
        /// </summary>
        public void Enter(RankSO targetRank)
        {
            if (_isActive || targetRank == null || targetRank.BossPrefab == null)
            {
                return;
            }

            _isActive = true;
            _targetRank = targetRank;

            stageController?.PauseForOverlay();

            StartAttempt();
        }

        /// <summary>
        /// 실패 후 재도전한다. 진행 중이 아니거나 아직 전투 중이면 무시한다.
        /// </summary>
        public void Retry()
        {
            if (!_isActive || _isFighting)
            {
                return;
            }

            if (playerTransform != null && playerTransform.TryGetComponent(out Health playerHealth) && playerHealth.IsDead)
            {
                playerHealth.Revive();
            }

            StartAttempt();
        }

        /// <summary>
        /// 실패 후 나가기 — 원래 스테이지로 복귀한다. 전투 중이 아닐 때만 유효하다.
        /// </summary>
        public void ExitToOriginalStage()
        {
            if (!_isActive || _isFighting)
            {
                return;
            }

            _isActive = false;

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new RankPromotionSessionEndedEvent(false));
        }

        private void StartAttempt()
        {
            _isFighting = true;

            SpawnBoss();

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            GameBootstrapper.Events?.Publish(new RankPromotionAttemptStartedEvent(_targetRank));
        }

        private void SpawnBoss()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(_targetRank.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.RandomOnScreenPosition(spawnViewportMargin);
            _bossInstance = pool.Get(_targetRank.BossPrefab, spawnPosition, Quaternion.identity);
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (evt.Character == _bossInstance)
            {
                HandleClear();
                return;
            }

            if (playerTransform != null && evt.Character == playerTransform.gameObject)
            {
                HandleFailure();
            }
        }

        private void HandleClear()
        {
            StopFighting();

            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                rankService.PromoteToNext();
            }

            _isActive = false;
            _bossInstance = null;

            stageController?.ResumeAfterOverlay();

            GameBootstrapper.Events?.Publish(new RankPromotionSessionEndedEvent(true));
        }

        /// <summary>
        /// 플레이어가 죽으면 이번 시도를 실패로 처리하고 "승급 실패" 화면을 띄운다.
        /// </summary>
        private void HandleFailure()
        {
            StopFighting();
            ReleaseBoss();

            GameBootstrapper.Events?.Publish(new RankPromotionAttemptFailedEvent());
        }

        private void StopFighting()
        {
            _isFighting = false;

            GameBootstrapper.Events?.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        private void ReleaseBoss()
        {
            if (_bossInstance != null && GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                pool.Release(_bossInstance);
            }

            _bossInstance = null;
        }

        private void OnDestroy()
        {
            if (_isFighting)
            {
                StopFighting();
                ReleaseBoss();
            }

            if (_isActive)
            {
                _isActive = false;
                stageController?.ResumeAfterOverlay();
            }
        }
    }
}
