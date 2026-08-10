using Core;
using Soldier;
using Stage.Events;
using UnityEngine;

namespace Character
{
    /// <summary>
    /// N-40(챕터 클라이맥스) 스테이지에 들어갈 때 플레이어와 병사들의 위치를 초기화한다.
    /// 방패벽 전술(section BW~CM)의 스폰 방향은 플레이어의 현재 화면 위치를 기준으로 정해지고
    /// (section CC의 DetermineSpawnSide), 병사는 평소 자유롭게 돌아다니므로, 이전 스테이지에서
    /// 남은 위치 그대로 이 전투에 들어가면 매번 스폰 방향/대형 배치가 달라지고 병사들도
    /// 뿔뿔이 흩어진 채로 시작한다. 챕터와 무관하게 StageNumber == 40이면 항상 리셋한다.
    /// </summary>
    public sealed class ClimaxStagePositionResetter : MonoBehaviour
    {
        private const int ClimaxStageNumber = 40;

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
            if (evt.StageNumber != ClimaxStageNumber)
            {
                return;
            }

            transform.localPosition = _startLocalPosition;
            soldierSpawner?.ResetSoldierPositions();
        }
    }
}
