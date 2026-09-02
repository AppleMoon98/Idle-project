using Character;
using Core;
using Dungeon;
using Managers;
using UI.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// "연습 스테이지" 오버레이 세션. Rank.RankPromotionBattleController와 같은
    /// StageController.PauseForOverlay/ResumeAfterOverlay 기반 오버레이 패턴이지만, 승패 판정이
    /// 없는 순수 샌드백이라 훨씬 단순하다 — CharacterDiedEvent를 구독하지 않고, 오직 UI가
    /// TryEnter()/Exit()를 명시적으로 호출할 때만 상태가 바뀐다(허수아비가 죽어도 자동 종료되지
    /// 않음, 요청 사양).
    /// </summary>
    public sealed class PracticeStageController : MonoBehaviour
    {
        [SerializeField]
        private StageController stageController;

        [SerializeField]
        private GameObject dummyPrefab;

        [SerializeField]
        private MonsterVisualSetSO dummyVisualSet;

        private GameObject _dummyInstance;

        /// <summary>
        /// 연습 스테이지가 진행 중인지 여부.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// 연습 스테이지에 진입한다. 이미 진행 중이거나 다른 오버레이(던전 등)가 이미 켜져 있으면
        /// (stageController.IsOverlayActive) 아무 일도 하지 않고 false를 반환한다 - 호출부(UI)가
        /// 이 경우 던전 중 사용 불가 안내를 띄운다. 허수아비 스폰을 먼저 시도해 성공했을 때만
        /// IsActive/PauseForOverlay를 커밋한다(GitHub 이슈 #20 - Gold/Stone 던전, 랭크 승급전 등
        /// 다른 오버레이 컨트롤러들이 이미 쓰는 "준비→생성→커밋" 순서와 동일). PoolManager 미등록
        /// 등으로 스폰이 실패하면 상태를 전혀 안 바꾸고 토스트만 안내한다 - 롤백할 상태 자체가
        /// 없으므로 별도 정리가 필요 없다.
        /// </summary>
        public bool TryEnter()
        {
            if (IsActive || stageController == null || stageController.IsOverlayActive || dummyPrefab == null)
            {
                return false;
            }

            if (!TrySpawnDummy())
            {
                PublishSpawnFailureToast();
                return false;
            }

            IsActive = true;
            stageController.PauseForOverlay("연습 스테이지");

            return true;
        }

        /// <summary>
        /// 연습 스테이지를 나가 원래 스테이지로 복귀한다. 진행 중이 아니면 무시한다.
        /// </summary>
        public void Exit()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;

            ReleaseDummy();
            stageController?.ResumeAfterOverlay();
        }

        /// <summary>
        /// 허수아비 프리팹을 스폰한다. PoolManager를 못 구하면 false를 반환하고 아무 상태도
        /// 바꾸지 않는다 - 호출부(TryEnter)가 성공을 확인하기 전까지는 IsActive를 켜지 않으므로,
        /// 실패해도 롤백할 게 없다(GitHub 이슈 #20).
        /// </summary>
        private bool TrySpawnDummy()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return false;
            }

            pool.EnsurePool(dummyPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.BossSpawnPosition();
            _dummyInstance = pool.Get(dummyPrefab, spawnPosition, Quaternion.identity);

            if (_dummyInstance == null)
            {
                return false;
            }

            if (_dummyInstance.TryGetComponent(out MonsterVisualRandomizer visualRandomizer))
            {
                visualRandomizer.ApplyVisualSet(dummyVisualSet);
            }

            return true;
        }

        private static void PublishSpawnFailureToast()
        {
            GameBootstrapper.Events?.Publish(new ToastMessageRequestedEvent("전투 대상을 생성하지 못했습니다. 잠시 후 다시 시도해주세요."));
        }

        private void ReleaseDummy()
        {
            if (_dummyInstance != null && DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                pool.Release(_dummyInstance);
            }

            _dummyInstance = null;
        }

        private void OnDestroy()
        {
            if (IsActive)
            {
                IsActive = false;
                ReleaseDummy();

                // GitHub 이슈 #54 - OnDestroy()는 이 컨트롤러에게 항상 teardown 신호다(정상 종료는
                // Exit() 등 별도 경로를 탄다). ResumeAfterOverlay()는 positionResetter 등 파괴
                // 순서를 보장할 수 없는 외부 오브젝트를 건드려 예외를 던질 수 있으므로(실제
                // 재현됨: MissingReferenceException), 부작용 없는 teardown 전용 API로 대체한다.
                stageController?.ReleaseOverlayForTeardown();
            }
        }
    }
}
