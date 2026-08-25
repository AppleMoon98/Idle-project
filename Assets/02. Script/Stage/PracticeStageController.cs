using Character;
using Core;
using Dungeon;
using Managers;
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
        /// 이 경우 던전 중 사용 불가 안내를 띄운다.
        /// </summary>
        public bool TryEnter()
        {
            if (IsActive || stageController == null || stageController.IsOverlayActive || dummyPrefab == null)
            {
                return false;
            }

            IsActive = true;

            stageController.PauseForOverlay("연습 스테이지");
            SpawnDummy();

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

        private void SpawnDummy()
        {
            if (!DungeonSpawnUtility.TryGetPool(out PoolManager pool))
            {
                return;
            }

            pool.EnsurePool(dummyPrefab, 1, 1);

            Vector3 spawnPosition = DungeonSpawnUtility.BossSpawnPosition();
            _dummyInstance = pool.Get(dummyPrefab, spawnPosition, Quaternion.identity);

            if (_dummyInstance.TryGetComponent(out MonsterVisualRandomizer visualRandomizer))
            {
                visualRandomizer.ApplyVisualSet(dummyVisualSet);
            }
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
                stageController?.ResumeAfterOverlay();
            }
        }
    }
}
