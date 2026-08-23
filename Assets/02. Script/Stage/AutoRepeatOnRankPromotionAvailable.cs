using Core;
using Rank;
using Rank.Events;
using Stage.Events;
using UnityEngine;

namespace Stage
{
    /// <summary>
    /// 랭크 승급 가능 상태(RankService.IsNextRankAvailable)가 false에서 true로 바뀌는 순간, 스테이지
    /// 진행 방침을 자동으로 반복으로 전환하고 현재 스테이지를 역대 최고 클리어 스테이지로 옮긴다.
    /// UI(RankUpAvailableTextUI)와 똑같은 이벤트를 구독해 같은 가용성을 계산하지만, 이쪽은 순수
    /// 반응(게임 상태 변경)만 담당하고 화면 표시는 건드리지 않는다.
    /// </summary>
    public sealed class AutoRepeatOnRankPromotionAvailable : MonoBehaviour
    {
        [SerializeField]
        private StageController stageController;

        private bool _wasAvailable;

        private void OnEnable()
        {
            GameBootstrapper.Events?.Subscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            GameBootstrapper.Events?.Subscribe<RankChangedEvent>(OnRankChanged);
        }

        // 앱을 재시작하면 StageModeService.CurrentMode는 저장되지 않아 항상 Breakthrough로,
        // _wasAvailable도 새 인스턴스라 false로 되돌아간다 - 승급 가능 상태 자체는 세이브에서
        // 이미 복원돼 있는데(RankService.SeedHighestCleared), 이 컴포넌트는 다음
        // HighestStageClearedEvent/RankChangedEvent가 뜰 때까지 그 사실을 모른다. 그 사이
        // Breakthrough로 한 스테이지가 새어나가는 게(재시작할 때마다 반복) 실사용 중 발견됐다 -
        // GameBootstrapper(-1000)의 Start()가 먼저 끝나 RankService 시딩이 이미 완료된 뒤이므로,
        // 여기서도 이벤트를 기다리지 않고 시작하자마자 한 번 직접 확인한다.
        private void Start()
        {
            Refresh();
        }

        private void OnDisable()
        {
            GameBootstrapper.Events?.Unsubscribe<HighestStageClearedEvent>(OnHighestStageCleared);
            GameBootstrapper.Events?.Unsubscribe<RankChangedEvent>(OnRankChanged);
        }

        private void OnHighestStageCleared(HighestStageClearedEvent evt)
        {
            Refresh();
        }

        private void OnRankChanged(RankChangedEvent evt)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (GameBootstrapper.Services == null || !GameBootstrapper.Services.TryGet(out RankService rankService))
            {
                return;
            }

            bool available = rankService.IsNextRankAvailable();

            if (available && !_wasAvailable)
            {
                if (GameBootstrapper.Services.TryGet(out StageModeService modeService))
                {
                    modeService.SetMode(StageProgressionMode.Repeat);
                }

                stageController?.JumpCurrentToHighestCleared();
            }

            _wasAvailable = available;
        }
    }
}
