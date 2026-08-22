using Core;
using Skill;
using Soldier;
using Stage.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// 스테이지가 바뀔 때마다(진행/반복/사망 후퇴 전부 포함) 그리고 던전 오버레이에 들어가고
    /// 나올 때마다(Stage.StageController.PauseForOverlay/ResumeAfterOverlay가 직접 호출)
    /// 플레이어와 병사들의 위치를 씬에 배치된 시작 좌표로 되돌린다. 방패벽 전술(section BW~CM)의
    /// 스폰 방향은 플레이어의 현재 화면 위치를 기준으로 정해지고(section CC의
    /// DetermineSpawnSide), 병사는 평소 자유롭게 돌아다니므로, 이전 위치 그대로 다음 전투(또는
    /// 던전에서 돌아온 스테이지)에 들어가면 매번 스폰 방향/대형 배치가 달라지고 병사들도
    /// 뿔뿔이 흩어진 채로 시작한다. 던전 진입/이탈은 StageChangedEvent를 발행하지 않으므로
    /// (StageSO/StageProgression 파이프라인을 건드리지 않는다는 설계, Dungeon 도메인 각 세션
    /// 컨트롤러의 doc 참고) 이벤트 구독만으로는 커버되지 않아 StageController가 직접 호출한다.
    /// 같은 이유로 체력 리셋(ResetHealth)도 함께 소유한다 — 병사 체력/사망 슬롯 복구는 원래
    /// StageChangedEvent(Soldier.SoldierSpawner.OnStageChanged)에서만 일어나는데, 던전 오버레이는
    /// 이 이벤트를 발행하지 않아 재도전/복귀 때 플레이어와 병사 모두 이전 시도의 피해를 그대로
    /// 이어받는 문제가 있었다(실사용 중 발견).
    /// </summary>
    public sealed class StagePositionResetter : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawner soldierSpawner;

        private Health _health;
        private Vector3 _startLocalPosition;

        private void Awake()
        {
            _startLocalPosition = transform.localPosition;
            TryGetComponent(out _health);
        }

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<StageChangedEvent>(OnStageChanged);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            ResetPositions();
            ResetSkillCooldowns();
        }

        /// <summary>
        /// 플레이어와 병사 위치를 즉시 되돌린다. StageChangedEvent 구독 핸들러가 쓰는 것과 동일한
        /// 로직을 StageController가 던전 오버레이 진입/이탈 시점에 직접 호출할 수 있도록 공개한다.
        /// </summary>
        public void ResetPositions()
        {
            transform.localPosition = _startLocalPosition;
            soldierSpawner?.ResetSoldierPositions();
        }

        /// <summary>
        /// 등록된 6개 스킬 슬롯 전체의 쿨다운을 즉시 발동 가능 상태로 되돌린다 —
        /// Stage.StageController.ResetSkillCooldowns()(던전 입장 시점 전용)와 완전히 같은 조회
        /// 방식을 여기서도 그대로 쓴다. 이쪽은 스테이지가 바뀔 때마다(진행/반복/사망 후퇴 전부)
        /// 매번 호출된다 — 방금 쓴 스킬의 남은 쿨다운을 다음 스테이지까지 그대로 들고 가지 않도록.
        /// </summary>
        private void ResetSkillCooldowns()
        {
            if (GameBootstrapper.Services != null && GameBootstrapper.Services.TryGet(out SkillLoadoutService loadout))
            {
                loadout.ResetAllCooldowns();
            }
        }

        /// <summary>
        /// 플레이어 체력을 최대치로(생사 무관 무조건 Revive — Character.PlayerReviveOnStageChanged가
        /// 매 StageChangedEvent마다 무조건 호출하는 것과 동일한 관례), 병사들의 체력/사망 슬롯을
        /// 새 시도 시작 시점처럼 되돌린다. Stage.StageController.ResumeAfterOverlay(던전 클리어/나가기)와
        /// 각 Dungeon 세션 컨트롤러의 Retry(재도전)가 호출한다.
        /// </summary>
        public void ResetHealth()
        {
            _health?.Revive();
            soldierSpawner?.ResetSoldiersForRetry();
        }
    }
}
