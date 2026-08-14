using Core;
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
    /// </summary>
    public sealed class StagePositionResetter : MonoBehaviour
    {
        [SerializeField]
        private SoldierSpawner soldierSpawner;

        private Vector3 _startLocalPosition;

        private void Awake()
        {
            _startLocalPosition = transform.localPosition;
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
    }
}
