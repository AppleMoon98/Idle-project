using Character;
using Character.Events;
using Core;
using Dungeon;
using Managers;
using Rank.Events;
using Stage;
using UI.Events;
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
        /// 이미 진행 중이거나(자기 자신) 다른 오버레이(던전 등)가 이미 켜져 있으면(stageController.
        /// IsOverlayActive) 무시한다 — Dungeon.GoldDungeonSessionController.Enter와 동일한 이유
        /// (오버레이 중복 진입 방지). 보스 스폰을 먼저 시도해 성공했을 때만 _isActive/_isFighting을
        /// 켠다(GitHub 이슈 #20) — 이 컨트롤러는 제한시간이 없어(section AY), 스폰 실패 후 상태만
        /// 먼저 켜면 CharacterDiedEvent가 영원히 안 와서 진짜 무한 대기(영구 소프트락)가 된다.
        /// PauseForOverlay()도 스폰 성공을 확인한 뒤에만 호출하므로, 실패 시 롤백할 상태 자체가 없다.
        /// </summary>
        public void Enter(RankSO targetRank)
        {
            if (_isActive || (stageController != null && stageController.IsOverlayActive) || targetRank == null || targetRank.BossPrefab == null)
            {
                return;
            }

            _targetRank = targetRank;

            if (!TrySpawnBoss(out GameObject bossInstance))
            {
                PublishSpawnFailureToast();
                return;
            }

            _isActive = true;
            _bossInstance = bossInstance;

            stageController?.PauseForOverlay();

            BeginFighting();
        }

        /// <summary>
        /// 실패 후 재도전한다. 진행 중이 아니거나 아직 전투 중이면 무시한다. 플레이어/병사 체력뿐
        /// 아니라 위치도 함께 되돌린다(Character.StagePositionResetter, ResumeAfterOverlay가
        /// 클리어/나가기 시점에 쓰는 것과 동일한 두 호출 조합) - 예전엔 죽었을 때만 체력만
        /// Revive했는데, 사망 지점이 보스 스폰 위치와 멀리 떨어져 있으면 재도전 시 플레이어가
        /// 그 자리 그대로 시작해 위치가 초기화되지 않는 것처럼 보였다(실사용 중 발견).
        /// </summary>
        public void Retry()
        {
            if (!_isActive || _isFighting)
            {
                return;
            }

            if (!TrySpawnBoss(out GameObject bossInstance))
            {
                PublishSpawnFailureToast();
                return;
            }

            _bossInstance = bossInstance;

            if (playerTransform != null && playerTransform.TryGetComponent(out StagePositionResetter positionResetter))
            {
                positionResetter.ResetPositions();
                positionResetter.ResetHealth();
            }

            BeginFighting();
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

        /// <summary>
        /// 스폰이 이미 성공적으로 끝난 뒤(_bossInstance가 세팅된 뒤) 실제 전투 시작 북키핑만 한다 -
        /// 스폰 자체는 TrySpawnBoss로 분리돼 Enter/Retry가 성공을 확인한 다음에만 이 메서드를 부른다.
        /// </summary>
        private void BeginFighting()
        {
            _isFighting = true;

            GameBootstrapper.Events?.Subscribe<CharacterDiedEvent>(OnCharacterDied);
            GameBootstrapper.Events?.Publish(new RankPromotionAttemptStartedEvent(_targetRank, _bossInstance));
        }

        /// <summary>
        /// _targetRank.BossPrefab을 스폰한다. PoolManager를 못 구하면 false를 반환하고 아무 상태도
        /// 바꾸지 않는다 - 호출부(Enter/Retry)가 성공을 확인하기 전까지는 _isActive/_isFighting을
        /// 켜지 않으므로, 실패해도 롤백할 게 없다(GitHub 이슈 #20).
        /// </summary>
        private bool TrySpawnBoss(out GameObject instance)
        {
            instance = null;

            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out PoolManager pool))
            {
                return false;
            }

            pool.EnsurePool(_targetRank.BossPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.BossSpawnPosition();
            instance = pool.Get(_targetRank.BossPrefab, spawnPosition, Quaternion.identity);

            return instance != null;
        }

        private static void PublishSpawnFailureToast()
        {
            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("전투 대상을 생성하지 못했습니다. 잠시 후 다시 시도해주세요."));
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
